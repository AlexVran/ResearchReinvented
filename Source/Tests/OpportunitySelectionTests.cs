using System.Globalization;
using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Domain.DefIndex;
using PeteTimesSix.ResearchReinvented.Domain.Rules;
using PeteTimesSix.ResearchReinvented.Domain.Selection;
using PeteTimesSix.ResearchReinvented.Opportunities;
using Xunit;

namespace PeteTimesSix.ResearchReinvented.Tests;

public sealed class OpportunitySelectionTests
{
	private static readonly DefIdentity Project = new("ResearchProjectDef", "SelectionProject");
	private static readonly DefIdentity Analyse = OpportunityTypeIds.Of("Analyse");
	private static readonly DefIdentity Category = new("ResearchOpportunityCategoryDef", "Analysis");

	[Fact]
	public void EquivalentSubjectsAndRecipesCanonicalizeBeforeSemanticDeduplication()
	{
		var first = Thing("First"); var second = Thing("Second"); var recipeA = Recipe("RecipeA"); var recipeB = Recipe("RecipeB");
		var index = Index(things: [new(first), new(second)], recipes: [new(recipeA), new(recipeB)], alternates:
		[
			new(new DefIdentity("AlternateResearchSubjectsDef", "things"), first, second, AlternateSubjectMode.Equivalent, false),
			new(new DefIdentity("AlternateResearchSubjectsDef", "recipes"), recipeA, recipeB, AlternateSubjectMode.Equivalent, false)
		]);
		var result = Select(index, [Candidate("one", first), Candidate("two", second), Candidate("recipe-one", recipeA), Candidate("recipe-two", recipeB)]);

		Assert.Equal(2, result.Selected.Count);
		Assert.All(result.Selected, item => Assert.Contains(item.Spec.Requirement.CanonicalSubject, new[] { first, recipeA }));
		Assert.Equal(2, result.Rejections.Count(reason => reason.Kind == RejectionReasonKind.DuplicateSemanticKey));
	}

	[Fact]
	public void DuplicateKeysMergeReasonsAndRetainExplicitMetadataEffects()
	{
		var subject = Thing("Subject"); var metadata = new DefIdentity("ThingDef", "Metadata");
		var one = Candidate("inferred", subject, 1f, GenerationReasonKind.Recipe);
		var two = Candidate("override", subject, 3f, GenerationReasonKind.ExplicitMetadata, metadata);
		var selected = Assert.Single(Select(Index(things: [new(subject)]), [one, two]).Selected);

		Assert.Equal(3f, selected.Spec.Importance);
		Assert.Equal(new[] { "inferred", "override" }, selected.RuleIds);
		Assert.Contains(selected.Spec.Reasons, reason => reason.Kind == GenerationReasonKind.Recipe);
		Assert.Contains(selected.Spec.Reasons, reason => reason.Kind == GenerationReasonKind.ExplicitMetadata && reason.SourceDef == metadata);
		Assert.Equal(0, selected.SuppressedReasonCount);
	}

	[Fact]
	public void MergedGenerationReasonsAreBoundedAndKeepMetadataFirst()
	{
		var subject = Thing("ReasonSubject");
		var candidates = Enumerable.Range(0, 20).Select(index => Candidate("rule-" + index.ToString("D2"), subject, source: Thing("Source" + index.ToString("D2")))).ToList();
		candidates.Add(Candidate("metadata", subject, kind: GenerationReasonKind.ExplicitMetadata, source: Thing("Metadata")));

		var selected = Assert.Single(Select(Index(things: [new(subject)]), candidates, Policy(reasonLimit: 2)).Selected);

		Assert.Equal(2, selected.Spec.Reasons.Count);
		Assert.Equal(GenerationReasonKind.ExplicitMetadata, selected.Spec.Reasons[0].Kind);
		Assert.Equal(19, selected.SuppressedReasonCount);
	}

