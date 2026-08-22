#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using PeteTimesSix.ResearchReinvented.Domain.DefIndex;
using PeteTimesSix.ResearchReinvented.Domain.Rules;
using PeteTimesSix.ResearchReinvented.Opportunities;

namespace PeteTimesSix.ResearchReinvented.Domain.Selection
{
	/// <summary>
	/// Immutable policy for the Phase 6 pure candidate-selection pass. Categories
	/// and their point budgets are input facts; this pass never recalculates them.
	/// </summary>
	public sealed class OpportunitySelectionPolicy
	{
		private static readonly DefIdentity FallbackCategory = DefIdentity.Synthetic("uncategorized");
		private readonly IReadOnlyDictionary<string, DefIdentity> categories;
		private readonly IReadOnlyDictionary<DefIdentity, int> categoryLimits;
		private readonly IReadOnlyDictionary<string, int> ruleLimits;
		private readonly IReadOnlyDictionary<DefIdentity, float> categoryBudgets;

		public OpportunitySelectionPolicy(
			IEnumerable<OpportunityCategoryAssignment>? categories = null,
			IEnumerable<KeyValuePair<DefIdentity, int>>? categoryLimits = null,
			IEnumerable<KeyValuePair<string, int>>? ruleLimits = null,
			IEnumerable<KeyValuePair<DefIdentity, float>>? categoryBudgets = null,
			float projectBudget = 0f,
			int defaultCategoryLimit = 24,
			int defaultRuleLimit = 8,
			int diversityFamilyLimit = 2,
			int diagnosticLimit = 128,
			int reasonLimit = 16)
		{
			if (defaultCategoryLimit < 1) throw new ArgumentOutOfRangeException(nameof(defaultCategoryLimit));
			if (defaultRuleLimit < 1) throw new ArgumentOutOfRangeException(nameof(defaultRuleLimit));
			if (diversityFamilyLimit < 1) throw new ArgumentOutOfRangeException(nameof(diversityFamilyLimit));
			if (diagnosticLimit < 1) throw new ArgumentOutOfRangeException(nameof(diagnosticLimit));
			if (reasonLimit < 1) throw new ArgumentOutOfRangeException(nameof(reasonLimit));
			this.categories = ReadOnly(categories, item => item.MapKey, item => item.Category);
			this.categoryLimits = ReadOnly(categoryLimits, item => item.Key, item => Positive(item.Value, nameof(categoryLimits)));
			this.ruleLimits = ReadOnly(ruleLimits, item => item.Key, item => Positive(item.Value, nameof(ruleLimits)), StringComparer.Ordinal);
			this.categoryBudgets = ReadOnly(categoryBudgets, item => item.Key, item => Finite(item.Value, nameof(categoryBudgets)));
			ProjectBudget = Finite(projectBudget, nameof(projectBudget));
			DefaultCategoryLimit = defaultCategoryLimit;
			DefaultRuleLimit = defaultRuleLimit;
			DiversityFamilyLimit = diversityFamilyLimit;
			DiagnosticLimit = diagnosticLimit;
			ReasonLimit = reasonLimit;
		}

		public int DefaultCategoryLimit { get; }
		public int DefaultRuleLimit { get; }
		public float ProjectBudget { get; }
		public int DiversityFamilyLimit { get; }
		public int DiagnosticLimit { get; }
		public int ReasonLimit { get; }
		public IReadOnlyDictionary<DefIdentity, float> CategoryBudgets => categoryBudgets;

		public DefIdentity CategoryFor(OpportunitySpec spec) =>
			categories.TryGetValue(OpportunityCategoryAssignment.CreateMapKey(spec.Type, spec.Relation), out var category) ? category : FallbackCategory;
		public int CategoryLimitFor(DefIdentity category) => categoryLimits.TryGetValue(category, out var value) ? value : DefaultCategoryLimit;
		public int RuleLimitFor(string ruleId) => ruleLimits.TryGetValue(ruleId, out var value) ? value : DefaultRuleLimit;

