#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PeteTimesSix.ResearchReinvented.Domain.State
{
	public interface IOpportunityService
	{
		OpportunityStateLoadStatus LoadStatus { get; }
		bool IsReadOnly { get; }
		DefIdentity? ActiveProject { get; set; }
		IReadOnlyList<OpportunityReconciliationDiagnostic> Diagnostics { get; }
		IReadOnlyList<SavedOpportunityState> Orphans { get; }
		IReadOnlyList<OpportunitySpecificationState> SpecificationsFor(DefIdentity project);
		void Restore(OpportunitySaveSnapshot snapshot);
		void RestoreLegacy(IEnumerable<LegacyOpportunityMigrationDto> state, IEnumerable<SavedCategoryBudget> budgets, DefIdentity? activeProject, IEnumerable<DefIdentity>? generatedProjects, int settingsChangeTicker);
		void SetSpecifications(DefIdentity project, IEnumerable<OpportunitySpecificationState> specifications, IEnumerable<SavedCategoryBudget> budgets, OpportunityReconciliationAliases? aliases = null);
		float ApplyProgress(OpportunityKey key, float amount);
		float ProgressFor(OpportunityKey key);
		float MaximumFor(OpportunityKey key);
		float CategoryProgress(DefIdentity project, DefIdentity category);
		OpportunitySaveSnapshot CreateSnapshot(int settingsChangeTicker);
		void RemoveProject(DefIdentity project);
		void RetainUnmatchedProject(DefIdentity project, string reason);
		void Reset();
	}

	/// <summary>
	/// Pure per-game authority for specifications and mutable progress. Runtime
	/// jobs and UI may continue to use legacy projections, but those projections
	/// bind to this service and are never the saved source of truth.
	/// </summary>
	public sealed class OpportunityService : IOpportunityService
	{
		private readonly Dictionary<DefIdentity, IReadOnlyList<OpportunitySpecificationState>> specifications = new();
		private readonly Dictionary<OpportunityKey, OpportunityProgressState> progress = new();
		private readonly Dictionary<OpportunityKey, OpportunitySpecificationState> specificationByKey = new();
		private readonly Dictionary<string, SavedCategoryBudget> categoryBudgets = new(StringComparer.Ordinal);
		private readonly List<SavedOpportunityState> pending = new();
		private readonly List<SavedOpportunityState> orphans = new();
		private readonly HashSet<DefIdentity> generatedProjects = new();
		private readonly List<OpportunityReconciliationDiagnostic> diagnostics = new();
		private OpportunitySaveSnapshot? preservedFutureSnapshot;

		public OpportunityStateLoadStatus LoadStatus { get; private set; } = OpportunityStateLoadStatus.NewGame;
		public bool IsReadOnly => preservedFutureSnapshot != null;
		public DefIdentity? ActiveProject { get; set; }
		public IReadOnlyList<OpportunityReconciliationDiagnostic> Diagnostics => new ReadOnlyCollection<OpportunityReconciliationDiagnostic>(diagnostics.ToArray());
		public IReadOnlyList<SavedOpportunityState> Orphans => new ReadOnlyCollection<SavedOpportunityState>(orphans.OrderBy(item => item.StableSortKey, StringComparer.Ordinal).ToArray());

		public IReadOnlyList<OpportunitySpecificationState> SpecificationsFor(DefIdentity project) =>
			specifications.TryGetValue(project, out var value) ? value : Array.Empty<OpportunitySpecificationState>();

		public void Restore(OpportunitySaveSnapshot snapshot)
		{
			if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
			ClearMutableState();
			ActiveProject = snapshot.ActiveProject;
			foreach (var project in snapshot.GeneratedProjects) generatedProjects.Add(project);
			if (snapshot.SchemaVersion > OpportunitySaveSnapshot.CurrentSchemaVersion)
			{
				preservedFutureSnapshot = snapshot;
				LoadStatus = OpportunityStateLoadStatus.FutureVersionReadOnly;
				diagnostics.Add(new OpportunityReconciliationDiagnostic("FutureSchema", $"Schema {snapshot.SchemaVersion} is newer than supported schema {OpportunitySaveSnapshot.CurrentSchemaVersion}; state is preserved read-only."));
				return;
			}

			LoadStatus = snapshot.SchemaVersion == OpportunitySaveSnapshot.CurrentSchemaVersion
				? OpportunityStateLoadStatus.Loaded
				: OpportunityStateLoadStatus.SalvagedMalformed;
			if (snapshot.SchemaVersion < 1)
				diagnostics.Add(new OpportunityReconciliationDiagnostic("MalformedSchema", $"Schema {snapshot.SchemaVersion} is invalid; finite non-negative records will be salvaged."));
			QueueForReconciliation(snapshot.Progress.Concat(snapshot.SpecialState));
			foreach (var budget in snapshot.CategoryBudgets) categoryBudgets[CategoryKey(budget.Project, budget.Category)] = budget;
		}

		public void RestoreLegacy(
			IEnumerable<LegacyOpportunityMigrationDto> state,
			IEnumerable<SavedCategoryBudget> budgets,
			DefIdentity? activeProject,
			IEnumerable<DefIdentity>? generated,
			int settingsChangeTicker)
		{
			ClearMutableState();
			ActiveProject = activeProject;
			QueueForReconciliation((state ?? Array.Empty<LegacyOpportunityMigrationDto>()).Select(item => item.State));
			foreach (var budget in budgets ?? Array.Empty<SavedCategoryBudget>()) categoryBudgets[CategoryKey(budget.Project, budget.Category)] = budget;
			foreach (var project in generated ?? Array.Empty<DefIdentity>()) generatedProjects.Add(project);
			LoadStatus = OpportunityStateLoadStatus.MigratedLegacy;
			diagnostics.Add(new OpportunityReconciliationDiagnostic("LegacyMigration", $"Queued {pending.Count} legacy opportunity records for deterministic reconciliation."));
		}

		public void SetSpecifications(
			DefIdentity project,
			IEnumerable<OpportunitySpecificationState> newSpecifications,
			IEnumerable<SavedCategoryBudget> budgets,
			OpportunityReconciliationAliases? aliases = null)
		{
			if (project == null) throw new ArgumentNullException(nameof(project));
			if (newSpecifications == null) throw new ArgumentNullException(nameof(newSpecifications));
			aliases ??= new OpportunityReconciliationAliases();
			var ordered = newSpecifications.OrderBy(item => item.Spec.Key).ToArray();
			if (ordered.Any(item => item.Spec.Project != project))
				throw new ArgumentException("All specifications must belong to the supplied project.", nameof(newSpecifications));
			if (ordered.GroupBy(item => item.Spec.Key).Any(group => group.Count() > 1))
				throw new ArgumentException("Specifications must have unique semantic keys.", nameof(newSpecifications));

			var existing = specifications.TryGetValue(project, out var prior)
				? prior.Select(item => SavedOpportunityState.FromSpecification(item, ProgressFor(item.Spec.Key)))
				: Array.Empty<SavedOpportunityState>();
			bool BelongsToProject(SavedOpportunityState item) => aliases.Canonicalize(item.Project) == project;
			var candidates = pending.Where(BelongsToProject).Concat(orphans.Where(BelongsToProject)).Concat(existing)
				.OrderBy(item => item.StableSortKey, StringComparer.Ordinal).ToArray();
			pending.RemoveAll(item => BelongsToProject(item));
			orphans.RemoveAll(item => BelongsToProject(item));
			foreach (var priorItem in prior ?? Array.Empty<OpportunitySpecificationState>())
			{
				progress.Remove(priorItem.Spec.Key);
				specificationByKey.Remove(priorItem.Spec.Key);
			}

			specifications[project] = Array.AsReadOnly(ordered);
			foreach (var item in ordered) specificationByKey[item.Spec.Key] = item;
			foreach (var budget in budgets ?? Array.Empty<SavedCategoryBudget>())
			{
				if (budget.Project == project) categoryBudgets[CategoryKey(project, budget.Category)] = budget;
			}
			generatedProjects.Add(project);

			var assignments = new Dictionary<OpportunityKey, List<SavedOpportunityState>>();
			foreach (var record in candidates)
			{
				if (!IsFiniteNonNegative(record.CurrentProgress) || !IsFiniteNonNegative(record.MaximumProgress))
				{
					orphans.Add(Sanitize(record));
					diagnostics.Add(new OpportunityReconciliationDiagnostic("MalformedProgress", "A non-finite or negative progress value was quarantined as a zero-credit orphan.", record.SourceId));
					continue;
				}

				var match = Match(record, ordered, aliases);
				if (match == null)
				{
					orphans.Add(record);
					diagnostics.Add(new OpportunityReconciliationDiagnostic("Orphaned", "No current specification matched; the complete record and safe category credit were retained.", record.SourceId ?? record.Key));
					continue;
				}
				if (!assignments.TryGetValue(match.Spec.Key, out var records)) assignments[match.Spec.Key] = records = new List<SavedOpportunityState>();
				records.Add(record);
			}

			foreach (var item in ordered)
			{
				var current = 0f;
				if (assignments.TryGetValue(item.Spec.Key, out var records))
				{
					current = records.Sum(record => record.CurrentProgress);
					if (records.Count > 1)
						diagnostics.Add(new OpportunityReconciliationDiagnostic("DuplicateMerged", $"Merged {records.Count} records into {item.Spec.Key}; progress was summed without truncation."));
				}
				progress[item.Spec.Key] = new OpportunityProgressState(item.Spec.Key, current, item.MaximumProgress);
			}
		}

		public float ApplyProgress(OpportunityKey key, float amount)
		{
			if (IsReadOnly) return 0f;
			if (key == null) throw new ArgumentNullException(nameof(key));
			return progress.TryGetValue(key, out var state) ? state.Apply(amount) : 0f;
		}

		public float ProgressFor(OpportunityKey key) => progress.TryGetValue(key, out var state) ? state.CurrentProgress : 0f;
		public float MaximumFor(OpportunityKey key) => progress.TryGetValue(key, out var state) ? state.MaximumProgress : 0f;

		public float CategoryProgress(DefIdentity project, DefIdentity category)
		{
			var matched = specificationByKey.Values
				.Where(item => item.Spec.Project == project && item.Category == category)
				.Sum(item => ProgressFor(item.Spec.Key));
			var orphaned = orphans.Where(item => item.Project == project && item.Category == category && IsFiniteNonNegative(item.CurrentProgress))
				.Sum(item => item.CurrentProgress);
			return matched + orphaned;
		}

		public OpportunitySaveSnapshot CreateSnapshot(int settingsChangeTicker)
		{
			if (preservedFutureSnapshot != null) return preservedFutureSnapshot;
			var ordinary = new List<SavedOpportunityState>();
			var special = new List<SavedOpportunityState>();
			foreach (var item in specificationByKey.Values.OrderBy(item => item.Spec.Key))
			{
				var record = SavedOpportunityState.FromSpecification(item, ProgressFor(item.Spec.Key));
				(item.StateKind == PreservedOpportunityStateKind.Ordinary ? ordinary : special).Add(record);
			}
			foreach (var orphan in orphans.OrderBy(item => item.StableSortKey, StringComparer.Ordinal))
				(orphan.StateKind == PreservedOpportunityStateKind.Ordinary ? ordinary : special).Add(orphan);
			foreach (var unmaterialized in pending.OrderBy(item => item.StableSortKey, StringComparer.Ordinal))
				(unmaterialized.StateKind == PreservedOpportunityStateKind.Ordinary ? ordinary : special).Add(unmaterialized);
			return new OpportunitySaveSnapshot(
				OpportunitySaveSnapshot.CurrentSchemaVersion,
				ActiveProject,
				generatedProjects,
				ordinary,
				special,
				categoryBudgets.Values,
				settingsChangeTicker,
				requiresSpecificationRegeneration: true);
		}

		public void RemoveProject(DefIdentity project)
		{
			if (specifications.TryGetValue(project, out var items))
				foreach (var item in items) { progress.Remove(item.Spec.Key); specificationByKey.Remove(item.Spec.Key); }
			specifications.Remove(project);
			pending.RemoveAll(item => item.Project == project);
			orphans.RemoveAll(item => item.Project == project);
			generatedProjects.Remove(project);
			foreach (var key in categoryBudgets.Where(item => item.Value.Project == project).Select(item => item.Key).ToArray()) categoryBudgets.Remove(key);
			if (ActiveProject == project) ActiveProject = null;
		}

		public void RetainUnmatchedProject(DefIdentity project, string reason)
		{
			if (project == null) throw new ArgumentNullException(nameof(project));
			var retained = pending.Where(item => item.Project == project).ToArray();
			pending.RemoveAll(item => item.Project == project);
			orphans.AddRange(retained);
			if (retained.Length > 0)
				diagnostics.Add(new OpportunityReconciliationDiagnostic("UnavailableProject", reason, project.CanonicalValue));
		}

		public void Reset()
		{
			ClearMutableState();
			LoadStatus = OpportunityStateLoadStatus.NewGame;
		}

		private static OpportunitySpecificationState? Match(
			SavedOpportunityState record,
			IReadOnlyList<OpportunitySpecificationState> available,
			OpportunityReconciliationAliases aliases)
		{
			if (!string.IsNullOrWhiteSpace(record.Key))
			{
				var exact = available.FirstOrDefault(item => string.Equals(item.Spec.Key.Value, record.Key, StringComparison.Ordinal));
				if (exact != null) return exact;
			}
			if (!record.HasUsableSemanticIdentity) return null;
			var project = aliases.Canonicalize(record.Project);
			var type = aliases.Canonicalize(record.Type);
			var subject = aliases.Canonicalize(record.CanonicalSubject);
			var alternates = new HashSet<DefIdentity>(record.AlternateSubjects.Select(aliases.Canonicalize).Where(item => item != null)!);
			return available
				.Where(item => aliases.Canonicalize(item.Spec.Project) == project
					&& aliases.Canonicalize(item.Spec.Type) == type
					&& item.Spec.Relation == record.Relation
					&& item.Spec.Requirement.Kind == record.RequirementKind)
				.Select(item => new
				{
					Item = item,
					Score = aliases.Canonicalize(item.Spec.Requirement.CanonicalSubject) == subject ? 2
						: item.Spec.Requirement.AlternateSubjects.Select(aliases.Canonicalize).Contains(subject) || alternates.Contains(aliases.Canonicalize(item.Spec.Requirement.CanonicalSubject)!) ? 1 : 0
				})
				.Where(match => match.Score > 0)
				.OrderByDescending(match => match.Score)
				.ThenBy(match => match.Item.Spec.Key)
				.Select(match => match.Item)
				.FirstOrDefault();
		}

		private static SavedOpportunityState Sanitize(SavedOpportunityState record) => new(
			record.Key, record.Project, record.Type, record.Relation, record.RequirementKind,
			record.CanonicalSubject, record.Category,
			IsFiniteNonNegative(record.CurrentProgress) ? record.CurrentProgress : 0f,
			IsFiniteNonNegative(record.MaximumProgress) ? record.MaximumProgress : 0f,
			record.StateKind, record.AlternateSubjects, record.SourceId);

		private void ClearMutableState()
		{
			specifications.Clear(); progress.Clear(); specificationByKey.Clear(); categoryBudgets.Clear();
			pending.Clear(); orphans.Clear(); generatedProjects.Clear(); diagnostics.Clear();
			preservedFutureSnapshot = null; ActiveProject = null;
		}

		private void QueueForReconciliation(IEnumerable<SavedOpportunityState> records)
		{
			foreach (var record in records)
			{
				if (record.Project == null)
				{
					orphans.Add(record);
					diagnostics.Add(new OpportunityReconciliationDiagnostic("MissingProjectIdentity", "A record without a usable project identity was retained as an orphan.", record.SourceId));
				}
				else
					pending.Add(record);
			}
		}

		private static bool IsFiniteNonNegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
		private static string CategoryKey(DefIdentity project, DefIdentity category) => project.CanonicalValue + "|" + category.CanonicalValue;
	}
}