	[Fact]
	public void ScoringDiversityAndCategoryAndRuleLimitsAreAppliedDeterministically()
	{
		var a = Thing("A"); var b = Thing("B"); var c = Thing("C"); var d = Thing("D");
		var policy = Policy(categoryLimit: 2, ruleLimit: 1, diversity: 1);
		var result = Select(Index(things: [new(a), new(b), new(c), new(d)]),
			[Candidate("alpha", a, 4f, GenerationReasonKind.RequiredAnalysis), Candidate("alpha", b, 3f, GenerationReasonKind.RequiredAnalysis), Candidate("beta", a, 2f, GenerationReasonKind.Project, type: OpportunityTypeIds.Of("AnalyseMedicine")), Candidate("gamma", c, 1f, GenerationReasonKind.Project, type: OpportunityTypeIds.Of("AnalyseDrug")), Candidate("delta", d, .5f, GenerationReasonKind.Project, type: OpportunityTypeIds.Of("AnalyseFood"))], policy);

		Assert.Equal(new[] { a, c }, result.Selected.Select(item => item.Spec.Requirement.CanonicalSubject));
		Assert.Contains(result.Rejections, reason => reason.Kind == RejectionReasonKind.RuleLimit);
		Assert.Contains(result.Rejections, reason => reason.Kind == RejectionReasonKind.DiversityLimit);
		Assert.Contains(result.Rejections, reason => reason.Kind == RejectionReasonKind.CategoryLimit);
		Assert.True(result.Selected[0].Score.Evidence > result.Selected[1].Score.Evidence);
	}

	[Fact]
	public void DiversityUsesSharedEvidenceSourceSoOneRecipeFamilyCannotDominate()
	{
		var a = Thing("FamilyA"); var b = Thing("FamilyB"); var recipe = Recipe("BroadSourceRecipe");
		var result = Select(Index(things: [new(a), new(b)], recipes: [new(recipe)]),
			[Candidate("ingredients-a", a, kind: GenerationReasonKind.Ingredient, source: recipe), Candidate("ingredients-b", b, kind: GenerationReasonKind.Ingredient, source: recipe)],
			Policy(ruleLimit: 10, diversity: 1));

		Assert.Single(result.Selected);
		Assert.Contains(result.Rejections, reason => reason.Kind == RejectionReasonKind.DiversityLimit && reason.Detail.Contains(recipe.DefName, StringComparison.Ordinal));
	}

	[Fact]
	public void ScoreExposesEvidenceRelationFeasibilityAndImportanceComponents()
	{
		var direct = Thing("Direct"); var ancestor = Thing("Ancestor");
		var reason = new GenerationReason(GenerationReasonKind.RequiredAnalysis, EvidenceSource.RequiredAnalysis, direct);
		var generated = new OpportunityRuleResult(
		[
			new("direct", new OpportunitySpec(Project, Analyse, ResearchRelation.Direct, RequirementSpec.ForThing(direct), 2f, false, true, [reason]), evidenceConfidence: .5f),
			new("ancestor", new OpportunitySpec(Project, Analyse, ResearchRelation.Ancestor, RequirementSpec.ForThing(ancestor), 2f, false, true, [reason]), evidenceConfidence: .5f),
			new("faction", new OpportunitySpec(Project, Analyse, ResearchRelation.Direct, RequirementSpec.ForFaction(DefIdentity.Synthetic("faction")), 2f, false, true, [reason]), evidenceConfidence: .5f, staticFeasibility: .5f)
		]);
		var policy = new OpportunitySelectionPolicy(
			categories: [new(Analyse, ResearchRelation.Direct, Category), new(Analyse, ResearchRelation.Ancestor, Category)],
			defaultCategoryLimit: 10, defaultRuleLimit: 10, diversityFamilyLimit: 10);

		var selected = new OpportunitySelector().Select(Index(things: [new(direct), new(ancestor)]), generated, policy).Selected;

		Assert.Equal(new[] { 1f, .8f, 1f }, selected.Select(item => item.Score.Relation));
		Assert.Equal(new[] { 1f, 1f, .3f }, selected.Select(item => item.Score.Feasibility));
		Assert.All(selected, item => { Assert.Equal(.95f, item.Score.Evidence); Assert.Equal(.5f, item.Score.Confidence); Assert.Equal(2f, item.Score.Importance); });
	}

