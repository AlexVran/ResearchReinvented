using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Domain.DefIndex;
using PeteTimesSix.ResearchReinvented.Domain.Rules;
using PeteTimesSix.ResearchReinvented.Opportunities;
using Xunit;

namespace PeteTimesSix.ResearchReinvented.Tests;

public sealed class OpportunityRuleTests
{
	private static readonly DefIdentity Main = Project("Main");

	[Fact]
	public void DefaultRegistryHasStableOrderAndAlwaysProvidesTheoryBooksAndStaticSocialCandidates()
	{
		var registry = OpportunityRuleRegistry.CreateDefault();
		var result = registry.Generate(Index(new TestSource { Projects = [new(Main)] }), Main);

		Assert.Equal(registry.Rules.OrderBy(rule => rule.Order).ThenBy(rule => rule.Id).Select(rule => rule.Id), registry.Rules.Select(rule => rule.Id));
		AssertTypes(result, "BasicResearch", "SchematicStudy", "Brainstorming", "GainFactionKnowledge", "GainFactionlessKnowledge");
		Assert.Contains(result.Candidates, candidate => candidate.Spec.Type.DefName == "Brainstorming" && candidate.Spec.Requirement.Kind == RequirementKind.Faction);
	}

	[Fact]
	public void DirectMedicineAndIngestibleDrugProduceConservativeSpecializedCandidates()
	{
		var medicine = Thing("Medicine");
		var drug = Thing("Drug");
		var source = new TestSource
		{
			Projects = [new(Main, requiredAnalyzed: [medicine, drug])],
			Things = [new(medicine, traits: ThingDefTraits.Medicine), new(drug, traits: ThingDefTraits.Drug | ThingDefTraits.Ingestible)]
		};

		var result = OpportunityRuleRegistry.CreateDefault().Generate(Index(source), Main);

		Assert.Contains(result.Candidates, candidate => Is(candidate, "AnalyseMedicine", medicine));
		Assert.Contains(result.Candidates, candidate => Is(candidate, "AnalyseDrug", drug));
		Assert.Contains(result.Candidates, candidate => Is(candidate, "TrialDrug", drug));
		Assert.DoesNotContain(result.Candidates, candidate => Is(candidate, "TrialDrug", medicine));
	}

	[Fact]
	public void PawnAnalysisIncludesTheCorrectCorpseOpportunity()
	{
		var pawn = Thing("Pawn");
		var corpse = Thing("Corpse");
		var source = new TestSource
		{
			Projects = [new(Main, requiredAnalyzed: [pawn])],
			Things = [new(pawn, traits: ThingDefTraits.Pawn | ThingDefTraits.FleshPawn, corpseDefinition: corpse), new(corpse, traits: ThingDefTraits.Corpse)]
		};

		var result = OpportunityRuleRegistry.CreateDefault().Generate(Index(source), Main);

		Assert.Contains(result.Candidates, candidate => Is(candidate, "AnalysePawn", pawn));
		Assert.Contains(result.Candidates, candidate => Is(candidate, "AnalyseDissect", corpse));
		Assert.DoesNotContain(result.Candidates, candidate => candidate.Spec.Type.DefName == "AnalysePawnDissected");
	}

	[Fact]
	public void SurgeryIngredientIsClinicalMedicineEvidenceButBroadDrugFilterIsNotATrialSubject()
	{
		var medicine = Thing("ClinicalMedicine");
		var drug = Thing("AllowedDrug");
		var surgery = Recipe("Surgery");
		var broadRecipe = Recipe("BroadRecipe");
		var source = new TestSource
		{
			Projects = [new(Main)],
			Things = [new(medicine, traits: ThingDefTraits.Medicine), new(drug, traits: ThingDefTraits.Drug | ThingDefTraits.Ingestible)],
			Recipes =
			[
				new(surgery, [Main], ingredients: [DefRequirementSnapshot.Fixed(medicine)], traits: RecipeDefTraits.Surgery | RecipeDefTraits.Meaningful),
				new(broadRecipe, [Main], ingredients: [DefRequirementSnapshot.Filter([drug])], traits: RecipeDefTraits.Meaningful)
			]
		};

		var result = OpportunityRuleRegistry.CreateDefault().Generate(Index(source), Main);

		Assert.Contains(result.Candidates, candidate => Is(candidate, "AnalyseMedicine", medicine));
		Assert.DoesNotContain(result.Candidates, candidate => candidate.Spec.Type.DefName == "TrialDrug");
		Assert.DoesNotContain(result.Candidates, candidate => candidate.Spec.Requirement.CanonicalSubject.DefType == "ThingFilter");
	}

