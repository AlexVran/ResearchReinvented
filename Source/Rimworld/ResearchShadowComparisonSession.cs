#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using PeteTimesSix.ResearchReinvented.Defs;
using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Domain.Rules;
using PeteTimesSix.ResearchReinvented.Domain.Selection;
using PeteTimesSix.ResearchReinvented.Domain.Shadow;
using PeteTimesSix.ResearchReinvented.Opportunities;
using RimWorld;
using Verse;

namespace PeteTimesSix.ResearchReinvented.Rimworld
{
	/// <summary>
	/// Opt-in, non-authoritative Phase 7 observer. It reads the legacy result,
	/// generates a detached semantic comparison, and writes bounded diagnostics.
	/// </summary>
	internal static class ResearchShadowComparisonSession
	{
		private const int RuntimeDiagnosticLimit = 64;
		private static IReadOnlyList<OpportunityCategoryAssignment>? categoryAssignments;

		internal static void Initialize(IResearchRuntimeServices services)
		{
			if (services == null) throw new ArgumentNullException(nameof(services));
			if (categoryAssignments != null) return;
			var assignments = new List<OpportunityCategoryAssignment>();
			foreach (var type in services.AllDefsListForReading<ResearchOpportunityTypeDef>().Where(type => type != null))
			{
				var typeIdentity = IdentityFor<ResearchOpportunityTypeDef>(type);
				foreach (var relation in new[] { ResearchRelation.Direct, ResearchRelation.Ancestor, ResearchRelation.Descendant })
				{
					var category = type.GetCategory(relation);
					if (category != null)
						assignments.Add(new OpportunityCategoryAssignment(typeIdentity, relation, IdentityFor<ResearchOpportunityCategoryDef>(category)));
				}
			}
			categoryAssignments = assignments
				.GroupBy(item => item.Type.CanonicalValue + "|" + item.Relation.ToString(), StringComparer.Ordinal)
				.Select(group => group.First()).ToArray();
		}

		internal static void Compare(
			ResearchProjectDef project,
			IReadOnlyCollection<ResearchOpportunity> legacyOpportunities,
			IReadOnlyCollection<ResearchOpportunityCategoryTotalsStore> categoryStores)
		{
			if (!ResearchReinvented_Debug.shadowComparisons) return;
			try
			{
				CompareCore(project, legacyOpportunities, categoryStores);
			}
			catch (Exception exception)
			{
				Log.Error($"RR shadow: observer failed without changing the authoritative legacy result: {exception}");
			}
		}

		private static void CompareCore(
			ResearchProjectDef project,
			IReadOnlyCollection<ResearchOpportunity> legacyOpportunities,
			IReadOnlyCollection<ResearchOpportunityCategoryTotalsStore> categoryStores)
		{
			if (project == null || categoryAssignments == null)
			{
				Log.Warning("RR shadow: comparison was requested before its runtime inputs were initialized.");
				return;
			}

			var projectIdentity = IdentityFor<ResearchProjectDef>(project);
			var legacySpecs = new List<OpportunitySpec>();
			var conversionFailures = new List<string>();
			foreach (var opportunity in legacyOpportunities)
			{
				try
				{
					var adapted = LegacyOpportunityAdapter.ToSpec(opportunity);
					legacySpecs.Add(adapted);
				}
				catch (Exception exception) { conversionFailures.Add(exception.GetType().Name + ": " + exception.Message); }
			}

			var budgets = categoryStores.Where(store => store?.category != null)
				.GroupBy(store => IdentityFor<ResearchOpportunityCategoryDef>(store.category))
				.ToDictionary(group => group.Key, group => group.First().researchPoints);
			var selectionPolicy = new OpportunitySelectionPolicy(
				categories: categoryAssignments,
				categoryBudgets: budgets,
				projectBudget: project.baseCost,
				diagnosticLimit: RuntimeDiagnosticLimit);
			var stopwatch = Stopwatch.StartNew();
			var generated = OpportunityRuleRegistry.CreateDefault().Generate(ResearchDefIndexSession.Current, projectIdentity);
			var generatedUniverse = new OpportunitySelector().Select(
				ResearchDefIndexSession.Current,
				generated,
				new OpportunitySelectionPolicy(
					categories: categoryAssignments,
					categoryBudgets: budgets,
					projectBudget: project.baseCost,
					defaultCategoryLimit: int.MaxValue,
					defaultRuleLimit: int.MaxValue,
					diversityFamilyLimit: int.MaxValue,
					diagnosticLimit: RuntimeDiagnosticLimit));
			var selected = new OpportunitySelector().Select(ResearchDefIndexSession.Current, generated, selectionPolicy);
			stopwatch.Stop();

			var comparisonPolicy = ShadowComparisonPolicies.Phase7(RuntimeDiagnosticLimit);
			var result = new OpportunityShadowComparer().Compare(
				projectIdentity,
				legacySpecs,
				selected,
				generatedUniverse.Selected.Select(item => item.Spec),
				budgets,
				project.baseCost,
				comparisonPolicy,
				spec => OpportunitySelector.NormalizeForComparison(
					ResearchDefIndexSession.Current,
					ShadowComparisonCanonicalizer.StaticAvailability(spec)));
			Log.Message($"RR shadow: project={project.defName} legacy={result.LegacyCount} new={result.NewCount} matched={result.MatchedCount} differences={result.TotalDifferenceCount} unexplained={result.UnexplainedCount} selectionRejected={result.NewSelectionRejectionCount} legacyConversionFailures={conversionFailures.Count} coldIndexMs={Milliseconds(ResearchDefIndexSession.ConstructionElapsedMilliseconds)} warmGenerationMs={Milliseconds(stopwatch.Elapsed.TotalMilliseconds)}");

			var remaining = RuntimeDiagnosticLimit;
			var shownDifferences = Math.Min(result.Differences.Count, remaining);
			foreach (var difference in result.Differences.Take(shownDifferences))
			{
				Log.Message($"RR shadow difference: {difference.Kind}|{difference.Classification}|{difference.Key}|{difference.Detail}");
				remaining--;
			}
			var shownRejections = Math.Min(selected.Rejections.Count, remaining);
			foreach (var rejection in selected.Rejections.Take(shownRejections))
			{
				Log.Message($"RR shadow selection rejection: {rejection.Kind}|{rejection.SourceDef}|{rejection.Detail}");
				remaining--;
			}
			var shownConversionFailures = Math.Min(conversionFailures.Count, remaining);
			foreach (var failure in conversionFailures.Take(shownConversionFailures))
			{
				Log.Message("RR shadow legacy conversion failure: " + failure);
				remaining--;
			}
			var suppressed = result.SuppressedDifferenceCount + selected.SuppressedDiagnostics
				+ (selected.Rejections.Count - shownRejections)
				+ (conversionFailures.Count - shownConversionFailures);
			if (suppressed > 0)
				Log.Message($"RR shadow: suppressed {suppressed} additional diagnostic details.");
		}

		private static DefIdentity IdentityFor<TDef>(TDef definition) where TDef : Def => new DefIdentity(typeof(TDef).Name, definition.defName);

		private static string Milliseconds(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
	}
}
