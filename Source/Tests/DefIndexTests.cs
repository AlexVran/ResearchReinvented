using System.Collections;
using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Domain.DefIndex;
using PeteTimesSix.ResearchReinvented.Opportunities;
using Xunit;

namespace PeteTimesSix.ResearchReinvented.Tests;

public sealed class DefIndexTests
{
	private static readonly DefIdentity Main = Project("Main");
	private static readonly DefIdentity Base = Project("Base");
	private static readonly DefIdentity Hidden = Project("Hidden");

	[Fact]
	public void ProjectQueryRetainsCompleteDirectHiddenAndAncestorProvenance()
	{
		var raw = Thing("Raw");
		var stuffA = Thing("StuffA");
		var stuffB = Thing("StuffB");
		var fuelA = Thing("FuelA");
		var fuelB = Thing("FuelB");
		var plant = Thing("Plant");
		var harvest = Thing("Harvest");
		var analyzed = Thing("Analyzed");
		var product = Thing("Product");
		var facility = Thing("Facility");
		var ancestorUnlock = Thing("AncestorUnlock");
		var specialThing = Thing("SpecialThing");
		var techprint = Thing("Techprint");
		var recipe = Recipe("MakeProduct");
		var special = Identity("SpecialResearchOpportunityDef", "ExplicitSpecial");
		var source = new TestSource
		{
			Projects =
			[
				new ProjectDefSnapshot(Base, unlocks: [ancestorUnlock]),
				new ProjectDefSnapshot(Hidden),
				new ProjectDefSnapshot(
					Main,
					prerequisites: [Base],
					hiddenPrerequisites: [Hidden],
					unlocks: [plant],
					requiredAnalyzed: [analyzed],
					techprint: techprint)
			],
			Recipes =
			[
				new RecipeDefSnapshot(
					recipe,
					researchPrerequisites: [Main],
					products: [new DefCountSnapshot(product, 2f)],
					users: [facility],
					ingredients: [DefRequirementSnapshot.Filter([raw, stuffA, stuffB], 3f)])
			],
			Things =
			[
				SimpleThing(raw),
				SimpleThing(stuffA),
				SimpleThing(stuffB),
				SimpleThing(fuelA),
				SimpleThing(fuelB),
				new ThingDefSnapshot(
					plant,
					constructionCosts: [DefRequirementSnapshot.Filter([stuffA, stuffB], 10f)],
					harvestedProduct: harvest,
					fuelRequirements: [DefRequirementSnapshot.Filter([fuelA, fuelB])]),
				SimpleThing(harvest),
				new ThingDefSnapshot(analyzed, constructionCosts: [DefRequirementSnapshot.Fixed(raw, 4f)]),
				SimpleThing(product),
				SimpleThing(facility),
				SimpleThing(ancestorUnlock),
				SimpleThing(specialThing),
				SimpleThing(techprint)
			],
			SpecialOpportunities =
			[
				new SpecialOpportunitySnapshot(
					special,
					Main,
					Identity("ResearchOpportunityTypeDef", "Analyse"),
					[new SpecialSubjectSnapshot(specialThing, SpecialSubjectKind.Thing)])
			]
		};

		var index = ResearchDefIndex.Build(source);
		var evidence = index.EvidenceFor(Main);

		Assert.True(index.TryGetProject(Main, out var indexedProject));
		Assert.NotNull(indexedProject);
		Assert.Contains(indexedProject!.Prerequisites, edge => edge.Prerequisite == Base && edge.Kind == PrerequisiteKind.Direct);
		Assert.Contains(indexedProject.Prerequisites, edge => edge.Prerequisite == Hidden && edge.Kind == PrerequisiteKind.Hidden);
		Assert.Contains(evidence, item => IsEvidence(item, Base, ResearchRelation.Ancestor, SubjectRole.Project, EvidenceSource.Prerequisite, Main));
		Assert.Contains(evidence, item => IsEvidence(item, Hidden, ResearchRelation.Ancestor, SubjectRole.Project, EvidenceSource.HiddenPrerequisite, Main));
		Assert.Contains(evidence, item => IsEvidence(item, plant, ResearchRelation.Direct, SubjectRole.Unlock, EvidenceSource.ProjectDefinition, Main));
		Assert.Contains(evidence, item => IsEvidence(item, analyzed, ResearchRelation.Direct, SubjectRole.AnalysisRequirement, EvidenceSource.RequiredAnalysis, Main));
		Assert.Contains(evidence, item => IsEvidence(item, raw, ResearchRelation.Direct, SubjectRole.CostMaterial, EvidenceSource.RequiredAnalysis, analyzed));
		Assert.Contains(evidence, item => item.Relation == ResearchRelation.Direct && item.Role == SubjectRole.CostMaterial && item.Subject.DefType == "ThingFilter" && item.SourceDef == plant);
		Assert.Contains(evidence, item => item.Relation == ResearchRelation.Direct && item.Role == SubjectRole.Fuel && item.Subject.DefType == "ThingFilter" && item.SourceDef == plant);
		Assert.Contains(evidence, item => IsEvidence(item, harvest, ResearchRelation.Direct, SubjectRole.Plant, EvidenceSource.ThingDefinition, plant));
		Assert.Contains(evidence, item => IsEvidence(item, recipe, ResearchRelation.Direct, SubjectRole.Recipe, EvidenceSource.RecipeDefinition, recipe));
		Assert.Contains(evidence, item => IsEvidence(item, product, ResearchRelation.Direct, SubjectRole.Product, EvidenceSource.RecipeDefinition, recipe));
		Assert.Contains(evidence, item => IsEvidence(item, facility, ResearchRelation.Direct, SubjectRole.ProductionFacility, EvidenceSource.RecipeDefinition, recipe));
		Assert.Contains(evidence, item => item.Relation == ResearchRelation.Direct && item.Role == SubjectRole.Ingredient && item.Subject.DefType == "ThingFilter" && item.SourceDef == recipe);
		Assert.Contains(evidence, item => IsEvidence(item, techprint, ResearchRelation.Direct, SubjectRole.Techprint, EvidenceSource.ProjectDefinition, Main));
		Assert.Contains(evidence, item => IsEvidence(item, specialThing, ResearchRelation.Direct, SubjectRole.Special, EvidenceSource.SpecialOpportunity, special));
		Assert.Contains(evidence, item => IsEvidence(item, ancestorUnlock, ResearchRelation.Ancestor, SubjectRole.Unlock, EvidenceSource.ProjectDefinition, Base));
		Assert.Equal([Base, Hidden], index.AncestorsFor(Main));
		Assert.Empty(index.Diagnostics);
	}