	[Fact]
	public void ContextRulesCoverIngredientsFacilitiesPlantsAndFuelWithoutExpandingFilters()
	{
		var material = Thing("Material");
		var facility = Thing("Bench");
		var crop = Thing("Crop");
		var biofuel = Thing("Biofuel");
		var plant = Thing("Plant");
		var recipe = Recipe("MakePart");
		var source = new TestSource
		{
			Projects = [new(Main, unlocks: [plant])],
			Things =
			[
				new(material), new(facility), new(crop, traits: ThingDefTraits.RawFood | ThingDefTraits.Ingestible),
				new(biofuel, traits: ThingDefTraits.Flammable),
				new(plant, harvestedProduct: crop, fuelRequirements: [DefRequirementSnapshot.Fixed(biofuel)], traits: ThingDefTraits.Plant)
			],
			Recipes = [new(recipe, [Main], users: [facility], ingredients: [DefRequirementSnapshot.Fixed(material)], traits: RecipeDefTraits.Meaningful)]
		};

		var result = OpportunityRuleRegistry.CreateDefault().Generate(Index(source), Main);

		AssertTypes(result, "AnalyseIngredients", "AnalyseProductionFacility", "AnalyseHarvestProduct", "AnalyseFuelFlammable", "AnalysePlant");
	}

	[Fact]
	public void TerrainRuleDistinguishesSoilFloorAndUnknownTerrain()
	{
		var soil = Terrain("Soil");
		var floor = Terrain("Floor");
		var natural = Terrain("Natural");
		var source = new TestSource
		{
			Projects = [new(Main, requiredAnalyzed: [soil, floor, natural])],
			Terrains = [new(soil, traits: TerrainDefTraits.Soil), new(floor, traits: TerrainDefTraits.PlayerBuildable), new(natural)]
		};

		var result = OpportunityRuleRegistry.CreateDefault().Generate(Index(source), Main);

		Assert.Contains(result.Candidates, candidate => Is(candidate, "AnalyseSoil", soil));
		Assert.Contains(result.Candidates, candidate => Is(candidate, "AnalyseFloor", floor));
		Assert.Contains(result.Candidates, candidate => Is(candidate, "AnalyseTerrain", natural));
	}

	[Fact]
	public void PrototypeRulesRequireDirectGatingAndSatisfiableStaticPrerequisites()
	{
		var ancestor = Project("Ancestor");
		var unrelated = Project("Unrelated");
		var surgery = Recipe("Surgery");
		var blocked = Recipe("BlockedSurgery");
		var product = Thing("Product");
		var source = new TestSource
		{
			Projects = [new(Main, prerequisites: [ancestor]), new(ancestor), new(unrelated)],
			Things = [new(product)],
			Recipes =
			[
				new(surgery, [Main, ancestor], traits: RecipeDefTraits.Surgery | RecipeDefTraits.Meaningful),
				new(blocked, [Main, unrelated], traits: RecipeDefTraits.Surgery | RecipeDefTraits.Meaningful)
			]
		};

		var result = OpportunityRuleRegistry.CreateDefault().Generate(Index(source), Main);

		Assert.Contains(result.Candidates, candidate => Is(candidate, "PrototypeSurgery", surgery));
		Assert.DoesNotContain(result.Candidates, candidate => Is(candidate, "PrototypeSurgery", blocked));
	}

	[Fact]
	public void ProductionConstructionAndTerrainPrototypesAreTypedAndBlacklistsWin()
	{
		var product = Thing("Product");
		var building = Thing("Building");
		var terrain = Terrain("BuildableTerrain");
		var production = Recipe("Production");
		var blacklisted = Recipe("Blacklisted");
		var source = new TestSource
		{
			Projects = [new(Main, unlocks: [building, terrain])],
			Things = [new(product), new(building, [Main], traits: ThingDefTraits.PlayerBuildable)],
			Terrains = [new(terrain, [Main], traits: TerrainDefTraits.PlayerBuildable)],
			Recipes =
			[
				new(production, [Main], [new DefCountSnapshot(product, 1)], traits: RecipeDefTraits.Meaningful),
				new(blacklisted, [Main], [new DefCountSnapshot(product, 1)], traits: RecipeDefTraits.Meaningful | RecipeDefTraits.Blacklisted)
			]
		};

		var result = OpportunityRuleRegistry.CreateDefault().Generate(Index(source), Main);

		AssertTypes(result, "PrototypeProduction", "PrototypeConstruction", "PrototypeTerrainConstruction");
		Assert.DoesNotContain(result.Candidates, candidate => Is(candidate, "PrototypeProduction", blacklisted));
	}

