#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using PeteTimesSix.ResearchReinvented.Domain.Rules;
using PeteTimesSix.ResearchReinvented.Domain.Selection;
using PeteTimesSix.ResearchReinvented.Opportunities;

namespace PeteTimesSix.ResearchReinvented.Domain.Shadow
{
	public static class ShadowComparisonCanonicalizer
	{
		public static OpportunitySpec StaticAvailability(OpportunitySpec spec)
		{
			if (spec == null) throw new ArgumentNullException(nameof(spec));
			if (spec.Requirement.Kind != RequirementKind.Faction)
				return spec;
			DefIdentity? canonicalSubject = null;
			if (spec.Type == OpportunityTypeIds.Of("Brainstorming"))
				canonicalSubject = DefIdentity.Synthetic("player-faction");
			else if (spec.Type == OpportunityTypeIds.Of("GainFactionKnowledge"))
				canonicalSubject = DefIdentity.Synthetic("non-player-faction");
			return canonicalSubject == null
				? spec
				: new OpportunitySpec(spec.Project, spec.Type, spec.Relation, RequirementSpec.ForFaction(canonicalSubject), spec.Importance, spec.Rare, spec.Freebie, spec.Reasons);
		}
	}

	public enum ShadowDifferenceKind
	{
		LegacyOnly,
		NotSelected,
		NewOnly,
		MetadataMismatch,
		DuplicateLegacyKey,
		DuplicateNewKey,
		ProjectBudgetMismatch,
		CategoryBudgetMismatch
	}

	public enum ShadowDifferenceClassification
	{
		Unexplained,
		IntendedImprovement,
		MissingCompatibility,
		Bug
	}

	public sealed class ShadowDifferenceApproval
	{
		public ShadowDifferenceApproval(
			ShadowDifferenceKind kind,
			ShadowDifferenceClassification classification,
			string explanation,
			DefIdentity? project = null,
			DefIdentity? opportunityType = null,
			DefIdentity? subject = null,
			ResearchRelation? relation = null,
			GenerationReasonKind? reasonKind = null)
		{
			if (classification == ShadowDifferenceClassification.Unexplained)
				throw new ArgumentException("An approval must classify the difference.", nameof(classification));
			if (string.IsNullOrWhiteSpace(explanation))
				throw new ArgumentException("An approval needs an explanation.", nameof(explanation));
			Kind = kind;
			Classification = classification;
			Explanation = explanation;
			Project = project;
			OpportunityType = opportunityType;
			Subject = subject;
			Relation = relation;
			ReasonKind = reasonKind;
		}

		public ShadowDifferenceKind Kind { get; }
		public ShadowDifferenceClassification Classification { get; }
		public string Explanation { get; }
		public DefIdentity? Project { get; }
		public DefIdentity? OpportunityType { get; }
		public DefIdentity? Subject { get; }
		public ResearchRelation? Relation { get; }
		public GenerationReasonKind? ReasonKind { get; }

		internal bool Matches(ShadowDifference difference) =>
			Kind == difference.Kind
			&& (Project == null || Project == difference.Project)
			&& (OpportunityType == null || OpportunityType == difference.OpportunityType)
			&& (Subject == null || Subject == difference.Subject)
			&& (!Relation.HasValue || Relation == difference.Relation)
			&& (!ReasonKind.HasValue || difference.ReasonKinds.Contains(ReasonKind.Value));
	}

	public sealed class ShadowComparisonPolicy
	{
		private readonly IReadOnlyList<ShadowDifferenceApproval> approvals;

		public ShadowComparisonPolicy(IEnumerable<ShadowDifferenceApproval>? approvals = null, int diagnosticLimit = 64)
		{
			if (diagnosticLimit < 1) throw new ArgumentOutOfRangeException(nameof(diagnosticLimit));
			this.approvals = Array.AsReadOnly((approvals ?? Array.Empty<ShadowDifferenceApproval>())
				.Select(approval => approval ?? throw new ArgumentException("Approvals cannot contain null.", nameof(approvals)))
				.ToArray());
			DiagnosticLimit = diagnosticLimit;
		}

		public IReadOnlyList<ShadowDifferenceApproval> Approvals => approvals;
		public int DiagnosticLimit { get; }
	}