	[Fact]
	public void OutputAndBoundedDiagnosticsAreByteStableAcrossInputOrder()
	{
		var subjects = Enumerable.Range(0, 20).Select(index => Thing("Subject" + index.ToString("D2"))).ToArray();
		var index = Index(things: subjects.Select(subject => new ThingDefSnapshot(subject)));
		var candidates = subjects.Select(subject => Candidate("rule", subject)).ToArray();
		var policy = Policy(categoryLimit: 3, ruleLimit: 20, diversity: 1, diagnostics: 4);

		var originalCulture = CultureInfo.CurrentCulture;
		string first;
		string second;
		try
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
			first = Surface(Select(index, candidates, policy));
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
			second = Surface(Select(index, candidates.Reverse(), policy));
		}
		finally
		{
			CultureInfo.CurrentCulture = originalCulture;
		}
		Assert.Equal(first, second);
		Assert.Equal(4, Select(index, candidates, policy).Rejections.Count);
		Assert.True(Select(index, candidates, policy).SuppressedDiagnostics > 0);
	}

	[Fact]
	public void RuleGenerationAndSelectionAreByteStableAndContainNoDuplicateKeys()
	{
		var a = Thing("RegistryA"); var b = Thing("RegistryB");
		var first = new Source { Projects = [new(Project, requiredAnalyzed: [a, b])], Things = [new(a), new(b)] };
		var second = new Source { Projects = first.Projects.Reverse(), Things = first.Things.Reverse() };
		var firstIndex = ResearchDefIndex.Build(first); var secondIndex = ResearchDefIndex.Build(second);
		var registry = OpportunityRuleRegistry.CreateDefault(); var selector = new OpportunitySelector();
		var firstResult = selector.Select(firstIndex, registry.Generate(firstIndex, Project), Policy());
		var secondResult = selector.Select(secondIndex, registry.Generate(secondIndex, Project), Policy());

		Assert.Equal(Surface(firstResult), Surface(secondResult));
		Assert.Equal(firstResult.Selected.Count, firstResult.Selected.Select(item => item.Spec.Key).Distinct().Count());
	}

	[Fact]
	public void BroadFiltersAndMalformedInputsRemainBoundedAndStatic()
	{
		var things = Enumerable.Range(0, 512).Select(index => Thing("Broad" + index.ToString("D3"))).ToArray();
		var recipe = Recipe("BroadRecipe");
		var index = Index(things: things.Select(subject => new ThingDefSnapshot(subject)), recipes: [new(recipe, [Project], ingredients: [DefRequirementSnapshot.Filter(things)])]);
		var result = Select(index, things.Select(subject => Candidate("broad", subject)), Policy(categoryLimit: 5, ruleLimit: 512, diversity: 1));
		var generated = OpportunityRuleRegistry.CreateDefault().Generate(index, Project);

		Assert.Equal(5, result.Selected.Count);
		Assert.DoesNotContain(generated.Candidates, item => item.Spec.Requirement.CanonicalSubject.DefType == "ThingFilter");
		Assert.DoesNotContain(result.Selected, item => item.Spec.Requirement.CanonicalSubject.DefType == "ThingFilter");
		Assert.Throws<ArgumentOutOfRangeException>(() => new OpportunitySelectionPolicy(defaultCategoryLimit: 0));
		Assert.Throws<ArgumentNullException>(() => new OpportunitySelector().Select(null!, new OpportunityRuleResult()));
		Assert.Contains(new OpportunitySelector().Select(index, new OpportunityRuleResult([null!]), Policy()).Rejections, reason => reason.Kind == RejectionReasonKind.InvalidEvidence);
		Assert.Contains(new OpportunitySelector().Select(index, new OpportunityRuleResult(rejections: [null!]), Policy()).Rejections, reason => reason.Kind == RejectionReasonKind.InvalidEvidence);
	}

	[Fact]
	public void CategoryBudgetsAreCopiedExactlyWithoutAllocationOrRebalancing()
	{
		var subject = Thing("BudgetSubject"); var other = new DefIdentity("ResearchOpportunityCategoryDef", "Other");
		var budgets = new[] { new KeyValuePair<DefIdentity, float>(Category, 208.333328f), new KeyValuePair<DefIdentity, float>(other, 1f) };
		var result = Select(Index(things: [new(subject)]), [Candidate("rule", subject)], new OpportunitySelectionPolicy(categoryBudgets: budgets, projectBudget: 500f));

		Assert.Equal(budgets.OrderBy(item => item.Key), result.CategoryBudgets.OrderBy(item => item.Key));
		Assert.Equal(500f, result.ProjectBudget);
	}

	private static OpportunitySelectionResult Select(ResearchDefIndex index, IEnumerable<OpportunityCandidate> candidates, OpportunitySelectionPolicy? policy = null) =>
		new OpportunitySelector().Select(index, new OpportunityRuleResult(candidates), policy ?? Policy());
	private static OpportunityCandidate Candidate(string rule, DefIdentity subject, float importance = 1f, GenerationReasonKind kind = GenerationReasonKind.RequiredAnalysis, DefIdentity? source = null, DefIdentity? type = null) =>
		new(rule, new OpportunitySpec(Project, type ?? Analyse, ResearchRelation.Direct, subject.DefType == "RecipeDef" ? RequirementSpec.ForRecipe(subject) : RequirementSpec.ForThing(subject), importance, false, true,
			[new GenerationReason(kind, EvidenceSource.ProjectDefinition, source ?? subject, rule)]));
	private static OpportunitySelectionPolicy Policy(int categoryLimit = 24, int ruleLimit = 8, int diversity = 2, int diagnostics = 128, int reasonLimit = 16) =>
		new([new OpportunityCategoryAssignment(Analyse, ResearchRelation.Direct, Category), new OpportunityCategoryAssignment(OpportunityTypeIds.Of("AnalyseMedicine"), ResearchRelation.Direct, Category), new OpportunityCategoryAssignment(OpportunityTypeIds.Of("AnalyseDrug"), ResearchRelation.Direct, Category), new OpportunityCategoryAssignment(OpportunityTypeIds.Of("AnalyseFood"), ResearchRelation.Direct, Category)], [new KeyValuePair<DefIdentity, int>(Category, categoryLimit)], [new KeyValuePair<string, int>("alpha", ruleLimit), new KeyValuePair<string, int>("beta", ruleLimit), new KeyValuePair<string, int>("gamma", ruleLimit), new KeyValuePair<string, int>("delta", ruleLimit), new KeyValuePair<string, int>("rule", ruleLimit), new KeyValuePair<string, int>("broad", ruleLimit), new KeyValuePair<string, int>("one", ruleLimit), new KeyValuePair<string, int>("two", ruleLimit), new KeyValuePair<string, int>("recipe-one", ruleLimit), new KeyValuePair<string, int>("recipe-two", ruleLimit), new KeyValuePair<string, int>("inferred", ruleLimit), new KeyValuePair<string, int>("override", ruleLimit)], defaultCategoryLimit: categoryLimit, defaultRuleLimit: ruleLimit, diversityFamilyLimit: diversity, diagnosticLimit: diagnostics, reasonLimit: reasonLimit);
	private static string Surface(OpportunitySelectionResult result) => result.CanonicalSnapshot();
	private static DefIdentity Thing(string name) => new("ThingDef", name);
	private static DefIdentity Recipe(string name) => new("RecipeDef", name);
	private static ResearchDefIndex Index(IEnumerable<ThingDefSnapshot>? things = null, IEnumerable<RecipeDefSnapshot>? recipes = null, IEnumerable<AlternateLinkSnapshot>? alternates = null) => ResearchDefIndex.Build(new Source { Projects = [new(Project)], Things = things ?? [], Recipes = recipes ?? [], AlternateLinks = alternates ?? [] });
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