	[Fact]
	public void ExplicitSpecialPreservesTypeRelationAlternatesAndFlags()
	{
		var subject = Thing("Subject");
		var alternate = Thing("Alternate");
		var specialType = OpportunityTypeIds.Of("AnalyseMedicine");
		var special = new SpecialOpportunitySnapshot(new DefIdentity("SpecialResearchOpportunityDef", "Special"), Main, specialType,
			[new SpecialSubjectSnapshot(subject, SpecialSubjectKind.Thing)], relationOverride: ResearchRelation.Ancestor,
			alternateMode: AlternateSubjectMode.Equivalent, importance: 2.5f, rare: true, freebie: false);
		var source = new TestSource
		{
			Projects = [new(Main)], Things = [new(subject), new(alternate)], SpecialOpportunities = [special],
			AlternateLinks = [new(new DefIdentity("AlternateResearchSubjectsDef", "A"), subject, alternate, AlternateSubjectMode.Equivalent, false)]
		};

		var candidate = Assert.Single(OpportunityRuleRegistry.CreateDefault().Generate(Index(source), Main).Candidates, candidate => candidate.RuleId == "explicit-specials");

		Assert.Equal(ResearchRelation.Ancestor, candidate.Spec.Relation);
		Assert.Equal(2.5f, candidate.Spec.Importance);
		Assert.True(candidate.Spec.Rare);
		Assert.False(candidate.Spec.Freebie);
		Assert.Contains(alternate, candidate.Spec.Requirement.AlternateSubjects);
	}

	[Fact]
	public void SubjectlessExplicitSpecialCanRepresentAnEventDrivenActivity()
	{
		var activityType = OpportunityTypeIds.Of("CatchFish");
		var special = new SpecialOpportunitySnapshot(
			new DefIdentity("SpecialResearchOpportunityDef", "FishingActivity"),
			Main,
			activityType,
			[]);
		var source = new TestSource
		{
			Projects = [new(Main)],
			SpecialOpportunities = [special]
		};

		var candidate = Assert.Single(
			OpportunityRuleRegistry.CreateDefault().Generate(Index(source), Main).Candidates,
			candidate => candidate.RuleId == "explicit-specials");

		Assert.Equal(activityType, candidate.Spec.Type);
		Assert.Equal(RequirementKind.None, candidate.Spec.Requirement.Kind);
		Assert.Equal(Main, candidate.Spec.Project);
	}

	[Fact]
	public void MetadataSuppressesInferenceAndForceTakesPrecedence()
	{
		var drug = Thing("Drug");
		var custom = OpportunityTypeIds.Of("AnalyseMedicine");
		var metadata = new[]
		{
			new OpportunityOverrideSnapshot(Main, OpportunityOverrideAction.Suppress, Main, OpportunityTypeIds.Of("TrialDrug"), drug),
			new OpportunityOverrideSnapshot(Main, OpportunityOverrideAction.Force, Main, custom, drug, importanceMultiplier: 4f)
		};
		var source = new TestSource
		{
			Projects = [new(Main, requiredAnalyzed: [drug])],
			Things = [new(drug, traits: ThingDefTraits.Drug | ThingDefTraits.Ingestible)], OpportunityOverrides = metadata
		};

		var result = OpportunityRuleRegistry.CreateDefault().Generate(Index(source), Main);

		Assert.DoesNotContain(result.Candidates, candidate => candidate.Spec.Type.DefName == "TrialDrug");
		Assert.Contains(result.Candidates, candidate => candidate.RuleId == "metadata.force" && Is(candidate, "AnalyseMedicine", drug) && candidate.Spec.Importance == 4f);
		Assert.Contains(result.Rejections, rejection => rejection.Kind == RejectionReasonKind.SuppressedByMetadata);
	}

	[Fact]
	public void ReplaceAndReweightMetadataProduceExplicitlyReasonedCandidate()
	{
		var sourceThing = Thing("Source");
		var replacement = Thing("Replacement");
		var source = new TestSource
		{
			Projects = [new(Main, requiredAnalyzed: [sourceThing])], Things = [new(sourceThing), new(replacement)],
			OpportunityOverrides =
			[
				new(Main, OpportunityOverrideAction.Replace, Main, OpportunityTypeIds.Of("Analyse"), sourceThing, replacementOpportunityType: OpportunityTypeIds.Of("AnalyseMedicine"), replacementSubject: replacement),
				new(Main, OpportunityOverrideAction.Reweight, Main, OpportunityTypeIds.Of("AnalyseMedicine"), replacement, importanceMultiplier: 3f)
			]
		};

		var candidate = Assert.Single(OpportunityRuleRegistry.CreateDefault().Generate(Index(source), Main).Candidates, candidate => candidate.RuleId == "metadata.reweight");

		Assert.True(Is(candidate, "AnalyseMedicine", replacement));
		Assert.Equal(3f, candidate.Spec.Importance);
		Assert.Contains(candidate.Spec.Reasons, reason => reason.Kind == GenerationReasonKind.ExplicitMetadata);
	}