	[Fact]
	public void IterativeTraversalReportsEachCyclicComponentOnceAndStaysBounded()
	{
		var a = Project("A");
		var b = Project("B");
		var self = Project("Self");
		var source = new TestSource
		{
			Projects =
			[
				new ProjectDefSnapshot(a, prerequisites: [b]),
				new ProjectDefSnapshot(b, hiddenPrerequisites: [a]),
				new ProjectDefSnapshot(self, prerequisites: [self])
			]
		};

		var index = ResearchDefIndex.Build(source);

		Assert.Equal(2, index.PrerequisiteCycles.Count);
		Assert.Contains(index.PrerequisiteCycles, cycle => cycle.Projects.SequenceEqual([a, b]));
		Assert.Contains(index.PrerequisiteCycles, cycle => cycle.Projects.SequenceEqual([self]));
		Assert.Equal([b], index.AncestorsFor(a));
		Assert.Equal([a], index.AncestorsFor(b));
		Assert.Empty(index.AncestorsFor(self));
		Assert.InRange(index.EvidenceFor(a).Count, 1, 10);
	}

	[Fact]
	public void BroadIngredientFilterIsOneCompactBitsetEvidenceEdge()
	{
		var things = Enumerable.Range(0, 512).Select(index => Thing($"Broad_{index:D3}")).ToArray();
		var recipe = Recipe("BroadRecipe");
		var source = new TestSource
		{
			Projects = [new ProjectDefSnapshot(Main)],
			Recipes =
			[
				new RecipeDefSnapshot(
					recipe,
					researchPrerequisites: [Main],
					ingredients: [DefRequirementSnapshot.Filter(things)])
			],
			Things = things.Select(SimpleThing).ToArray()
		};

		var index = ResearchDefIndex.Build(source);
		var indexedRecipe = Assert.Single(index.Recipes);
		var ingredient = Assert.Single(indexedRecipe.Ingredients);
		var filter = Assert.IsType<CompactDefFilter>(ingredient.Filter);

		Assert.Equal(512, filter.AllowedDefinitionCount);
		Assert.Equal(8, filter.EncodedWords.Count);
		Assert.Equal(512, index.DefinitionsAllowedBy(filter).Count());
		Assert.True(index.FilterAllows(filter, things[0]));
		Assert.True(index.FilterAllows(filter, things[^1]));
		Assert.Single(index.EvidenceFor(Main), item => item.Role == SubjectRole.Ingredient);
		Assert.DoesNotContain(index.EvidenceFor(Main), item => things.Contains(item.Subject));
	}