	public static class ShadowComparisonPolicies
	{
		public static ShadowComparisonPolicy Phase7(int diagnosticLimit = 64) => new ShadowComparisonPolicy(new[]
		{
			new ShadowDifferenceApproval(
				ShadowDifferenceKind.DuplicateLegacyKey,
				ShadowDifferenceClassification.IntendedImprovement,
				"Phase 6 intentionally merges legacy duplicate semantic keys."),
			new ShadowDifferenceApproval(
				ShadowDifferenceKind.NotSelected,
				ShadowDifferenceClassification.IntendedImprovement,
				"Phase 6 intentionally bounds and diversifies generated candidates."),
			new ShadowDifferenceApproval(
				ShadowDifferenceKind.MetadataMismatch,
				ShadowDifferenceClassification.IntendedImprovement,
				"Per-faction importance is dynamic availability metadata and is intentionally deferred.",
				opportunityType: OpportunityTypeIds.Of("GainFactionKnowledge")),
			new ShadowDifferenceApproval(
				ShadowDifferenceKind.MetadataMismatch,
				ShadowDifferenceClassification.IntendedImprovement,
				"Player-faction importance is dynamic availability metadata and is intentionally deferred.",
				opportunityType: OpportunityTypeIds.Of("Brainstorming")),
			new ShadowDifferenceApproval(
				ShadowDifferenceKind.MetadataMismatch,
				ShadowDifferenceClassification.IntendedImprovement,
				"Explicit special metadata is preserved instead of being dropped by legacy type inference.",
				reasonKind: GenerationReasonKind.ExplicitMetadata),
			new ShadowDifferenceApproval(
				ShadowDifferenceKind.NewOnly,
				ShadowDifferenceClassification.IntendedImprovement,
				"Descendant evidence deliberately supports reverse-engineering later applications while researching their prerequisite.",
				relation: ResearchRelation.Descendant),
			new ShadowDifferenceApproval(
				ShadowDifferenceKind.NewOnly,
				ShadowDifferenceClassification.IntendedImprovement,
				"The immutable evidence graph retains relevant ancestor products, facilities, materials, and terrain that legacy traversal omitted.",
				relation: ResearchRelation.Ancestor),
			new ShadowDifferenceApproval(
				ShadowDifferenceKind.NewOnly,
				ShadowDifferenceClassification.IntendedImprovement,
				"The immutable evidence graph retains an explicit direct unlock that legacy traversal omitted.",
				relation: ResearchRelation.Direct,
				reasonKind: GenerationReasonKind.Unlock),
			BroadFilterApproval("AnalyseIngredients"),
			BroadFilterApproval("AnalyseIngredientsFood"),
			BroadFilterApproval("AnalyseFuel"),
			BroadFilterApproval("AnalyseFuelDrug"),
			BroadFilterApproval("AnalyseFuelFood"),
			BroadFilterApproval("AnalyseFuelFlammable"),
			BroadFilterApproval("AnalyseMedicine"),
			ApprovedNew(
				"MicroelectronicsBasics",
				"Analyse",
				"ThingDef",
				"Autobong",
				"The new evidence graph retains direct analysis of the explicitly unlocked autobong that legacy traversal omitted."),
			ApprovedNew(
				"MultiAnalyzer",
				"PrototypeProduction",
				"RecipeDef",
				"Make_RR_FieldResearchKitMultiAnalyzer",
				"The new evidence graph retains the directly gated multi-analyzer field-kit recipe that legacy traversal omitted."),
			ApprovedNew(
				"MultiAnalyzer",
				"PrototypeProduction",
				"RecipeDef",
				"Make_RR_FieldResearchKitRemote",
				"The new evidence graph retains the directly gated remote field-kit recipe that legacy traversal omitted.")
		}, diagnosticLimit);

		private static ShadowDifferenceApproval BroadFilterApproval(string opportunityType) => new ShadowDifferenceApproval(
			ShadowDifferenceKind.LegacyOnly,
			ShadowDifferenceClassification.IntendedImprovement,
			"The legacy generator expanded a broad filter into incidental subjects; the static rules intentionally retain only explicit evidence.",
			opportunityType: OpportunityTypeIds.Of(opportunityType));

