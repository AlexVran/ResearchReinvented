#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PeteTimesSix.ResearchReinvented.Domain.State;
using PeteTimesSix.ResearchReinvented.Opportunities;

namespace PeteTimesSix.ResearchReinvented.Domain.Presentation
{
	/// <summary>
	/// Detached, immutable opportunity state for presentation consumers. It
	/// deliberately contains no saved-state object or mutable progress owner.
	/// </summary>
	public sealed class OpportunityReadModel
	{
		public OpportunityReadModel(OpportunitySpecificationState specification, float progress)
		{
			if (specification == null) throw new ArgumentNullException(nameof(specification));
			if (float.IsNaN(progress) || float.IsInfinity(progress) || progress < 0f)
				throw new ArgumentOutOfRangeException(nameof(progress));
			Key = specification.Spec.Key;
			Project = specification.Spec.Project;
			Type = specification.Spec.Type;
			Category = specification.Category;
			Relation = specification.Spec.Relation;
			Requirement = specification.Spec.Requirement;
			Importance = specification.Spec.Importance;
			Rare = specification.Spec.Rare;
			Freebie = specification.Spec.Freebie;
			Progress = progress;
			MaximumProgress = specification.MaximumProgress;
			StateKind = specification.StateKind;
			Reasons = new ReadOnlyCollection<GenerationReason>(specification.Spec.Reasons.ToArray());
		}

		public OpportunityKey Key { get; }
		public DefIdentity Project { get; }
		public DefIdentity Type { get; }
		public DefIdentity Category { get; }
		public ResearchRelation Relation { get; }
		public RequirementSpec Requirement { get; }
		public float Importance { get; }
		public bool Rare { get; }
		public bool Freebie { get; }
		public float Progress { get; }
		public float MaximumProgress { get; }
		public PreservedOpportunityStateKind StateKind { get; }
		public IReadOnlyList<GenerationReason> Reasons { get; }
		public float ProgressFraction => MaximumProgress <= 0f ? (Progress > 0f ? 1f : 0f) : Progress / MaximumProgress;
		public bool IsFinished => MaximumProgress <= 0f ? Progress > 0f : Progress >= MaximumProgress;
	}

	public sealed class OpportunityCategoryReadModel
	{
		public OpportunityCategoryReadModel(
			DefIdentity project,
			DefIdentity category,
			float progress,
			float budget,
			bool hasBudget,
			IEnumerable<OpportunityReadModel> opportunities)
		{
			Project = project ?? throw new ArgumentNullException(nameof(project));
			Category = category ?? throw new ArgumentNullException(nameof(category));
			if (!FiniteNonNegative(progress)) throw new ArgumentOutOfRangeException(nameof(progress));
			if (!FiniteNonNegative(budget)) throw new ArgumentOutOfRangeException(nameof(budget));
			Progress = progress;
			Budget = budget;
			HasBudget = hasBudget;
			Opportunities = new ReadOnlyCollection<OpportunityReadModel>((opportunities ?? Array.Empty<OpportunityReadModel>())
				.OrderBy(item => item.Key)
				.ToArray());
		}

		public DefIdentity Project { get; }
		public DefIdentity Category { get; }
		public float Progress { get; }
		public float Budget { get; }
		public bool HasBudget { get; }
		public IReadOnlyList<OpportunityReadModel> Opportunities { get; }

		private static bool FiniteNonNegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
	}

	public sealed class OpportunityProjectReadModel
	{
		public OpportunityProjectReadModel(DefIdentity project, IEnumerable<OpportunityCategoryReadModel> categories)
		{
			Project = project ?? throw new ArgumentNullException(nameof(project));
			Categories = new ReadOnlyCollection<OpportunityCategoryReadModel>((categories ?? Array.Empty<OpportunityCategoryReadModel>())
				.OrderBy(item => item.Category)
				.ToArray());
		}

		public DefIdentity Project { get; }
		public IReadOnlyList<OpportunityCategoryReadModel> Categories { get; }
	}
}
