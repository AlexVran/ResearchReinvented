#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PeteTimesSix.ResearchReinvented.Opportunities;

namespace PeteTimesSix.ResearchReinvented.Domain.State
{
	public enum PreservedOpportunityStateKind
	{
		Ordinary,
		Special,
		Prototype
	}

	public enum OpportunityStateLoadStatus
	{
		NewGame,
		Loaded,
		MigratedLegacy,
		SalvagedMalformed,
		FutureVersionReadOnly
	}

	public sealed class OpportunitySpecificationState
	{
		public OpportunitySpecificationState(
			OpportunitySpec spec,
			DefIdentity category,
			float maximumProgress,
			PreservedOpportunityStateKind stateKind = PreservedOpportunityStateKind.Ordinary)
		{
			Spec = spec ?? throw new ArgumentNullException(nameof(spec));
			Category = category ?? throw new ArgumentNullException(nameof(category));
			MaximumProgress = FiniteNonNegative(maximumProgress, nameof(maximumProgress));
			StateKind = stateKind;
		}

		public OpportunitySpec Spec { get; }
		public DefIdentity Category { get; }
		public float MaximumProgress { get; }
		public PreservedOpportunityStateKind StateKind { get; }

		private static float FiniteNonNegative(float value, string name) =>
			float.IsNaN(value) || float.IsInfinity(value) || value < 0f
				? throw new ArgumentOutOfRangeException(name)
				: value;
	}

	/// <summary>
	/// Version-independent semantic state used both by the new save schema and
	/// the explicit legacy migration DTO. Def identities remain strings so a
	/// removed mod cannot erase the record while loading.
	/// </summary>
	public sealed class SavedOpportunityState
	{
		public SavedOpportunityState(
			string? key,
			DefIdentity? project,
			DefIdentity? type,
			ResearchRelation relation,
			RequirementKind requirementKind,
			DefIdentity? canonicalSubject,
			DefIdentity? category,
			float currentProgress,
			float maximumProgress,
			PreservedOpportunityStateKind stateKind = PreservedOpportunityStateKind.Ordinary,
			IEnumerable<DefIdentity>? alternateSubjects = null,
			string? sourceId = null)
		{
			Key = key;
			Project = project;
			Type = type;
			Relation = relation;
			RequirementKind = requirementKind;
			CanonicalSubject = canonicalSubject;
			Category = category;
			CurrentProgress = currentProgress;
			MaximumProgress = maximumProgress;
			StateKind = stateKind;
			AlternateSubjects = Array.AsReadOnly((alternateSubjects ?? Array.Empty<DefIdentity>())
				.Where(subject => subject != null)
				.Distinct()
				.OrderBy(subject => subject)
				.ToArray());
			SourceId = sourceId;
		}

		public string? Key { get; }
		public DefIdentity? Project { get; }
		public DefIdentity? Type { get; }
		public ResearchRelation Relation { get; }
		public RequirementKind RequirementKind { get; }
		public DefIdentity? CanonicalSubject { get; }
		public DefIdentity? Category { get; }
		public float CurrentProgress { get; }
		public float MaximumProgress { get; }
		public PreservedOpportunityStateKind StateKind { get; }
		public IReadOnlyList<DefIdentity> AlternateSubjects { get; }
		public string? SourceId { get; }

		public bool HasUsableSemanticIdentity => Project != null && Type != null && CanonicalSubject != null;

		public string StableSortKey => string.Join("|", new[]
		{
			Project?.CanonicalValue ?? string.Empty,
			Type?.CanonicalValue ?? string.Empty,
			Relation.ToString(),
			RequirementKind.ToString(),
			CanonicalSubject?.CanonicalValue ?? string.Empty,
			Key ?? string.Empty,
			SourceId ?? string.Empty,
		});

		public static SavedOpportunityState FromSpecification(
			OpportunitySpecificationState specification,
			float currentProgress)
		{
			if (specification == null) throw new ArgumentNullException(nameof(specification));
			var spec = specification.Spec;
			return new SavedOpportunityState(
				spec.Key.Value,
				spec.Project,
				spec.Type,
				spec.Relation,
				spec.Requirement.Kind,
				spec.Requirement.CanonicalSubject,
				specification.Category,
				currentProgress,
				specification.MaximumProgress,
				specification.StateKind,
				spec.Requirement.AlternateSubjects);
		}
	}