		private static ShadowDifferenceApproval ApprovedNew(string project, string opportunityType, string subjectType, string subject, string explanation) =>
			new ShadowDifferenceApproval(
				ShadowDifferenceKind.NewOnly,
				ShadowDifferenceClassification.IntendedImprovement,
				explanation,
				project: new DefIdentity("ResearchProjectDef", project),
				opportunityType: OpportunityTypeIds.Of(opportunityType),
				subject: new DefIdentity(subjectType, subject),
				relation: ResearchRelation.Direct);
	}

	public sealed class ShadowDifference
	{
		internal ShadowDifference(
			ShadowDifferenceKind kind,
			ShadowDifferenceClassification classification,
			string detail,
			DefIdentity? project,
			OpportunityKey? key,
			DefIdentity? opportunityType,
			DefIdentity? subject,
			ResearchRelation? relation,
			IEnumerable<GenerationReasonKind>? reasonKinds)
		{
			Kind = kind;
			Classification = classification;
			Detail = detail;
			Project = project;
			Key = key;
			OpportunityType = opportunityType;
			Subject = subject;
			Relation = relation;
			ReasonKinds = Array.AsReadOnly((reasonKinds ?? Array.Empty<GenerationReasonKind>()).Distinct().OrderBy(kind => kind).ToArray());
		}

		public ShadowDifferenceKind Kind { get; }
		public ShadowDifferenceClassification Classification { get; }
		public string Detail { get; }
		public DefIdentity? Project { get; }
		public OpportunityKey? Key { get; }
		public DefIdentity? OpportunityType { get; }
		public DefIdentity? Subject { get; }
		public ResearchRelation? Relation { get; }
		public IReadOnlyList<GenerationReasonKind> ReasonKinds { get; }
	}

	public sealed class ShadowComparisonResult
	{
		internal ShadowComparisonResult(
			DefIdentity project,
			int legacyCount,
			int newCount,
			int matchedCount,
			int newSelectionRejectionCount,
			IEnumerable<ShadowDifference> differences,
			int totalDifferenceCount,
			int unexplainedCount)
		{
			Project = project;
			LegacyCount = legacyCount;
			NewCount = newCount;
			MatchedCount = matchedCount;
			NewSelectionRejectionCount = newSelectionRejectionCount;
			Differences = Array.AsReadOnly(differences.ToArray());
			TotalDifferenceCount = totalDifferenceCount;
			SuppressedDifferenceCount = totalDifferenceCount - Differences.Count;
			UnexplainedCount = unexplainedCount;
		}

		public DefIdentity Project { get; }
		public int LegacyCount { get; }
		public int NewCount { get; }
		public int MatchedCount { get; }
		public int NewSelectionRejectionCount { get; }
		public IReadOnlyList<ShadowDifference> Differences { get; }
		public int TotalDifferenceCount { get; }
		public int SuppressedDifferenceCount { get; }
		public int UnexplainedCount { get; }
		public bool HasUnexplainedDifferences => UnexplainedCount != 0;

		public string CanonicalSnapshot()
		{
			var lines = new List<string>
			{
				$"summary|{Project}|{LegacyCount}|{NewCount}|{MatchedCount}|{NewSelectionRejectionCount}|{TotalDifferenceCount}|{SuppressedDifferenceCount}|{UnexplainedCount}"
			};
			lines.AddRange(Differences.Select(difference =>
				$"difference|{difference.Kind}|{difference.Classification}|{difference.Project}|{difference.Key}|{difference.OpportunityType}|{difference.Subject}|{difference.Relation}|{string.Join(",", difference.ReasonKinds)}|{Encoded(difference.Detail)}"));
			return string.Join("\n", lines);
		}

		private static string Encoded(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
	}

