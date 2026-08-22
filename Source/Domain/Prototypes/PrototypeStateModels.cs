#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PeteTimesSix.ResearchReinvented.Domain.Prototypes
{
	public enum PrototypeArtifactKind
	{
		Blueprint,
		Frame,
		UnfinishedItem,
		CompletedProduct,
		Terrain,
		FoundationTerrain,
		Bill,
		SurgeryBill,
	}

	public enum PrototypeLifecycleState
	{
		Active,
		Completed,
		Failed,
		Cancelled,
	}

	public sealed class PrototypeArtifactKey : IEquatable<PrototypeArtifactKey>, IComparable<PrototypeArtifactKey>
	{
		private const string Prefix = "rrp1|";

		private PrototypeArtifactKey(string value)
		{
			Value = value;
		}

		public string Value { get; }

		public static PrototypeArtifactKey Create(DefIdentity project, string mapKey, PrototypeArtifactKind kind, string artifactKey)
		{
			if (project == null) throw new ArgumentNullException(nameof(project));
			if (string.IsNullOrWhiteSpace(mapKey)) throw new ArgumentException("A prototype key needs a map identity.", nameof(mapKey));
			if (string.IsNullOrWhiteSpace(artifactKey)) throw new ArgumentException("A prototype key needs an artifact identity.", nameof(artifactKey));
			return new PrototypeArtifactKey($"{Prefix}project={Escape(project.CanonicalValue)}|map={Escape(mapKey)}|kind={kind}|artifact={Escape(artifactKey)}");
		}

		public static PrototypeArtifactKey Parse(string value)
		{
			if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
				throw new FormatException("Prototype keys must use the rrp1 stable-key format.");
			var segments = value.Substring(Prefix.Length).Split('|');
			if (segments.Length != 4
				|| !segments[0].StartsWith("project=", StringComparison.Ordinal)
				|| !segments[1].StartsWith("map=", StringComparison.Ordinal)
				|| !segments[2].StartsWith("kind=", StringComparison.Ordinal)
				|| !segments[3].StartsWith("artifact=", StringComparison.Ordinal)
				|| segments.Any(segment => segment.EndsWith("=", StringComparison.Ordinal)))
				throw new FormatException("Prototype keys must contain project, map, kind, and artifact identities.");
			return new PrototypeArtifactKey(value);
		}

		public bool Equals(PrototypeArtifactKey? other) => other != null && string.Equals(Value, other.Value, StringComparison.Ordinal);
		public override bool Equals(object? obj) => Equals(obj as PrototypeArtifactKey);
		public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
		public int CompareTo(PrototypeArtifactKey? other) => other == null ? 1 : string.Compare(Value, other.Value, StringComparison.Ordinal);
		public override string ToString() => Value;

		private static string Escape(string value) => Uri.EscapeDataString(value);
	}

	public sealed class PrototypeRecord
	{
		public PrototypeRecord(
			PrototypeArtifactKey key,
			DefIdentity project,
			PrototypeArtifactKind kind,
			PrototypeLifecycleState state,
			OpportunityKey? opportunity = null,
			string? subject = null)
		{
			Key = key ?? throw new ArgumentNullException(nameof(key));
			Project = project ?? throw new ArgumentNullException(nameof(project));
			Kind = kind;
			State = state;
			Opportunity = opportunity;
			Subject = subject;
		}

		public PrototypeArtifactKey Key { get; }
		public DefIdentity Project { get; }
		public PrototypeArtifactKind Kind { get; }
		public PrototypeLifecycleState State { get; }
		public OpportunityKey? Opportunity { get; }
		public string? Subject { get; }
		public PrototypeRecord With(PrototypeArtifactKey key, PrototypeArtifactKind kind, PrototypeLifecycleState state) =>
			new PrototypeRecord(key, Project, kind, state, Opportunity, Subject);
	}

	public sealed class PrototypeSavedRecord
	{
		public PrototypeSavedRecord(string? key, DefIdentity? project, PrototypeArtifactKind kind, PrototypeLifecycleState state, string? opportunityKey = null, string? subject = null)
		{
			Key = key;
			Project = project;
			Kind = kind;
			State = state;
			OpportunityKey = opportunityKey;
			Subject = subject;
		}

		public string? Key { get; }
		public DefIdentity? Project { get; }
		public PrototypeArtifactKind Kind { get; }
		public PrototypeLifecycleState State { get; }
		public string? OpportunityKey { get; }
		public string? Subject { get; }
	}

	public sealed class PrototypeSaveSnapshot
	{
		public const int CurrentSchemaVersion = 1;

		public PrototypeSaveSnapshot(int schemaVersion, IEnumerable<PrototypeSavedRecord>? records = null)
		{
			SchemaVersion = schemaVersion;
			Records = new ReadOnlyCollection<PrototypeSavedRecord>((records ?? Array.Empty<PrototypeSavedRecord>()).ToArray());
		}

		public int SchemaVersion { get; }
		public IReadOnlyList<PrototypeSavedRecord> Records { get; }
	}

	public sealed class PrototypeDiagnostic
	{
		public PrototypeDiagnostic(string kind, string detail, string? key = null)
		{
			Kind = kind;
			Detail = detail;
			Key = key;
		}

		public string Kind { get; }
		public string Detail { get; }
		public string? Key { get; }
	}
}