	public sealed class SavedCategoryBudget
	{
		public SavedCategoryBudget(DefIdentity project, DefIdentity category, float budget)
		{
			Project = project ?? throw new ArgumentNullException(nameof(project));
			Category = category ?? throw new ArgumentNullException(nameof(category));
			if (float.IsNaN(budget) || float.IsInfinity(budget) || budget < 0f)
				throw new ArgumentOutOfRangeException(nameof(budget));
			Budget = budget;
		}

		public DefIdentity Project { get; }
		public DefIdentity Category { get; }
		public float Budget { get; }
	}

	/// <summary>
	/// Explicit one-way DTO for the legacy deep-saved ResearchOpportunity graph.
	/// It contains no runtime object or loaded Def reference and is discarded
	/// after the service reconciles it to the versioned schema.
	/// </summary>
	public sealed class LegacyOpportunityMigrationDto
	{
		public LegacyOpportunityMigrationDto(SavedOpportunityState state, int? legacyLoadId = null)
		{
			State = state ?? throw new ArgumentNullException(nameof(state));
			LegacyLoadId = legacyLoadId;
		}

		public SavedOpportunityState State { get; }
		public int? LegacyLoadId { get; }
	}

	public sealed class OpportunitySaveSnapshot
	{
		public const int CurrentSchemaVersion = 1;

		public OpportunitySaveSnapshot(
			int schemaVersion,
			DefIdentity? activeProject,
			IEnumerable<DefIdentity>? generatedProjects,
			IEnumerable<SavedOpportunityState>? progress,
			IEnumerable<SavedOpportunityState>? specialState,
			IEnumerable<SavedCategoryBudget>? categoryBudgets,
			int settingsChangeTicker = -1,
			bool requiresSpecificationRegeneration = true)
		{
			SchemaVersion = schemaVersion;
			ActiveProject = activeProject;
			GeneratedProjects = ReadOnly(generatedProjects, item => item);
			Progress = ReadOnly(progress, item => item.StableSortKey);
			SpecialState = ReadOnly(specialState, item => item.StableSortKey);
			CategoryBudgets = ReadOnly(categoryBudgets, item => item.Project.CanonicalValue + "|" + item.Category.CanonicalValue);
			SettingsChangeTicker = settingsChangeTicker;
			RequiresSpecificationRegeneration = requiresSpecificationRegeneration;
		}

		public int SchemaVersion { get; }
		public DefIdentity? ActiveProject { get; }
		public IReadOnlyList<DefIdentity> GeneratedProjects { get; }
		public IReadOnlyList<SavedOpportunityState> Progress { get; }
		public IReadOnlyList<SavedOpportunityState> SpecialState { get; }
		public IReadOnlyList<SavedCategoryBudget> CategoryBudgets { get; }
		public int SettingsChangeTicker { get; }
		public bool RequiresSpecificationRegeneration { get; }

		private static IReadOnlyList<T> ReadOnly<T, TKey>(IEnumerable<T>? source, Func<T, TKey> key) =>
			new ReadOnlyCollection<T>((source ?? Array.Empty<T>()).Where(item => item != null).OrderBy(key).ToArray());
	}

	public sealed class OpportunityReconciliationAliases
	{
		private readonly IReadOnlyDictionary<DefIdentity, DefIdentity> aliases;

		public OpportunityReconciliationAliases(IEnumerable<KeyValuePair<DefIdentity, DefIdentity>>? aliases = null)
		{
			this.aliases = new ReadOnlyDictionary<DefIdentity, DefIdentity>((aliases ?? Array.Empty<KeyValuePair<DefIdentity, DefIdentity>>())
				.GroupBy(pair => pair.Key)
				.ToDictionary(group => group.Key, group => group.OrderBy(pair => pair.Value).First().Value));
		}

		public DefIdentity? Canonicalize(DefIdentity? identity)
		{
			if (identity == null) return null;
			var current = identity;
			var visited = new HashSet<DefIdentity>();
			while (visited.Add(current) && aliases.TryGetValue(current, out var replacement))
				current = replacement;
			return current;
		}
	}

	public sealed class OpportunityReconciliationDiagnostic
	{
		public OpportunityReconciliationDiagnostic(string kind, string detail, string? sourceId = null)
		{
			Kind = kind ?? throw new ArgumentNullException(nameof(kind));
			Detail = detail ?? throw new ArgumentNullException(nameof(detail));
			SourceId = sourceId;
		}

		public string Kind { get; }
		public string Detail { get; }
		public string? SourceId { get; }
	}
}