	[Fact]
	public void MalformedAndNullModdedSnapshotsProduceBoundedDiagnosticsNotExceptions()
	{
		var missing = Project("Missing");
		var recipe = Recipe("MalformedRecipe");
		var thing = Thing("MalformedThing");
		var special = Identity("SpecialResearchOpportunityDef", "MalformedSpecial");
		var source = new TestSource
		{
			Projects = [null, new ProjectDefSnapshot(Main, prerequisites: [null, missing])],
			Recipes =
			[
				new RecipeDefSnapshot(
					recipe,
					products: [null, new DefCountSnapshot(null, 1f)],
					users: [null],
					ingredients: [null, DefRequirementSnapshot.Fixed(null), DefRequirementSnapshot.Filter([null])])
			],
			Things = [null, new ThingDefSnapshot(thing, researchPrerequisites: [null], constructionCosts: [null])],
			Terrains = [null],
			SpecialOpportunities =
			[
				null,
				new SpecialOpportunitySnapshot(special, null, null, [null, new SpecialSubjectSnapshot(null, SpecialSubjectKind.Thing)])
			],
			AlternateLinks = [null, new AlternateLinkSnapshot(Identity("AlternateResearchSubjectsDef", "Broken"), null, thing, AlternateSubjectMode.Equivalent, false)]
		};

		var index = ResearchDefIndex.Build(source);

		Assert.Contains(index.Diagnostics, diagnostic => diagnostic.Kind == DefIndexDiagnosticKind.NullDefinition);
		Assert.Contains(index.Diagnostics, diagnostic => diagnostic.Kind == DefIndexDiagnosticKind.NullReference);
		Assert.Contains(index.Diagnostics, diagnostic => diagnostic.Kind == DefIndexDiagnosticKind.MissingProject);
		Assert.Contains(index.Diagnostics, diagnostic => diagnostic.Kind == DefIndexDiagnosticKind.InvalidRequirement);
		Assert.InRange(index.Diagnostics.Count, 4, 20);
		Assert.Empty(index.PrerequisiteCycles);
		Assert.All(index.EvidenceFor(Main), item => Assert.NotNull(item.Subject));
	}

	[Fact]
	public void OutputIsIndependentOfEverySourceEnumerationOrder()
	{
		var thingA = Thing("A");
		var thingB = Thing("B");
		var recipe = Recipe("Recipe");
		var linkSource = Identity("AlternateResearchSubjectsDef", "Explicit");
		var forward = new TestSource
		{
			Projects =
			[
				new ProjectDefSnapshot(Base, unlocks: [thingA, thingB]),
				new ProjectDefSnapshot(Main, prerequisites: [Hidden, Base], unlocks: [recipe]),
				new ProjectDefSnapshot(Hidden)
			],
			Recipes = [new RecipeDefSnapshot(recipe, products: [new DefCountSnapshot(thingA, 1f), new DefCountSnapshot(thingB, 2f)], users: [thingB, thingA])],
			Things = [SimpleThing(thingA), SimpleThing(thingB)],
			AlternateLinks = [new AlternateLinkSnapshot(linkSource, thingA, thingB, AlternateSubjectMode.Equivalent, false)]
		};
		var reverse = new TestSource
		{
			Projects =
			[
				new ProjectDefSnapshot(Hidden),
				new ProjectDefSnapshot(Main, prerequisites: [Base, Hidden], unlocks: [recipe]),
				new ProjectDefSnapshot(Base, unlocks: [thingB, thingA])
			],
			Recipes = [new RecipeDefSnapshot(recipe, products: [new DefCountSnapshot(thingB, 2f), new DefCountSnapshot(thingA, 1f)], users: [thingA, thingB])],
			Things = [SimpleThing(thingB), SimpleThing(thingA)],
			AlternateLinks = [new AlternateLinkSnapshot(linkSource, thingB, thingA, AlternateSubjectMode.Equivalent, false)]
		};

		Assert.Equal(ResearchDefIndex.Build(forward).CanonicalSnapshot(), ResearchDefIndex.Build(reverse).CanonicalSnapshot());
	}

	[Fact]
	public void ExplicitAndInferredAlternatesFormOneStableTransitiveGroup()
	{
		var a = Thing("A");
		var b = Thing("B");
		var c = Thing("C");
		var explicitSource = Identity("AlternateResearchSubjectsDef", "Explicit");
		var inferredSource = Identity("AlternateResearchSubjectsDef", "RR_auto_ancient_C");
		var source = new TestSource
		{
			Things = [SimpleThing(c), SimpleThing(a), SimpleThing(b)],
			AlternateLinks =
			[
				new AlternateLinkSnapshot(inferredSource, b, c, AlternateSubjectMode.Equivalent, true),
				new AlternateLinkSnapshot(explicitSource, a, b, AlternateSubjectMode.Equivalent, false)
			]
		};

		var index = ResearchDefIndex.Build(source);
		var group = Assert.Single(index.AlternateGroups);

		Assert.Equal([a, b, c], group.Members);
		Assert.Equal([explicitSource, inferredSource], group.SourceDefs);
		Assert.True(group.HasExplicitSource);
		Assert.True(group.HasInferredSource);
		Assert.Same(group, index.AlternateGroupFor(a, AlternateSubjectMode.Equivalent));
		Assert.Same(group, index.AlternateGroupFor(c, AlternateSubjectMode.Equivalent));
		Assert.Null(index.AlternateGroupFor(a, AlternateSubjectMode.Similar));
	}