		private static int Positive(int value, string name) => value < 1 ? throw new ArgumentOutOfRangeException(name) : value;
		private static float Finite(float value, string name) => float.IsNaN(value) || float.IsInfinity(value) ? throw new ArgumentOutOfRangeException(name) : value;
		private static IReadOnlyDictionary<TKey, TValue> ReadOnly<TSource, TKey, TValue>(IEnumerable<TSource>? source, Func<TSource, TKey> key, Func<TSource, TValue> value, IEqualityComparer<TKey>? comparer = null) where TKey : notnull =>
			new ReadOnlyDictionary<TKey, TValue>((source ?? Array.Empty<TSource>()).ToDictionary(key, value, comparer));
	}

	public sealed class OpportunityCategoryAssignment
	{
		public OpportunityCategoryAssignment(DefIdentity type, ResearchRelation relation, DefIdentity category)
		{
			Type = type ?? throw new ArgumentNullException(nameof(type));
			Relation = relation;
			Category = category ?? throw new ArgumentNullException(nameof(category));
		}
		public DefIdentity Type { get; }
		public ResearchRelation Relation { get; }
		public DefIdentity Category { get; }
		internal string MapKey => CreateMapKey(Type, Relation);
		internal static string CreateMapKey(DefIdentity type, ResearchRelation relation) => type.CanonicalValue + "|" + relation.ToString();
	}

	public sealed class OpportunityScore
	{
		internal OpportunityScore(float evidence, float confidence, float relation, float feasibility, float importance)
		{
			Evidence = evidence; Confidence = confidence; Relation = relation; Feasibility = feasibility; Importance = importance;
			Total = evidence * confidence * relation * feasibility * importance;
		}
		public float Evidence { get; }
		public float Confidence { get; }
		public float Relation { get; }
		public float Feasibility { get; }
		public float Importance { get; }
		public float Total { get; }
	}

	public sealed class SelectedOpportunity
	{
		internal SelectedOpportunity(OpportunitySpec spec, DefIdentity category, IReadOnlyList<string> ruleIds, OpportunityScore score, int suppressedReasonCount)
		{ Spec = spec; Category = category; RuleIds = ruleIds; Score = score; SuppressedReasonCount = suppressedReasonCount; }
		public OpportunitySpec Spec { get; }
		public DefIdentity Category { get; }
		public IReadOnlyList<string> RuleIds { get; }
		public OpportunityScore Score { get; }
		public int SuppressedReasonCount { get; }
	}

	public sealed class OpportunitySelectionResult
	{
		internal OpportunitySelectionResult(IEnumerable<SelectedOpportunity> selected, IEnumerable<RejectionReason> rejections, IEnumerable<KeyValuePair<DefIdentity, float>> budgets, float projectBudget, int suppressedDiagnostics)
		{
			Selected = Array.AsReadOnly(selected.ToArray());
			Rejections = Array.AsReadOnly(rejections.ToArray());
			CategoryBudgets = new ReadOnlyDictionary<DefIdentity, float>(budgets.ToDictionary(item => item.Key, item => item.Value));
			ProjectBudget = projectBudget;
			SuppressedDiagnostics = suppressedDiagnostics;
		}
		public IReadOnlyList<SelectedOpportunity> Selected { get; }
		public IReadOnlyList<RejectionReason> Rejections { get; }
		public IReadOnlyDictionary<DefIdentity, float> CategoryBudgets { get; }
		public float ProjectBudget { get; }
		public int SuppressedDiagnostics { get; }

