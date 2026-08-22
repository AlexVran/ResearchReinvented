#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using PeteTimesSix.ResearchReinvented.Defs;
using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Domain.Rules;
using PeteTimesSix.ResearchReinvented.Domain.Selection;
using PeteTimesSix.ResearchReinvented.Domain.State;
using PeteTimesSix.ResearchReinvented.Managers;
using PeteTimesSix.ResearchReinvented.Opportunities;
using RimWorld;
using Verse;

namespace PeteTimesSix.ResearchReinvented.Rimworld
{
	internal sealed class OpportunityProjection
	{
		internal OpportunityProjection(OpportunitySpecificationState specification, ResearchOpportunity legacy)
		{
			Specification = specification;
			Legacy = legacy;
		}

		internal OpportunitySpecificationState Specification { get; }
		internal ResearchOpportunity Legacy { get; }
	}

	internal sealed class OpportunityProjectGeneration
	{
		internal OpportunityProjectGeneration(
			IEnumerable<OpportunityProjection> projections,
			IEnumerable<OpportunitySpecificationState> specifications,
			IEnumerable<SavedCategoryBudget> budgets,
			IEnumerable<RejectionReason> rejections,
			int suppressedDiagnostics)
		{
			Projections = projections.ToArray();
			Specifications = specifications.ToArray();
			Budgets = budgets.ToArray();
			Rejections = rejections.ToArray();
			SuppressedDiagnostics = suppressedDiagnostics;
		}

		internal IReadOnlyList<OpportunityProjection> Projections { get; }
		internal IReadOnlyList<OpportunitySpecificationState> Specifications { get; }
		internal IReadOnlyList<SavedCategoryBudget> Budgets { get; }
		internal IReadOnlyList<RejectionReason> Rejections { get; }
		internal int SuppressedDiagnostics { get; }
	}

	/// <summary>
	/// Static Phase 5/6 generation followed by a legacy runtime projection.
	/// Current-map availability is consulted only while materializing a legacy
	/// faction requirement; it never changes the static candidate set.
	/// </summary>
	internal static class OpportunitySpecificationPipeline
	{
		internal static OpportunityProjectGeneration Generate(ResearchProjectDef project, IResearchRuntimeServices services)
		{
			if (project == null) throw new ArgumentNullException(nameof(project));
			if (services == null) throw new ArgumentNullException(nameof(services));
			var projectIdentity = IdentityFor<ResearchProjectDef>(project);
			var assignments = CategoryAssignments(services);
			var generated = OpportunityRuleRegistry.CreateDefault().Generate(ResearchDefIndexSession.Current, projectIdentity);
			var selected = new OpportunitySelector().Select(
				ResearchDefIndexSession.Current,
				generated,
				new OpportunitySelectionPolicy(categories: assignments, projectBudget: project.baseCost));

			var adapted = new List<(SelectedOpportunity Selected, ResearchOpportunity Legacy, bool Projectable)>();
			var stateOnly = new List<OpportunitySpecificationState>();
			var rejections = new List<RejectionReason>(selected.Rejections);
			foreach (var item in selected.Selected.OrderBy(item => item.Spec.Key))
			{
				if (LegacyOpportunityAdapter.TryCreateLegacy(item.Spec, out var legacy, out var rejection) && legacy != null)
					adapted.Add((item, legacy, true));
				else
				{
					if (rejection != null) rejections.Add(rejection);
					var type = services.AllDefsListForReading<ResearchOpportunityTypeDef>()
						.FirstOrDefault(definition => string.Equals(definition.defName, item.Spec.Type.DefName, StringComparison.Ordinal));
					if (type != null)
					{
						// Allocation is static. This detached placeholder never reaches
						// jobs or UI and merely preserves the spec's category budget/max
						// when no current-game subject can be projected.
						var placeholder = new ResearchOpportunity(project, type, item.Spec.Relation, new OpportunityComps.ROComp_RequiresNothing(), "state-only projection", item.Spec.Importance, item.Spec.Rare, item.Spec.Freebie);
						adapted.Add((item, placeholder, false));
					}
					else
						stateOnly.Add(new OpportunitySpecificationState(item.Spec, item.Category, 0f));
				}
			}

			var allocated = ResearchOpportunityPrefabs.AllocateProgress(project, adapted.Select(item => item.Legacy));
			var budgetByCategory = allocated.categoryStores.Where(store => store?.category != null)
				.Select(store => new SavedCategoryBudget(projectIdentity, IdentityFor<ResearchOpportunityCategoryDef>(store.category), store.researchPoints))
				.ToArray();
			var allocatedStates = adapted.Select(item =>
			{
				var category = item.Legacy.def.GetCategory(item.Legacy.relation);
				var stateKind = StateKind(item.Legacy.def);
				return new
				{
					State = new OpportunitySpecificationState(item.Selected.Spec, IdentityFor<ResearchOpportunityCategoryDef>(category), item.Legacy.StoredMaximumProgress, stateKind),
					item.Legacy,
					item.Projectable,
				};
			}).ToArray();
			var projections = allocatedStates.Where(item => item.Projectable)
				.Select(item => new OpportunityProjection(item.State, item.Legacy)).ToArray();
			var specifications = allocatedStates.Select(item => item.State).Concat(stateOnly).OrderBy(item => item.Spec.Key).ToArray();

			return new OpportunityProjectGeneration(projections, specifications, budgetByCategory, rejections, selected.SuppressedDiagnostics);
		}

		internal static IReadOnlyList<OpportunityCategoryAssignment> CategoryAssignments(IResearchRuntimeServices services)
		{
			return services.AllDefsListForReading<ResearchOpportunityTypeDef>()
				.Where(type => type != null)
				.SelectMany(type => new[] { ResearchRelation.Direct, ResearchRelation.Ancestor, ResearchRelation.Descendant }
					.Select(relation => (Type: type, Relation: relation, Category: type.GetCategory(relation))))
				.Where(item => item.Category != null)
				.Select(item => new OpportunityCategoryAssignment(
					IdentityFor<ResearchOpportunityTypeDef>(item.Type),
					item.Relation,
					IdentityFor<ResearchOpportunityCategoryDef>(item.Category)))
				.GroupBy(item => item.Type.CanonicalValue + "|" + item.Relation, StringComparer.Ordinal)
				.Select(group => group.First())
				.ToArray();
		}

		private static PreservedOpportunityStateKind StateKind(ResearchOpportunityTypeDef type)
		{
			if (type.handledBy.HasFlag(HandlingMode.Special_Prototype)) return PreservedOpportunityStateKind.Prototype;
			var special = HandlingMode.Special_OnIngest | HandlingMode.Special_OnIngest_Observable | HandlingMode.Special_Medicine | HandlingMode.Special_Tooling | HandlingMode.Special_Books;
			return (type.handledBy & special) != 0 ? PreservedOpportunityStateKind.Special : PreservedOpportunityStateKind.Ordinary;
		}

		internal static DefIdentity IdentityFor<TDef>(TDef definition) where TDef : Def =>
			new DefIdentity(typeof(TDef).Name, definition.defName);
	}
}
