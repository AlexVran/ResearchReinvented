using System.Globalization;
using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Domain.DefIndex;
using PeteTimesSix.ResearchReinvented.Domain.Rules;
using PeteTimesSix.ResearchReinvented.Domain.Selection;
using PeteTimesSix.ResearchReinvented.Domain.Shadow;
using PeteTimesSix.ResearchReinvented.Opportunities;
using Xunit;

namespace PeteTimesSix.ResearchReinvented.Tests;

public sealed class OpportunityShadowComparisonTests
{
	private static readonly DefIdentity Project = new("ResearchProjectDef", "ShadowProject");
	private static readonly DefIdentity Type = OpportunityTypeIds.Of("Analyse");
	private static readonly DefIdentity Category = new("ResearchOpportunityCategoryDef", "Analysis");

	[Fact]
	public void EqualSemanticOutputsMatchWithoutDifferences()
	{
		var spec = Spec("Subject");
		var result = Compare([spec], Selected([spec]));

		Assert.Equal(1, result.LegacyCount);
		Assert.Equal(1, result.NewCount);
		Assert.Equal(1, result.MatchedCount);
		Assert.Empty(result.Differences);
		Assert.False(result.HasUnexplainedDifferences);
	}

	[Fact]
	public void LegacyOnlyAndNewOnlyDifferencesRequireExplicitApprovals()
	{
		var legacy = Spec("Legacy"); var modern = Spec("Modern");
		var unexplained = Compare([legacy], Selected([modern]));
		var policy = new ShadowComparisonPolicy(
		[
			new(ShadowDifferenceKind.LegacyOnly, ShadowDifferenceClassification.MissingCompatibility, "Legacy-only analysis needs a compatibility rule.", subject: legacy.Requirement.CanonicalSubject),
			new(ShadowDifferenceKind.NewOnly, ShadowDifferenceClassification.IntendedImprovement, "The conservative replacement is approved.", subject: modern.Requirement.CanonicalSubject)
		]);
		var classified = Compare([legacy], Selected([modern]), policy: policy);

		Assert.Equal(2, unexplained.UnexplainedCount);
		Assert.Equal(0, classified.UnexplainedCount);
		Assert.Contains(classified.Differences, item => item.Classification == ShadowDifferenceClassification.MissingCompatibility);
		Assert.Contains(classified.Differences, item => item.Classification == ShadowDifferenceClassification.IntendedImprovement);
	}

	[Fact]
	public void MetadataDifferencesRemainVisibleForMatchingSemanticKeys()
	{
		var legacy = Spec("Subject", importance: 1f, freebie: true);
		var modern = Spec("Subject", importance: 2f, freebie: false);
		var result = Compare([legacy], Selected([modern]));

		var difference = Assert.Single(result.Differences);
		Assert.Equal(ShadowDifferenceKind.MetadataMismatch, difference.Kind);
		Assert.Equal(1, result.MatchedCount);
		Assert.Contains("importance=1", difference.Detail, StringComparison.Ordinal);
		Assert.Contains("freebie=False", difference.Detail, StringComparison.Ordinal);
	}

	[Fact]
	public void DuplicateAndLargeDifferencesAreSortedAndBoundedByteForByte()
	{
		var legacy = Enumerable.Range(0, 20).Select(index => Spec("Legacy" + index.ToString("D2"))).ToList();
		legacy.Add(legacy[0]);
		var selected = Selected([Spec("Modern")]);
		var policy = new ShadowComparisonPolicy(diagnosticLimit: 3);
		var originalCulture = CultureInfo.CurrentCulture;
		string first;
		string second;
		try
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
			first = Compare(legacy, selected, policy: policy).CanonicalSnapshot();
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
			second = Compare(legacy.AsEnumerable().Reverse(), selected, policy: policy).CanonicalSnapshot();
		}
		finally
		{
			CultureInfo.CurrentCulture = originalCulture;
		}