		public string CanonicalSnapshot()
		{
			var lines = new List<string> { "project-budget|" + Float(ProjectBudget) };
			lines.AddRange(CategoryBudgets.OrderBy(item => item.Key).Select(item => $"category-budget|{item.Key}|{Float(item.Value)}"));
			foreach (var item in Selected)
			{
				lines.Add($"selected|{item.Spec.Key}|{item.Category}|{Float(item.Score.Evidence)}|{Float(item.Score.Confidence)}|{Float(item.Score.Relation)}|{Float(item.Score.Feasibility)}|{Float(item.Score.Importance)}|{Float(item.Score.Total)}|{string.Join(",", item.RuleIds)}|{item.SuppressedReasonCount}");
				lines.AddRange(item.Spec.Reasons.Select(reason => $"reason|{item.Spec.Key}|{reason.Kind}|{reason.Source}|{reason.SourceDef}|{Encoded(reason.Detail)}"));
			}
			lines.AddRange(Rejections.Select(reason => $"rejection|{reason.Kind}|{reason.SourceDef}|{Encoded(reason.Detail)}"));
			lines.Add("suppressed-diagnostics|" + SuppressedDiagnostics.ToString(CultureInfo.InvariantCulture));
			return string.Join("\n", lines);
		}

		private static string Float(float value) => value.ToString("R", CultureInfo.InvariantCulture);
		private static string Encoded(string? value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
	}

	/// <summary>Normalizes, scores, deduplicates, bounds, and orders Phase 5 output without consulting game state.</summary>
	public sealed class OpportunitySelector
	{
		public OpportunitySelectionResult Select(ResearchDefIndex index, OpportunityRuleResult generated, OpportunitySelectionPolicy? policy = null)
		{
			if (index == null) throw new ArgumentNullException(nameof(index));
			if (generated == null) throw new ArgumentNullException(nameof(generated));
			policy ??= new OpportunitySelectionPolicy();
			var diagnostics = new List<RejectionReason>();
			foreach (var rejection in generated.Rejections)
			{
				if (rejection == null)
					diagnostics.Add(new RejectionReason(RejectionReasonKind.InvalidEvidence, "A rule returned a null rejection diagnostic."));
				else
					diagnostics.Add(rejection);
			}
			var normalized = new List<OpportunityCandidate>();
			foreach (var candidate in generated.Candidates)
			{
				if (candidate == null)
				{
					diagnostics.Add(new RejectionReason(RejectionReasonKind.InvalidEvidence, "A rule returned a null opportunity candidate."));
					continue;
				}
				normalized.Add(Normalize(index, candidate));
			}
			var merged = new List<MergedCandidate>();
			foreach (var group in normalized.GroupBy(candidate => candidate.Spec.Key).OrderBy(group => group.Key))
			{
				var candidates = group.OrderBy(candidate => candidate.RuleId, StringComparer.Ordinal).ThenByDescending(candidate => candidate.Spec.Importance).ToArray();
				var winner = candidates[0];
				var allReasons = candidates.SelectMany(candidate => candidate.Spec.Reasons).Distinct()
					.OrderBy(reason => reason.Kind == GenerationReasonKind.ExplicitMetadata ? 0 : 1)
					.ThenBy(reason => reason.Kind).ThenBy(reason => reason.Source).ThenBy(reason => reason.SourceDef).ThenBy(reason => reason.Detail, StringComparer.Ordinal).ToArray();
				var mergedRequirement = MergeRequirements(candidates.Select(candidate => candidate.Spec.Requirement));
				var mergedSpec = new OpportunitySpec(winner.Spec.Project, winner.Spec.Type, winner.Spec.Relation, mergedRequirement, candidates.Max(candidate => candidate.Spec.Importance), candidates.Any(candidate => candidate.Spec.Rare), candidates.All(candidate => candidate.Spec.Freebie), allReasons.Take(policy.ReasonLimit));
				merged.Add(new MergedCandidate(mergedSpec, candidates.Select(candidate => candidate.RuleId).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray(), allReasons.Length - Math.Min(allReasons.Length, policy.ReasonLimit), candidates.Max(candidate => candidate.EvidenceConfidence), candidates.Max(candidate => candidate.StaticFeasibility)));
				foreach (var discarded in candidates.Skip(1)) diagnostics.Add(new RejectionReason(RejectionReasonKind.DuplicateSemanticKey, $"Merged into {mergedSpec.Key}.", discarded.Spec.Requirement.CanonicalSubject));
			}

			var scored = merged.Select(candidate => new RankedCandidate(candidate, policy.CategoryFor(candidate.Spec), Score(candidate))).OrderByDescending(item => item.Score.Total).ThenBy(item => item.Candidate.Spec.Key).ThenBy(item => item.Candidate.RuleIds[0], StringComparer.Ordinal).ToArray();
			var selected = new List<SelectedOpportunity>();
			var byCategory = new Dictionary<DefIdentity, int>();
			var byRule = new Dictionary<string, int>(StringComparer.Ordinal);
			var byFamily = new Dictionary<DefIdentity, int>();
			foreach (var item in scored)
			{
				var subject = item.Candidate.Spec.Requirement.CanonicalSubject;
				var family = DiversityFamily(item.Candidate.Spec);
				if (byCategory.TryGetValue(item.Category, out var categoryCount) && categoryCount >= policy.CategoryLimitFor(item.Category)) { diagnostics.Add(new RejectionReason(RejectionReasonKind.CategoryLimit, $"Category {item.Category} reached its limit.", subject)); continue; }
				if (item.Candidate.RuleIds.Any(rule => byRule.TryGetValue(rule, out var ruleCount) && ruleCount >= policy.RuleLimitFor(rule))) { diagnostics.Add(new RejectionReason(RejectionReasonKind.RuleLimit, "A generating rule reached its limit.", subject)); continue; }
				if (byFamily.TryGetValue(family, out var familyCount) && familyCount >= policy.DiversityFamilyLimit) { diagnostics.Add(new RejectionReason(RejectionReasonKind.DiversityLimit, $"Evidence family {family} reached its diversity limit.", subject)); continue; }
				selected.Add(new SelectedOpportunity(item.Candidate.Spec, item.Category, Array.AsReadOnly(item.Candidate.RuleIds), item.Score, item.Candidate.SuppressedReasonCount));
				byCategory[item.Category] = categoryCount + 1; byFamily[family] = familyCount + 1;
				foreach (var rule in item.Candidate.RuleIds) byRule[rule] = byRule.TryGetValue(rule, out var count) ? count + 1 : 1;
			}
			var orderedDiagnostics = diagnostics.OrderBy(reason => reason.Kind).ThenBy(reason => reason.SourceDef).ThenBy(reason => reason.Detail, StringComparer.Ordinal).ToArray();
			var keptDiagnostics = orderedDiagnostics.Take(policy.DiagnosticLimit).ToArray();
			return new OpportunitySelectionResult(selected, keptDiagnostics, policy.CategoryBudgets.OrderBy(item => item.Key), policy.ProjectBudget, orderedDiagnostics.Length - keptDiagnostics.Length);
		}

