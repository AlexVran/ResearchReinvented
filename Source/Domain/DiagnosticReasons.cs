#nullable enable

using System;

namespace PeteTimesSix.ResearchReinvented.Domain
{
	public enum GenerationReasonKind
	{
		LegacyGenerator,
		ExplicitMetadata,
		Project,
		RequiredAnalysis,
		Unlock,
		Recipe,
		Ingredient,
		ConstructionCost,
		Fuel,
		Plant,
		Faction,
		Compatibility
	}

	public enum RejectionReasonKind
	{
		InvalidEvidence,
		InvalidRequirement,
		MissingDefinition,
		SuppressedByMetadata,
		WeakEvidence,
		DuplicateSemanticKey,
		CategoryLimit,
		RuleLimit,
		DiversityLimit,
		UnsupportedLegacyRequirement
	}

	public sealed class GenerationReason : IEquatable<GenerationReason>
	{
		public GenerationReason(
			GenerationReasonKind kind,
			EvidenceSource source,
			DefIdentity sourceDef,
			string? detail = null)
		{
			Kind = kind;
			Source = source;
			SourceDef = sourceDef ?? throw new ArgumentNullException(nameof(sourceDef));
			Detail = detail;
		}

		public GenerationReasonKind Kind { get; }

		public EvidenceSource Source { get; }

		public DefIdentity SourceDef { get; }

		public string? Detail { get; }

		public bool Equals(GenerationReason? other)
		{
			return other != null
				&& Kind == other.Kind
				&& Source == other.Source
				&& SourceDef == other.SourceDef
				&& string.Equals(Detail, other.Detail, StringComparison.Ordinal);
		}

		public override bool Equals(object? obj)
		{
			return Equals(obj as GenerationReason);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = (int)Kind;
				hashCode = (hashCode * 397) ^ (int)Source;
				hashCode = (hashCode * 397) ^ SourceDef.GetHashCode();
				hashCode = (hashCode * 397) ^ (Detail == null ? 0 : StringComparer.Ordinal.GetHashCode(Detail));
				return hashCode;
			}
		}
	}

	public sealed class RejectionReason : IEquatable<RejectionReason>
	{
		public RejectionReason(
			RejectionReasonKind kind,
			string detail,
			DefIdentity? sourceDef = null)
		{
			if (string.IsNullOrWhiteSpace(detail))
				throw new ArgumentException("A rejection reason needs a diagnostic detail.", nameof(detail));

			Kind = kind;
			Detail = detail;
			SourceDef = sourceDef;
		}

		public RejectionReasonKind Kind { get; }

		public string Detail { get; }

		public DefIdentity? SourceDef { get; }

		public bool Equals(RejectionReason? other)
		{
			return other != null
				&& Kind == other.Kind
				&& string.Equals(Detail, other.Detail, StringComparison.Ordinal)
				&& SourceDef == other.SourceDef;
		}

		public override bool Equals(object? obj)
		{
			return Equals(obj as RejectionReason);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = (int)Kind;
				hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Detail);
				hashCode = (hashCode * 397) ^ (SourceDef?.GetHashCode() ?? 0);
				return hashCode;
			}
		}
	}
}
