using System.Text.Json.Nodes;
using Xunit;

namespace PeteTimesSix.ResearchReinvented.Tests;

public sealed class LegacyGeneratorCharacterizationTests
{
	private static readonly IReadOnlyDictionary<string, ExpectedGeneratorFixture> ExpectedFixtures =
		new Dictionary<string, ExpectedGeneratorFixture>(StringComparer.Ordinal)
		{
			["phase0/legacy-generator-compat-ferny-butcher-table.json"] = new(
				"62a0b75f7eba2c0ffe13bb30621337f67b1a9de22f23425e8b33b1379e5c4142",
				new Dictionary<string, ExpectedProject>(StringComparer.Ordinal)
				{
					["Ferny_ButcherTable"] = new(27, "5aefeba3845eab6bc6df5221bbc70ecf78baeebee48ef282bbc846750c42e5d5"),
				}),
			["phase0/legacy-generator-minimal.json"] = new(
				"88f6319c9e515082b265e103b8e14d28395a1b48ed6ba5b7adc1cd7c55139204",
				new Dictionary<string, ExpectedProject>(StringComparer.Ordinal)
				{
					["BioferriteShaping"] = new(37, "959b3263d95b9ef428cf321ab58d001d7dff8dea13dc38cb58f7a5df56f3d6e8"),
					["BiofuelRefining"] = new(36, "b060cd62c9704872946649274fa1ef1c0fa826e82025c576f29efce3b17ec740"),
					["Bionics"] = new(40, "58ff726df8dbcb68abb545270c8ec4e2907ef5d9601f96a2862c7aee5c1f0a72"),
					["BlissLobotomy"] = new(21, "650f818833f89e7febb30ec3e62bb9bb44198e46da0089da6e71d352412a2d74"),
					["CarpetMaking"] = new(24, "eb56fc482717f9a16c8f7adf9a184f37fe95ca589a71a15bc5c859f3f5c4eb37"),
					["ComplexClothing"] = new(97, "cf56d9e4118a9712c4457244ed0a06283ea3d683ad020173c114e0679f114c30"),
					["DrugProduction"] = new(25, "4acff58b518a392c371e687e7006ec4bd2ea4a41ab6e73c132ce90f3cd177ee0"),
					["Electricity"] = new(65, "72a4e45e2cb58c6dbe9bdefcc53562a84399a682dae9406b45065de76b556155"),
					["MedicineProduction"] = new(29, "3b3274e55482753c370e84fb67e670cefba6cde991f7faf7a64e2807d2272d17"),
					["MicroelectronicsBasics"] = new(60, "454adf759e8c12bdc69414c8f7f5f8fbad0e767c732c35383ea77688bc5686bb"),
					["MultiAnalyzer"] = new(34, "1a0c92e6dea4a18502a338cf831ca51b7a621c74189b8c7d5678a93808d6a0ef"),
					["Prosthetics"] = new(29, "4acd3d39ab636d856475c222cd4a7dff381b4ee96b855dc5d5968bc4676175d2"),
					["TreeSowing"] = new(55, "2fbfc7d07658e3a8741c28fbdfd4586bdca1db85f0ac933a3ecc837a54946c65"),
				}),
		};

	public static IEnumerable<object[]> GeneratorFixtureNames =>
		ExpectedFixtures.Keys.Select(name => new object[] { name });

	[Theory]
	[MemberData(nameof(GeneratorFixtureNames))]
	public void CapturedProjectsMatchPhase0SemanticSnapshots(string fixtureName)
	{
		var fixture = Phase0Fixture.Load(fixtureName);
		var expectedFixture = ExpectedFixtures[fixtureName];
		var projects = fixture.RequiredArray("projects");
		var actualProjects = projects.Objects().ToDictionary(project => project.RequiredString("defName"), StringComparer.Ordinal);

		Assert.Equal("research-reinvented-legacy-generator-fixture-v1", fixture.RequiredString("schema"));
		Assert.Equal(expectedFixture.Projects.Keys.Order(), actualProjects.Keys.Order());
		Assert.Equal(expectedFixture.ProjectDigest, fixture.RequiredString("projectDigest"));
		Assert.Equal(expectedFixture.ProjectDigest, Phase0Fixture.CanonicalDigest(projects));

		foreach (var (projectName, expectedProject) in expectedFixture.Projects)
		{
			var project = actualProjects[projectName];
			var opportunities = project.RequiredArray("opportunities");
			Assert.True(
				opportunities.Count == expectedProject.OpportunityCount,
				$"{projectName}: expected {expectedProject.OpportunityCount} opportunities, found {opportunities.Count}.");
			Assert.Equal(expectedProject.OpportunityCount, project.RequiredInt("opportunityCount"));
			Assert.Equal(expectedProject.OpportunityDigest, project.RequiredString("opportunityDigest"));
			Assert.True(
				Phase0Fixture.CanonicalDigest(opportunities) == expectedProject.OpportunityDigest,
				$"{projectName}: semantic opportunity snapshot changed; inspect type, relation, category, requirement, progress, flags, and source fields.");
		}
	}

