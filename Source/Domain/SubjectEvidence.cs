#nullable enable

using System;
using PeteTimesSix.ResearchReinvented.Opportunities;

namespace PeteTimesSix.ResearchReinvented.Domain
{
	public enum SubjectRole
	{
		Project,
		AnalysisRequirement,
		Unlock,
		Recipe,
		Product,
		Ingredient,
		ProductionFacility,
		CostMaterial,
		Plant,
		Fuel,
		Terrain,
		Techprint,
		Faction,
		Schematic,
		Special
	}

	public enum EvidenceSource
	{
		ProjectDefinition,
		Prerequisite,
		HiddenPrerequisite,
		RequiredAnalysis,
		RecipeDefinition,
		ThingDefinition,
		TerrainDefinition,
		CompProperties,
		SpecialOpportunity,
		ModExtension,
		Compatibility,
		LegacyAdapter
	}

	public sealed class SubjectEvidence : IEquatable<SubjectEvidence>
	{
		public SubjectEvidence(
			DefIdentity project,
			DefIdentity subject,
			ResearchRelation relation,
			SubjectRole role,
			EvidenceSource source,
			DefIdentity sourceDef,
			float confidence)
		{
			if (float.IsNaN(confidence) || float.IsInfinity(confidence) || confidence < 0f || confidence > 1f)
				throw new ArgumentOutOfRangeException(nameof(confidence), "Evidence confidence must be between zero and one.");

			Project = project ?? throw new ArgumentNullException(nameof(project));
			Subject = subject ?? throw new ArgumentNullException(nameof(subject));
			Relation = relation;
			Role = role;
			Source = source;
			SourceDef = sourceDef ?? throw new ArgumentNullException(nameof(sourceDef));
			Confidence = confidence;
		}

		public DefIdentity Project { get; }

		public DefIdentity Subject { get; }

		public ResearchRelation Relation { get; }

		public SubjectRole Role { get; }

		public EvidenceSource Source { get; }

		public DefIdentity SourceDef { get; }

		public float Confidence { get; }

		public bool Equals(SubjectEvidence? other)
		{
			return other != null
				&& Project == other.Project
				&& Subject == other.Subject
				&& Relation == other.Relation
				&& Role == other.Role
				&& Source == other.Source
				&& SourceDef == other.SourceDef
				&& Confidence.Equals(other.Confidence);
		}

		public override bool Equals(object? obj)
		{
			return Equals(obj as SubjectEvidence);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = Project.GetHashCode();
				hashCode = (hashCode * 397) ^ Subject.GetHashCode();
				hashCode = (hashCode * 397) ^ (int)Relation;
				hashCode = (hashCode * 397) ^ (int)Role;
				hashCode = (hashCode * 397) ^ (int)Source;
				hashCode = (hashCode * 397) ^ SourceDef.GetHashCode();
				hashCode = (hashCode * 397) ^ Confidence.GetHashCode();
				return hashCode;
			}
		}
	}
}