		var result = Compare(legacy, selected, policy: policy);
		Assert.Equal(first, second);
		Assert.Equal(3, result.Differences.Count);
		Assert.True(result.SuppressedDifferenceCount > 0);
		Assert.Contains(result.Differences, item => item.Kind == ShadowDifferenceKind.DuplicateLegacyKey);
	}

	[Fact]
	public void ProjectCategoryBudgetsAndSelectionRejectionsAreComparedWithoutMutation()
	{
		var one = Spec("One"); var two = Spec("Two");
		var legacyBudgets = new Dictionary<DefIdentity, float> { [Category] = 125f };
		var selected = Selected([one, two], categoryLimit: 1, projectBudget: 500f, categoryBudget: 100f);
		var result = Compare([one], selected, budgets: legacyBudgets, projectBudget: 600f);

		Assert.Equal(1, result.NewSelectionRejectionCount);
		Assert.Contains(result.Differences, item => item.Kind == ShadowDifferenceKind.ProjectBudgetMismatch);
		Assert.Contains(result.Differences, item => item.Kind == ShadowDifferenceKind.CategoryBudgetMismatch);
		Assert.Equal(500f, selected.ProjectBudget);
		Assert.Equal(100f, selected.CategoryBudgets[Category]);
	}

	[Fact]
	public void GeneratedButBoundedCandidatesAreDistinguishedFromMissingRules()
	{
		var selected = Spec("Selected");
		var bounded = Spec("Bounded");
		var missing = Spec("Missing");
		var policy = new ShadowComparisonPolicy(
		[
			new(ShadowDifferenceKind.NotSelected, ShadowDifferenceClassification.IntendedImprovement, "Bounded for relevance.")
		]);
		var result = Compare([selected, bounded, missing], Selected([selected]), generatedUniverse: [selected, bounded], policy: policy);

		Assert.Contains(result.Differences, item => item.Kind == ShadowDifferenceKind.NotSelected && item.Classification == ShadowDifferenceClassification.IntendedImprovement);
		Assert.Contains(result.Differences, item => item.Kind == ShadowDifferenceKind.LegacyOnly && item.Classification == ShadowDifferenceClassification.Unexplained);
		Assert.Equal(1, result.UnexplainedCount);
	}

	[Fact]
	public void MalformedGeneratedUniverseIsRejectedExplicitly()
	{
		Assert.Throws<ArgumentException>(() =>
			Compare([Spec("Legacy")], Selected([]), generatedUniverse: new OpportunitySpec[] { null! }));
	}

	[Fact]
	public void CurrentGameFactionsCanonicalizeToStaticAvailabilityFamilies()
	{
		var factionType = OpportunityTypeIds.Of("GainFactionKnowledge");
		OpportunitySpec Faction(string name) => new(Project, factionType, ResearchRelation.Direct,
			RequirementSpec.ForFaction(new DefIdentity("FactionDef", name)), 1f, false, false,
			[new GenerationReason(GenerationReasonKind.Faction, EvidenceSource.LegacyAdapter, Project)]);
		var legacy = new[] { Faction("Civil"), Faction("Rough") }
			.Select(ShadowComparisonCanonicalizer.StaticAvailability).ToArray();
		var modern = new OpportunitySpec(Project, factionType, ResearchRelation.Direct,
			RequirementSpec.ForFaction(DefIdentity.Synthetic("non-player-faction")), 1f, false, false,
			[new GenerationReason(GenerationReasonKind.Faction, EvidenceSource.ProjectDefinition, Project)]);
		var policy = new ShadowComparisonPolicy(
		[
			new(ShadowDifferenceKind.DuplicateLegacyKey, ShadowDifferenceClassification.IntendedImprovement, "One static family replaces current factions.")
		]);
		var result = Compare(legacy, Selected([modern]), policy: policy);

		Assert.Equal(1, result.MatchedCount);
		Assert.DoesNotContain(result.Differences, difference => difference.Kind is ShadowDifferenceKind.LegacyOnly or ShadowDifferenceKind.NewOnly);
		Assert.Equal(0, result.UnexplainedCount);
	}

	[Fact]
	public void ApprovalsCanBeScopedToProjectAndExplicitMetadataProvenance()
	{
		var legacy = Spec("Special", freebie: false);
		var modern = new OpportunitySpec(Project, Type, ResearchRelation.Direct, legacy.Requirement, 1f, false, true,
			[new GenerationReason(GenerationReasonKind.ExplicitMetadata, EvidenceSource.SpecialOpportunity, new DefIdentity("SpecialResearchOpportunityDef", "Special"))]);
		var approved = new ShadowComparisonPolicy(
		[
			new(ShadowDifferenceKind.MetadataMismatch, ShadowDifferenceClassification.IntendedImprovement,
				"Preserve explicit metadata.", project: Project, reasonKind: GenerationReasonKind.ExplicitMetadata)
		]);
		var wrongProject = new ShadowComparisonPolicy(
		[
			new(ShadowDifferenceKind.MetadataMismatch, ShadowDifferenceClassification.IntendedImprovement,
				"Wrong fixture.", project: new DefIdentity("ResearchProjectDef", "Other"), reasonKind: GenerationReasonKind.ExplicitMetadata)
		]);

		Assert.Equal(0, Compare([legacy], Selected([modern]), policy: approved).UnexplainedCount);
		Assert.Equal(1, Compare([legacy], Selected([modern]), policy: wrongProject).UnexplainedCount);
	}

	[Fact]
	public void SimilarAlternateFamiliesCompareAsOneSemanticTaskWithoutChangingSelectionKeys()
	{
		var a = new DefIdentity("ThingDef", "GeneratorA");
		var b = new DefIdentity("ThingDef", "GeneratorB");
		var source = new Source
		{
			Projects = [new(Project)],
			Things = [new(a), new(b)],
			AlternateLinks = [new AlternateLinkSnapshot(new DefIdentity("AlternateResearchSubjectsDef", "Generators"), a, b, AlternateSubjectMode.Similar, false)]
		};
		var index = ResearchDefIndex.Build(source);
		var legacy = new OpportunitySpec(Project, Type, ResearchRelation.Direct, RequirementSpec.ForThing(a, AlternateSubjectMode.Similar, [b]), 1f, false, false,
			[new GenerationReason(GenerationReasonKind.Unlock, EvidenceSource.LegacyAdapter, Project)]);
		var modern = new OpportunitySpec(Project, Type, ResearchRelation.Direct, RequirementSpec.ForThing(b, AlternateSubjectMode.Similar, [a]), 1f, false, false,
			[new GenerationReason(GenerationReasonKind.Unlock, EvidenceSource.ProjectDefinition, Project)]);
		var selected = Selected([modern]);
		var result = new OpportunityShadowComparer().Compare(Project, [legacy], selected,
			canonicalize: spec => OpportunitySelector.NormalizeForComparison(index, spec));

		Assert.NotEqual(legacy.Key, modern.Key);
		Assert.Equal(1, result.MatchedCount);
		Assert.Empty(result.Differences);
	}

	[Fact]
	public void Phase7PolicyApprovesOnlyTheReviewedAutobongAnalysisDifference()
	{
		var project = new DefIdentity("ResearchProjectDef", "MicroelectronicsBasics");
		OpportunitySpec DirectAnalysis(string subject) => new(project, Type, ResearchRelation.Direct,
			RequirementSpec.ForThing(new DefIdentity("ThingDef", subject)), 1f, false, false,
			[new GenerationReason(GenerationReasonKind.RequiredAnalysis, EvidenceSource.RequiredAnalysis, project)]);
		var approved = DirectAnalysis("Autobong");
		var unreviewed = DirectAnalysis("UnrelatedUnlock");

		var approvedResult = new OpportunityShadowComparer().Compare(
			project, [], Selected([approved], project: project), policy: ShadowComparisonPolicies.Phase7());
		var unreviewedResult = new OpportunityShadowComparer().Compare(
			project, [], Selected([unreviewed], project: project), policy: ShadowComparisonPolicies.Phase7());

		Assert.Equal(0, approvedResult.UnexplainedCount);
		Assert.Equal(ShadowDifferenceClassification.IntendedImprovement, Assert.Single(approvedResult.Differences).Classification);
		Assert.Equal(1, unreviewedResult.UnexplainedCount);
	}

	private static ShadowComparisonResult Compare(
		IEnumerable<OpportunitySpec> legacy,
		OpportunitySelectionResult selected,
		IEnumerable<OpportunitySpec>? generatedUniverse = null,
		IReadOnlyDictionary<DefIdentity, float>? budgets = null,
		float? projectBudget = null,
		ShadowComparisonPolicy? policy = null) =>
		new OpportunityShadowComparer().Compare(Project, legacy, selected, generatedUniverse, budgets, projectBudget, policy);

	private static OpportunitySelectionResult Selected(IEnumerable<OpportunitySpec> specs, int categoryLimit = 24, float projectBudget = 0f, float categoryBudget = 0f, DefIdentity? project = null)
	{
		var values = specs.ToArray();
		var sourceProject = project ?? Project;
		var source = new Source
		{
			Projects = [new(sourceProject)],
			Things = values.Where(spec => spec.Requirement.Kind == RequirementKind.Thing).Select(spec => new ThingDefSnapshot(spec.Requirement.CanonicalSubject))
		};
		var index = ResearchDefIndex.Build(source);
		var candidates = values.Select((spec, position) => new OpportunityCandidate("shadow-" + position.ToString("D2"), spec));
		var policy = new OpportunitySelectionPolicy(
			categories: [new(Type, ResearchRelation.Direct, Category)],
			categoryLimits: [new KeyValuePair<DefIdentity, int>(Category, categoryLimit)],
			categoryBudgets: [new KeyValuePair<DefIdentity, float>(Category, categoryBudget)],
			projectBudget: projectBudget,
			defaultRuleLimit: 100,
			diversityFamilyLimit: 100);
		return new OpportunitySelector().Select(index, new OpportunityRuleResult(candidates), policy);
	}

	private static OpportunitySpec Spec(string subject, float importance = 1f, bool freebie = true) =>
		new(Project, Type, ResearchRelation.Direct, RequirementSpec.ForThing(new DefIdentity("ThingDef", subject)), importance, false, freebie,
			[new GenerationReason(GenerationReasonKind.RequiredAnalysis, EvidenceSource.RequiredAnalysis, new DefIdentity("ThingDef", subject))]);

	private sealed class Source : IResearchDefSnapshotSource
	{
		public IEnumerable<ProjectDefSnapshot?> Projects { get; init; } = [];
		public IEnumerable<RecipeDefSnapshot?> Recipes { get; init; } = [];
		public IEnumerable<ThingDefSnapshot?> Things { get; init; } = [];
		public IEnumerable<TerrainDefSnapshot?> Terrains { get; init; } = [];
		public IEnumerable<SpecialOpportunitySnapshot?> SpecialOpportunities { get; init; } = [];
		public IEnumerable<AlternateLinkSnapshot?> AlternateLinks { get; init; } = [];
		public IEnumerable<OpportunityOverrideSnapshot?> OpportunityOverrides { get; init; } = [];
	}
}