	public sealed class OpportunityShadowComparer
	{
		public ShadowComparisonResult Compare(
			DefIdentity project,
			IEnumerable<OpportunitySpec> legacy,
			OpportunitySelectionResult selected,
			IEnumerable<OpportunitySpec>? generatedUniverse = null,
			IReadOnlyDictionary<DefIdentity, float>? legacyCategoryBudgets = null,
			float? legacyProjectBudget = null,
			ShadowComparisonPolicy? policy = null,
			Func<OpportunitySpec, OpportunitySpec>? canonicalize = null)
		{
			if (project == null) throw new ArgumentNullException(nameof(project));
			if (legacy == null) throw new ArgumentNullException(nameof(legacy));
			if (selected == null) throw new ArgumentNullException(nameof(selected));
			policy ??= new ShadowComparisonPolicy();
			canonicalize ??= spec => spec;

			var legacyValues = legacy.Select(spec => spec ?? throw new ArgumentException("Legacy specifications cannot contain null.", nameof(legacy))).Select(canonicalize).ToArray();
			var newValues = selected.Selected.Select(item => canonicalize(item.Spec)).ToArray();
			var generatedKeys = (generatedUniverse ?? newValues)
				.Select(spec => spec ?? throw new ArgumentException("Generated specifications cannot contain null.", nameof(generatedUniverse)))
				.Select(canonicalize)
				.Select(spec => spec.Key).ToHashSet();
			var raw = new List<RawDifference>();
			var legacyGroups = legacyValues.GroupBy(spec => spec.Key).OrderBy(group => group.Key).ToArray();
			var newGroups = newValues.GroupBy(spec => spec.Key).OrderBy(group => group.Key).ToArray();
			foreach (var group in legacyGroups.Where(group => group.Count() > 1))
				raw.Add(ForSpec(ShadowDifferenceKind.DuplicateLegacyKey, group.First(), $"Legacy output contains {group.Count()} entries with the same semantic key."));
			foreach (var group in newGroups.Where(group => group.Count() > 1))
				raw.Add(ForSpec(ShadowDifferenceKind.DuplicateNewKey, group.First(), $"New output contains {group.Count()} entries with the same semantic key."));

			var legacyByKey = legacyGroups.ToDictionary(group => group.Key, group => group.First());
			var newByKey = newGroups.ToDictionary(group => group.Key, group => group.First());
			foreach (var item in legacyByKey)
			{
				if (!newByKey.TryGetValue(item.Key, out var newSpec))
					raw.Add(ForSpec(
						generatedKeys.Contains(item.Key) ? ShadowDifferenceKind.NotSelected : ShadowDifferenceKind.LegacyOnly,
						item.Value,
						generatedKeys.Contains(item.Key)
							? "The new generator produced this semantic opportunity, but deterministic selection omitted it."
							: "Legacy generated this semantic opportunity; the new rules did not generate it."));
				else if (!MetadataEqual(item.Value, newSpec))
					raw.Add(ForSpec(ShadowDifferenceKind.MetadataMismatch, newSpec, $"Legacy metadata [{Metadata(item.Value)}] differs from new metadata [{Metadata(newSpec)}]."));
			}
			foreach (var item in newByKey.Where(item => !legacyByKey.ContainsKey(item.Key)))
				raw.Add(ForSpec(ShadowDifferenceKind.NewOnly, item.Value, "The new generator selected this semantic opportunity; legacy did not generate it."));

			if (legacyProjectBudget.HasValue && legacyProjectBudget.Value != selected.ProjectBudget)
				raw.Add(new RawDifference(ShadowDifferenceKind.ProjectBudgetMismatch, $"Legacy project budget {Float(legacyProjectBudget.Value)} differs from new budget {Float(selected.ProjectBudget)}.", project: project));
			foreach (var category in (legacyCategoryBudgets ?? EmptyBudgets()).Keys.Union(selected.CategoryBudgets.Keys).OrderBy(identity => identity))
			{
				var legacyValue = ValueFor(legacyCategoryBudgets, category);
				var newValue = ValueFor(selected.CategoryBudgets, category);
				if (legacyValue != newValue)
					raw.Add(new RawDifference(ShadowDifferenceKind.CategoryBudgetMismatch, $"Category {category} legacy budget {Float(legacyValue)} differs from new budget {Float(newValue)}.", project: project, subject: category));
			}

			var classified = raw.Select(item => Classify(item, policy)).OrderBy(item => DiagnosticPriority(item.Kind)).ThenBy(item => item.Kind).ThenBy(item => item.Key).ThenBy(item => item.Subject).ThenBy(item => item.Detail, StringComparer.Ordinal).ToArray();
			return new ShadowComparisonResult(
				project,
				legacyValues.Length,
				newValues.Length,
				legacyByKey.Keys.Intersect(newByKey.Keys).Count(),
				selected.Rejections.Count + selected.SuppressedDiagnostics,
				classified.Take(policy.DiagnosticLimit),
				classified.Length,
				classified.Count(item => item.Classification == ShadowDifferenceClassification.Unexplained));
		}