		private static RequirementSpec MergeRequirements(IEnumerable<RequirementSpec> requirements)
		{
			var values = requirements.ToArray();
			var first = values[0];
			if (first.Kind != RequirementKind.Thing && first.Kind != RequirementKind.Terrain && first.Kind != RequirementKind.Recipe) return first;
			var mode = (AlternateSubjectMode)values.Max(value => (int)value.AlternateMode);
			var alternates = values.SelectMany(value => value.AlternateSubjects).Where(subject => subject != first.CanonicalSubject).Distinct().OrderBy(subject => subject).ToArray();
			return first.Kind == RequirementKind.Thing ? RequirementSpec.ForThing(first.CanonicalSubject, mode, alternates)
				: first.Kind == RequirementKind.Terrain ? RequirementSpec.ForTerrain(first.CanonicalSubject, mode, alternates)
				: RequirementSpec.ForRecipe(first.CanonicalSubject, mode, alternates);
		}

		private static DefIdentity DiversityFamily(OpportunitySpec spec)
		{
			return spec.Reasons.Where(reason => reason.Kind == GenerationReasonKind.Recipe || reason.Kind == GenerationReasonKind.Ingredient || reason.Kind == GenerationReasonKind.ConstructionCost || reason.Kind == GenerationReasonKind.Fuel || reason.Kind == GenerationReasonKind.Plant)
				.Select(reason => reason.SourceDef).OrderBy(source => source).FirstOrDefault() ?? spec.Requirement.CanonicalSubject;
		}