	[Fact]
	public void CapturedOpportunitiesExposeSemanticFieldsRatherThanRuntimeAddresses()
	{
		var fixture = Phase0Fixture.Load("phase0/legacy-generator-minimal.json");
		var opportunities = fixture.RequiredArray("projects").Objects()
			.SelectMany(project => project.RequiredArray("opportunities").Objects())
			.ToArray();
		var requiredFields = new[]
		{
			"project", "type", "relation", "category", "maximumProgress", "currentProgress",
			"importance", "rare", "freebie", "generationSource", "requirement",
		};

		Assert.Equal(552, opportunities.Length);
		foreach (var opportunity in opportunities)
		{
			foreach (var field in requiredFields)
				Assert.True(opportunity.ContainsKey(field), $"Opportunity omitted semantic field '{field}': {opportunity}");

			var requirement = opportunity.RequiredObject("requirement");
			Assert.True(requirement.ContainsKey("kind"), $"Requirement omitted kind: {requirement}");
			Assert.True(requirement.ContainsKey("canonicalIdentity"), $"Requirement omitted canonical identity: {requirement}");
			Assert.True(requirement.ContainsKey("alternates"), $"Requirement omitted alternates: {requirement}");
		}
	}

	[Fact]
	public void CompatibilityFixturePreservesCanonicalAlternateSubjects()
	{
		var fixture = Phase0Fixture.Load("phase0/legacy-generator-compat-ferny-butcher-table.json");
		var opportunity = fixture.RequiredArray("projects").Objects().Single()
			.RequiredArray("opportunities").Objects()
			.Single(value => value.RequiredString("type") == "Analyse");
		var requirement = opportunity.RequiredObject("requirement");

		Assert.Equal("TableButcher", requirement.RequiredString("canonicalIdentity"));
		Assert.Equal(
			new[] { "AncientTableButcher" },
			requirement.RequiredArray("alternates").Select(value => value!.GetValue<string>()));
		Assert.Equal("Direct", opportunity.RequiredString("relation"));
		Assert.Equal("ReverseEngineering", opportunity.RequiredString("category"));
	}

	[Fact]
	public void CapturedCategoryBudgetsRetainLegacyFloorsAndWeightedTotals()
	{
		var minimal = Phase0Fixture.Load("phase0/legacy-generator-minimal.json");
		var projects = minimal.RequiredArray("projects").Objects()
			.ToDictionary(project => project.RequiredString("defName"), StringComparer.Ordinal);
		var anomalyZeroCostBudgets = projects["BioferriteShaping"].RequiredArray("categoryStores").Objects()
			.Select(store => store.RequiredFloat("researchPoints"));
		Assert.All(anomalyZeroCostBudgets, value => Assert.Equal(1f, value));

		var electricity = CategoryBudgets(projects["Electricity"]);
		Assert.Equal(1f, electricity["Theory"]);
		Assert.Equal(500f, electricity["ForwardEngineering"]);
		Assert.Equal(600f, electricity["Prototyping"]);
		Assert.Equal(800f, electricity["Books"]);

		var compatibility = Phase0Fixture.Load("phase0/legacy-generator-compat-ferny-butcher-table.json");
		var ferny = CategoryBudgets(compatibility.RequiredArray("projects").Objects().Single());
		Assert.InRange(ferny["Material"], 208.3333f, 208.3334f);
		Assert.Equal(250f, ferny["Social"]);
	}

	[Fact]
	public void CapturedRarityAndFreebieResultsRemainExplicit()
	{
		var opportunities = ExpectedFixtures.Keys
			.Select(Phase0Fixture.Load)
			.SelectMany(fixture => fixture.RequiredArray("projects").Objects())
			.SelectMany(project => project.RequiredArray("opportunities").Objects())
			.ToArray();

		Assert.Equal(579, opportunities.Length);
		Assert.All(opportunities, opportunity =>
		{
			Assert.False(opportunity.RequiredBool("rare"));
			Assert.False(opportunity.RequiredBool("freebie"));
		});
	}

	[Fact]
	public void ArgonicCompatibilityContextIsCapturedWithoutClaimingPrototypeRepair()
	{
		var fixture = Phase0Fixture.Load("phase0/legacy-generator-compat-ferny-butcher-table.json");
		var packageIds = fixture.RequiredArray("packageIds").Select(value => value!.GetValue<string>()).ToArray();
		var opportunities = fixture.RequiredArray("projects").Objects().Single().RequiredArray("opportunities").Objects();

		Assert.Contains("Argon.CoreLib", packageIds);
		Assert.Contains(opportunities, opportunity =>
			opportunity.RequiredString("type") == "PrototypeConstruction"
			&& opportunity.RequiredObject("requirement").RequiredString("canonicalIdentity") == "TableButcher");
	}

	private static Dictionary<string, float> CategoryBudgets(JsonObject project)
	{
		return project.RequiredArray("categoryStores").Objects().ToDictionary(
			store => store.RequiredString("category"),
			store => store.RequiredFloat("researchPoints"),
			StringComparer.Ordinal);
	}

	private sealed record ExpectedGeneratorFixture(
		string ProjectDigest,
		IReadOnlyDictionary<string, ExpectedProject> Projects);

	private sealed record ExpectedProject(int OpportunityCount, string OpportunityDigest);
}