	[Fact]
	public void MalformedNullAndUnknownInputsReturnDiagnosticsOrRejectionsWithoutThrowing()
	{
		var unknown = Project("Unknown");
		var source = new TestSource
		{
			Projects = [new(Main)], OpportunityOverrides = [null, new(Main, OpportunityOverrideAction.Force, Main)]
		};
		var index = Index(source);

		var malformed = OpportunityRuleRegistry.CreateDefault().Generate(index, Main);
		var missing = OpportunityRuleRegistry.CreateDefault().Generate(index, unknown);

		Assert.Contains(index.Diagnostics, diagnostic => diagnostic.Kind == DefIndexDiagnosticKind.NullDefinition);
		Assert.Contains(malformed.Rejections, rejection => rejection.Kind == RejectionReasonKind.InvalidRequirement);
		Assert.Contains(missing.Rejections, rejection => rejection.Kind == RejectionReasonKind.MissingDefinition);
	}

	[Fact]
	public void MalformedOrDisabledSpecialDoesNotBecomeAnAccidentalTheoryCandidate()
	{
		var malformed = new SpecialOpportunitySnapshot(new DefIdentity("SpecialResearchOpportunityDef", "Malformed"), Main, null, []);
		var disabled = new SpecialOpportunitySnapshot(new DefIdentity("SpecialResearchOpportunityDef", "Disabled"), Main, OpportunityTypeIds.Of("Analyse"), [],
			forDirect: false, forAncestor: false, forDescendant: false);
		var source = new TestSource { Projects = [new(Main)], SpecialOpportunities = [malformed, disabled] };

		var result = OpportunityRuleRegistry.CreateDefault().Generate(Index(source), Main);

		Assert.DoesNotContain(result.Candidates, candidate => candidate.RuleId == "explicit-specials");
		Assert.Contains(result.Rejections, rejection => rejection.SourceDef == malformed.Identity);
	}

	[Fact]
	public void CyclesRemainBoundedAndUnknownDefsFallBackToGenericAnalysisAndTheory()
	{
		var other = Project("Other");
		var unknownThing = Thing("UnknownThing");
		var source = new TestSource
		{
			Projects = [new(Main, prerequisites: [other], requiredAnalyzed: [unknownThing]), new(other, prerequisites: [Main])],
			Things = [new(unknownThing)]
		};
		var index = Index(source);

		var result = OpportunityRuleRegistry.CreateDefault().Generate(index, Main);

		Assert.NotEmpty(index.PrerequisiteCycles);
		Assert.Contains(result.Candidates, candidate => Is(candidate, "Analyse", unknownThing));
		AssertTypes(result, "BasicResearch", "SchematicStudy");
	}

	[Fact]
	public void CandidateEnumerationIsIndependentOfLoadedDefOrder()
	{
		var medicine = Thing("Medicine");
		var drug = Thing("Drug");
		var first = new TestSource
		{
			Projects = [new(Main, requiredAnalyzed: [medicine, drug])],
			Things = [new(medicine, traits: ThingDefTraits.Medicine), new(drug, traits: ThingDefTraits.Drug | ThingDefTraits.Ingestible)]
		};
		var second = new TestSource
		{
			Projects = first.Projects.Reverse(), Things = first.Things.Reverse()
		};

		Assert.Equal(Surface(first), Surface(second));
	}

	private static string Surface(TestSource source) => string.Join("\n", OpportunityRuleRegistry.CreateDefault().Generate(Index(source), Main).Candidates
		.Select(candidate => $"{candidate.RuleId}|{candidate.Spec.Key}|{candidate.Spec.Importance}"));

	private static bool Is(OpportunityCandidate candidate, string type, DefIdentity subject) =>
		candidate.Spec.Type.DefName == type && candidate.Spec.Requirement.CanonicalSubject == subject;

	private static void AssertTypes(OpportunityRuleResult result, params string[] types)
	{
		foreach (var type in types) Assert.Contains(result.Candidates, candidate => candidate.Spec.Type.DefName == type);
	}

	private static ResearchDefIndex Index(TestSource source) => ResearchDefIndex.Build(source);
	private static DefIdentity Project(string name) => new("ResearchProjectDef", name);
	private static DefIdentity Thing(string name) => new("ThingDef", name);
	private static DefIdentity Recipe(string name) => new("RecipeDef", name);
	private static DefIdentity Terrain(string name) => new("TerrainDef", name);

	private sealed class TestSource : IResearchDefSnapshotSource
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