		private static OpportunityCandidate Normalize(ResearchDefIndex index, OpportunityCandidate candidate)
		{
			var spec = candidate.Spec; var requirement = spec.Requirement;
			if (requirement.Kind != RequirementKind.Thing && requirement.Kind != RequirementKind.Terrain && requirement.Kind != RequirementKind.Recipe) return candidate;
			var group = index.AlternateGroupFor(requirement.CanonicalSubject, AlternateSubjectMode.Equivalent);
			if (group == null) return candidate;
			var canonical = group.Members[0];
			var alternates = requirement.AlternateSubjects.Concat(group.Members).Where(subject => subject != canonical).Distinct().OrderBy(subject => subject).ToArray();
			var mode = requirement.AlternateMode == AlternateSubjectMode.None ? AlternateSubjectMode.Equivalent : requirement.AlternateMode;
			var normalized = requirement.Kind == RequirementKind.Thing ? RequirementSpec.ForThing(canonical, mode, alternates)
				: requirement.Kind == RequirementKind.Terrain ? RequirementSpec.ForTerrain(canonical, mode, alternates)
				: RequirementSpec.ForRecipe(canonical, mode, alternates);
			return new OpportunityCandidate(candidate.RuleId, new OpportunitySpec(spec.Project, spec.Type, spec.Relation, normalized, spec.Importance, spec.Rare, spec.Freebie, spec.Reasons), candidate.EvidenceConfidence, candidate.StaticFeasibility);
		}

		private static OpportunityScore Score(MergedCandidate candidate)
		{
			var spec = candidate.Spec;
			var evidence = spec.Reasons.Count == 0 ? .25f : spec.Reasons.Max(reason => reason.Kind == GenerationReasonKind.ExplicitMetadata ? 1f : reason.Kind == GenerationReasonKind.RequiredAnalysis ? .95f : reason.Kind == GenerationReasonKind.Project ? .85f : reason.Kind == GenerationReasonKind.Unlock || reason.Kind == GenerationReasonKind.Recipe ? .75f : .55f);
			var relation = spec.Relation == ResearchRelation.Direct ? 1f : spec.Relation == ResearchRelation.Ancestor ? .8f : .7f;
			var requirementFeasibility = spec.Requirement.Kind == RequirementKind.None || spec.Requirement.Kind == RequirementKind.Schematic ? .8f : spec.Requirement.Kind == RequirementKind.Faction || spec.Requirement.Kind == RequirementKind.FactionlessPawn ? .6f : 1f;
			var feasibility = requirementFeasibility * candidate.StaticFeasibility;
			var importance = Math.Max(.1f, Math.Min(10f, spec.Importance));
			return new OpportunityScore(evidence, candidate.EvidenceConfidence, relation, feasibility, importance);
		}

		private sealed class MergedCandidate { internal MergedCandidate(OpportunitySpec spec, string[] ruleIds, int suppressedReasonCount, float evidenceConfidence, float staticFeasibility) { Spec = spec; RuleIds = ruleIds; SuppressedReasonCount = suppressedReasonCount; EvidenceConfidence = evidenceConfidence; StaticFeasibility = staticFeasibility; } internal OpportunitySpec Spec { get; } internal string[] RuleIds { get; } internal int SuppressedReasonCount { get; } internal float EvidenceConfidence { get; } internal float StaticFeasibility { get; } }
		private sealed class RankedCandidate { internal RankedCandidate(MergedCandidate candidate, DefIdentity category, OpportunityScore score) { Candidate = candidate; Category = category; Score = score; } internal MergedCandidate Candidate { get; } internal DefIdentity Category { get; } internal OpportunityScore Score { get; } }
	}
}