	[Fact]
	public void BuilderEnumeratesEachLoadedDefCollectionExactlyOnceWithoutWorldServices()
	{
		var source = new CountingSource();

		ResearchDefIndex.Build(source);

		Assert.All(source.Collections, collection => Assert.Equal(1, collection.EnumerationCount));
		var surface = string.Join("|", typeof(IResearchDefSnapshotSource).GetProperties().Select(property => property.PropertyType.FullName));
		Assert.DoesNotContain("Map", surface, StringComparison.Ordinal);
		Assert.DoesNotContain("Pawn", surface, StringComparison.Ordinal);
	}

	[Fact]
	public void BuiltIndexIsDetachedFromMutableSnapshotInputs()
	{
		var first = Thing("First");
		var second = Thing("Second");
		var allowed = new List<DefIdentity?> { first };
		var projects = new List<ProjectDefSnapshot?> { new(Main) };
		var recipe = new RecipeDefSnapshot(
			Recipe("ImmutableRecipe"),
			researchPrerequisites: [Main],
			ingredients: [DefRequirementSnapshot.Filter(allowed)]);
		var source = new TestSource
		{
			Projects = projects,
			Recipes = [recipe],
			Things = [SimpleThing(first), SimpleThing(second)]
		};

		allowed.Add(second);
		var index = ResearchDefIndex.Build(source);
		projects.Clear();
		var filter = Assert.Single(Assert.Single(index.Recipes).Ingredients).Filter!;

		Assert.Single(index.Projects);
		Assert.Equal(1, filter.AllowedDefinitionCount);
		Assert.True(index.FilterAllows(filter, first));
		Assert.False(index.FilterAllows(filter, second));
		Assert.True(((ICollection<IndexedProject>)index.Projects).IsReadOnly);
		Assert.True(((ICollection<ulong>)filter.EncodedWords).IsReadOnly);
	}

	private static bool IsEvidence(
		SubjectEvidence evidence,
		DefIdentity subject,
		ResearchRelation relation,
		SubjectRole role,
		EvidenceSource source,
		DefIdentity sourceDef)
	{
		return evidence.Subject == subject
			&& evidence.Relation == relation
			&& evidence.Role == role
			&& evidence.Source == source
			&& evidence.SourceDef == sourceDef;
	}

	private static ThingDefSnapshot SimpleThing(DefIdentity identity) => new(identity);

	private static DefIdentity Project(string name) => Identity("ResearchProjectDef", name);

	private static DefIdentity Recipe(string name) => Identity("RecipeDef", name);

	private static DefIdentity Thing(string name) => Identity("ThingDef", name);

	private static DefIdentity Identity(string type, string name) => new(type, name);

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

	private sealed class CountingSource : IResearchDefSnapshotSource
	{
		private readonly CountingEnumerable<ProjectDefSnapshot?> projects = new([]);
		private readonly CountingEnumerable<RecipeDefSnapshot?> recipes = new([]);
		private readonly CountingEnumerable<ThingDefSnapshot?> things = new([]);
		private readonly CountingEnumerable<TerrainDefSnapshot?> terrains = new([]);
		private readonly CountingEnumerable<SpecialOpportunitySnapshot?> specials = new([]);
		private readonly CountingEnumerable<AlternateLinkSnapshot?> alternates = new([]);
		private readonly CountingEnumerable<OpportunityOverrideSnapshot?> overrides = new([]);

		public IEnumerable<ProjectDefSnapshot?> Projects => projects;

		public IEnumerable<RecipeDefSnapshot?> Recipes => recipes;

		public IEnumerable<ThingDefSnapshot?> Things => things;

		public IEnumerable<TerrainDefSnapshot?> Terrains => terrains;

		public IEnumerable<SpecialOpportunitySnapshot?> SpecialOpportunities => specials;

		public IEnumerable<AlternateLinkSnapshot?> AlternateLinks => alternates;

		public IEnumerable<OpportunityOverrideSnapshot?> OpportunityOverrides => overrides;

		public IEnumerable<ICountingEnumerable> Collections => [projects, recipes, things, terrains, specials, alternates, overrides];
	}

	private interface ICountingEnumerable
	{
		int EnumerationCount { get; }
	}

	private sealed class CountingEnumerable<T>(IEnumerable<T> values) : IEnumerable<T>, ICountingEnumerable
	{
		private readonly IReadOnlyList<T> values = values.ToArray();

		public int EnumerationCount { get; private set; }

		public IEnumerator<T> GetEnumerator()
		{
			EnumerationCount++;
			return values.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