		private static ShadowDifference Classify(RawDifference difference, ShadowComparisonPolicy policy)
		{
			var provisional = difference.ToDifference(ShadowDifferenceClassification.Unexplained, difference.Detail);
			var approval = policy.Approvals.FirstOrDefault(candidate => candidate.Matches(provisional));
			return approval == null
				? provisional
				: difference.ToDifference(approval.Classification, approval.Explanation + " " + difference.Detail);
		}

		private static int DiagnosticPriority(ShadowDifferenceKind kind)
		{
			switch (kind)
			{
				case ShadowDifferenceKind.DuplicateNewKey: return 0;
				case ShadowDifferenceKind.ProjectBudgetMismatch:
				case ShadowDifferenceKind.CategoryBudgetMismatch: return 1;
				case ShadowDifferenceKind.MetadataMismatch: return 2;
				case ShadowDifferenceKind.DuplicateLegacyKey: return 3;
				default: return 4;
			}
		}

		private static bool MetadataEqual(OpportunitySpec left, OpportunitySpec right) =>
			left.Importance == right.Importance
			&& left.Rare == right.Rare
			&& left.Freebie == right.Freebie
			&& left.Requirement.AlternateMode == right.Requirement.AlternateMode
			&& left.Requirement.AlternateSubjects.SequenceEqual(right.Requirement.AlternateSubjects);

		private static string Metadata(OpportunitySpec spec) =>
			$"importance={Float(spec.Importance)},rare={spec.Rare},freebie={spec.Freebie},alternateMode={spec.Requirement.AlternateMode},alternates={string.Join(",", spec.Requirement.AlternateSubjects)}";

		private static RawDifference ForSpec(ShadowDifferenceKind kind, OpportunitySpec spec, string detail) =>
			new RawDifference(kind, detail, spec.Project, spec.Key, spec.Type, spec.Requirement.CanonicalSubject, spec.Relation, spec.Reasons.Select(reason => reason.Kind));

		private static float ValueFor(IReadOnlyDictionary<DefIdentity, float>? values, DefIdentity key) => values != null && values.TryGetValue(key, out var value) ? value : 0f;
		private static IReadOnlyDictionary<DefIdentity, float> EmptyBudgets() => new ReadOnlyDictionary<DefIdentity, float>(new Dictionary<DefIdentity, float>());
		private static string Float(float value) => value.ToString("R", CultureInfo.InvariantCulture);

		private sealed class RawDifference
		{
			internal RawDifference(ShadowDifferenceKind kind, string detail, DefIdentity? project = null, OpportunityKey? key = null, DefIdentity? opportunityType = null, DefIdentity? subject = null, ResearchRelation? relation = null, IEnumerable<GenerationReasonKind>? reasonKinds = null)
			{ Kind = kind; Detail = detail; Project = project; Key = key; OpportunityType = opportunityType; Subject = subject; Relation = relation; ReasonKinds = reasonKinds?.ToArray() ?? Array.Empty<GenerationReasonKind>(); }
			internal ShadowDifferenceKind Kind { get; }
			internal string Detail { get; }
			internal DefIdentity? Project { get; }
			internal OpportunityKey? Key { get; }
			internal DefIdentity? OpportunityType { get; }
			internal DefIdentity? Subject { get; }
			internal ResearchRelation? Relation { get; }
			internal IReadOnlyList<GenerationReasonKind> ReasonKinds { get; }
			internal ShadowDifference ToDifference(ShadowDifferenceClassification classification, string detail) => new ShadowDifference(Kind, classification, detail, Project, Key, OpportunityType, Subject, Relation, ReasonKinds);
		}
	}
}
