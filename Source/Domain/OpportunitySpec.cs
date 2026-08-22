#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PeteTimesSix.ResearchReinvented.Opportunities;

namespace PeteTimesSix.ResearchReinvented.Domain
{
	/// <summary>
	/// Immutable definition of an opportunity. Progress is deliberately owned by
	/// a separate state object and is not part of semantic identity.
	/// </summary>
	public sealed class OpportunitySpec : IEquatable<OpportunitySpec>
	{
		private readonly ReadOnlyCollection<GenerationReason> reasons;

		public OpportunitySpec(
			DefIdentity project,
			DefIdentity type,
			ResearchRelation relation,
			RequirementSpec requirement,
			float importance,
			bool rare,
			bool freebie,
			IEnumerable<GenerationReason>? reasons = null)
			: this(null, project, type, relation, requirement, importance, rare, freebie, reasons)
		{
		}

		public OpportunitySpec(
			OpportunityKey? key,
			DefIdentity project,
			DefIdentity type,
			ResearchRelation relation,
			RequirementSpec requirement,
			float importance,
			bool rare,
			bool freebie,
			IEnumerable<GenerationReason>? reasons = null)
		{
			if (float.IsNaN(importance) || float.IsInfinity(importance))
				throw new ArgumentOutOfRangeException(nameof(importance), "Opportunity importance must be finite.");

			Project = project ?? throw new ArgumentNullException(nameof(project));
			Type = type ?? throw new ArgumentNullException(nameof(type));
			Relation = relation;
			Requirement = requirement ?? throw new ArgumentNullException(nameof(requirement));
			Importance = importance;
			Rare = rare;
			Freebie = freebie;

			var expectedKey = OpportunityKey.Create(Project, Type, Relation, Requirement);
			if (key != null && key != expectedKey)
				throw new ArgumentException("The supplied opportunity key does not match the semantic fields.", nameof(key));
			Key = expectedKey;

			var reasonSnapshot = (reasons ?? Array.Empty<GenerationReason>())
				.Select(reason => reason ?? throw new ArgumentException("Generation reasons cannot contain null.", nameof(reasons)))
				.ToArray();
			this.reasons = Array.AsReadOnly(reasonSnapshot);
		}

		public OpportunityKey Key { get; }

		public DefIdentity Project { get; }

		public DefIdentity Type { get; }

		public ResearchRelation Relation { get; }

		public RequirementSpec Requirement { get; }

		public float Importance { get; }

		public bool Rare { get; }

		public bool Freebie { get; }

		public IReadOnlyList<GenerationReason> Reasons => reasons;

		public bool Equals(OpportunitySpec? other)
		{
			return other != null && Key == other.Key;
		}

		public override bool Equals(object? obj)
		{
			return Equals(obj as OpportunitySpec);
		}

		public override int GetHashCode()
		{
			return Key.GetHashCode();
		}
	}
}
