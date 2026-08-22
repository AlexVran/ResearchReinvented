#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PeteTimesSix.ResearchReinvented.Domain.Prototypes
{
	public interface IPrototypeService
	{
		bool Enabled { get; }
		bool IsReadOnly { get; }
		IReadOnlyList<PrototypeRecord> Records { get; }
		IReadOnlyList<PrototypeSavedRecord> Orphans { get; }
		IReadOnlyList<PrototypeDiagnostic> Diagnostics { get; }
		void SetEnabled(bool enabled);
		bool Register(PrototypeRecord record);
		bool IsActive(PrototypeArtifactKey key);
		bool Transition(PrototypeArtifactKey from, PrototypeArtifactKey to, PrototypeArtifactKind kind, PrototypeLifecycleState state);
		bool Finish(PrototypeArtifactKey key, PrototypeLifecycleState state);
		IReadOnlyList<PrototypeRecord> CancelProject(DefIdentity project);
		bool DisableFeature(string feature, string reason);
		bool IsFeatureEnabled(string feature);
		PrototypeSaveSnapshot CreateSnapshot();
		void Restore(PrototypeSaveSnapshot snapshot);
	}

	/// <summary>
	/// Pure per-game authority for prototype lifecycle. RimWorld references stay
	/// in map components; this service stores only stable semantic identities.
	/// </summary>
	public sealed class PrototypeService : IPrototypeService
	{
		private readonly Dictionary<PrototypeArtifactKey, PrototypeRecord> records = new();
		private readonly List<PrototypeSavedRecord> orphans = new();
		private readonly List<PrototypeDiagnostic> diagnostics = new();
		private readonly HashSet<string> disabledFeatures = new(StringComparer.Ordinal);
		private PrototypeSaveSnapshot? futureSnapshot;

		public bool Enabled { get; private set; } = true;
		public bool IsReadOnly => futureSnapshot != null;
		public IReadOnlyList<PrototypeRecord> Records => new ReadOnlyCollection<PrototypeRecord>(records.Values.OrderBy(item => item.Key).ToArray());
		public IReadOnlyList<PrototypeSavedRecord> Orphans => new ReadOnlyCollection<PrototypeSavedRecord>(orphans.ToArray());
		public IReadOnlyList<PrototypeDiagnostic> Diagnostics => new ReadOnlyCollection<PrototypeDiagnostic>(diagnostics.ToArray());

		public void SetEnabled(bool enabled) => Enabled = enabled;

		public bool Register(PrototypeRecord record)
		{
			if (record == null) throw new ArgumentNullException(nameof(record));
			if (!Enum.IsDefined(typeof(PrototypeArtifactKind), record.Kind) || !Enum.IsDefined(typeof(PrototypeLifecycleState), record.State))
				throw new ArgumentException("Prototype records require known artifact and lifecycle values.", nameof(record));
			if (!Enabled || IsReadOnly) return false;
			if (records.TryGetValue(record.Key, out var existing))
			{
				records[record.Key] = Prefer(existing, record);
				return false;
			}
			records.Add(record.Key, record);
			return true;
		}

		public bool IsActive(PrototypeArtifactKey key) => Enabled && records.TryGetValue(key, out var record) && record.State == PrototypeLifecycleState.Active;

		public bool Transition(PrototypeArtifactKey from, PrototypeArtifactKey to, PrototypeArtifactKind kind, PrototypeLifecycleState state)
		{
			if (!Enabled || IsReadOnly || !records.TryGetValue(from, out var record)) return false;
			records.Remove(from);
			var transitioned = record.With(to, kind, state);
			if (records.TryGetValue(to, out var existing))
			{
				records[to] = Prefer(existing, transitioned);
				diagnostics.Add(new PrototypeDiagnostic("DuplicateMerged", "Duplicate prototype semantic state was deterministically merged.", to.Value));
			}
			else records.Add(to, transitioned);
			return true;
		}

		public bool Finish(PrototypeArtifactKey key, PrototypeLifecycleState state)
		{
			if (state == PrototypeLifecycleState.Active) throw new ArgumentException("Finish requires a terminal state.", nameof(state));
			if (!Enabled || IsReadOnly || !records.TryGetValue(key, out var record)) return false;
			records[key] = Prefer(record, record.With(key, record.Kind, state));
			return true;
		}

		public IReadOnlyList<PrototypeRecord> CancelProject(DefIdentity project)
		{
			if (project == null) throw new ArgumentNullException(nameof(project));
			if (!Enabled || IsReadOnly) return Array.Empty<PrototypeRecord>();
			var cancelled = records.Values.Where(item => item.Project == project && item.State == PrototypeLifecycleState.Active)
				.OrderBy(item => item.Key).ToArray();
			foreach (var item in cancelled)
				records[item.Key] = item.With(item.Key, item.Kind, PrototypeLifecycleState.Cancelled);
			return new ReadOnlyCollection<PrototypeRecord>(cancelled);
		}

		public bool DisableFeature(string feature, string reason)
		{
			if (string.IsNullOrWhiteSpace(feature)) throw new ArgumentException("A feature id is required.", nameof(feature));
			if (!disabledFeatures.Add(feature)) return false;
			diagnostics.Add(new PrototypeDiagnostic("FeatureDisabled", reason, feature));
			return true;
		}

		public bool IsFeatureEnabled(string feature) => Enabled && !disabledFeatures.Contains(feature);

		public PrototypeSaveSnapshot CreateSnapshot()
		{
			if (futureSnapshot != null) return futureSnapshot;
			return new PrototypeSaveSnapshot(PrototypeSaveSnapshot.CurrentSchemaVersion,
				records.Values.Select(ToSaved).Concat(orphans).OrderBy(StableSortKey, StringComparer.Ordinal));
		}

		public void Restore(PrototypeSaveSnapshot snapshot)
		{
			if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
			records.Clear();
			orphans.Clear();
			diagnostics.Clear();
			futureSnapshot = null;
			if (snapshot.SchemaVersion > PrototypeSaveSnapshot.CurrentSchemaVersion)
			{
				futureSnapshot = snapshot;
				diagnostics.Add(new PrototypeDiagnostic("FutureSchema", $"Prototype schema {snapshot.SchemaVersion} is newer than supported schema {PrototypeSaveSnapshot.CurrentSchemaVersion}; prototype mutation is disabled while ordinary research remains available."));
				return;
			}
			if (snapshot.SchemaVersion < 1)
				diagnostics.Add(new PrototypeDiagnostic("MalformedSchema", $"Prototype schema {snapshot.SchemaVersion} is invalid; only well-formed records will be salvaged."));

			foreach (var saved in snapshot.Records.OrderBy(StableSortKey, StringComparer.Ordinal))
			{
				if (!TryRestore(saved, out var restored))
				{
					orphans.Add(saved);
					diagnostics.Add(new PrototypeDiagnostic("MalformedRecord", "Malformed prototype state was retained without activating an unrelated artifact.", saved.Key));
					continue;
				}
				if (records.TryGetValue(restored!.Key, out var existing))
				{
					records[restored.Key] = Prefer(existing, restored);
					diagnostics.Add(new PrototypeDiagnostic("DuplicateMerged", "Duplicate prototype semantic state was deterministically merged.", restored.Key.Value));
				}
				else records.Add(restored.Key, restored);
			}
		}

		private static bool TryRestore(PrototypeSavedRecord saved, out PrototypeRecord? record)
		{
			record = null;
			if (saved.Project == null || string.IsNullOrWhiteSpace(saved.Key)
				|| !Enum.IsDefined(typeof(PrototypeArtifactKind), saved.Kind)
				|| !Enum.IsDefined(typeof(PrototypeLifecycleState), saved.State)) return false;
			try
			{
				var key = PrototypeArtifactKey.Parse(saved.Key!);
				var opportunity = string.IsNullOrWhiteSpace(saved.OpportunityKey) ? null : OpportunityKey.Parse(saved.OpportunityKey!);
				record = new PrototypeRecord(key, saved.Project, saved.Kind, saved.State, opportunity, saved.Subject);
				return true;
			}
			catch (FormatException)
			{
				return false;
			}
		}

		private static PrototypeSavedRecord ToSaved(PrototypeRecord item) =>
			new PrototypeSavedRecord(item.Key.Value, item.Project, item.Kind, item.State, item.Opportunity?.Value, item.Subject);

		private static PrototypeRecord Prefer(PrototypeRecord left, PrototypeRecord right) =>
			Rank(left.State) > Rank(right.State) ? left
				: Rank(right.State) > Rank(left.State) ? right
				: string.Compare(left.Subject, right.Subject, StringComparison.Ordinal) <= 0 ? left : right;

		private static int Rank(PrototypeLifecycleState state) => state switch
		{
			PrototypeLifecycleState.Completed => 4,
			PrototypeLifecycleState.Failed => 3,
			PrototypeLifecycleState.Cancelled => 2,
			_ => 1,
		};

		private static string StableSortKey(PrototypeSavedRecord item) =>
			$"{item.Key ?? string.Empty}|{item.Project?.CanonicalValue ?? string.Empty}|{item.Kind}|{item.State}|{item.OpportunityKey ?? string.Empty}|{item.Subject ?? string.Empty}";
	}
}
