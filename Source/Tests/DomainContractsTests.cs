using System.Text.Json.Nodes;
using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Opportunities;
using Xunit;

namespace PeteTimesSix.ResearchReinvented.Tests;

public sealed class DomainContractsTests
{
	private static readonly DefIdentity Electricity = new("ResearchProjectDef", "Electricity");
	private static readonly DefIdentity Analyse = new("ResearchOpportunityTypeDef", "Analyse");
	private static readonly DefIdentity TableButcher = new("ThingDef", "TableButcher");

	[Fact]
	public void EqualSemanticOpportunitiesProduceEqualStableKeys()
	{
		var first = new OpportunitySpec(
			Electricity,
			Analyse,
			ResearchRelation.Direct,
			RequirementSpec.ForThing(
				TableButcher,
				AlternateSubjectMode.Equivalent,
				new[] { new DefIdentity("ThingDef", "AncientTableButcher") }),
			1f,
			rare: false,
			freebie: false,
			new[] { LegacyReason("direct analysis") });
		var second = new OpportunitySpec(
			new DefIdentity("ResearchProjectDef", "Electricity"),
			new DefIdentity("ResearchOpportunityTypeDef", "Analyse"),
			ResearchRelation.Direct,
			RequirementSpec.ForThing(new DefIdentity("ThingDef", "TableButcher")),
			3.5f,
			rare: true,
			freebie: true,
			new[] { LegacyReason("different provenance") });

		Assert.Equal(first.Key, second.Key);
		Assert.Equal(first, second);
		Assert.Equal(
			"rr1|project=ResearchProjectDef/Electricity|type=ResearchOpportunityTypeDef/Analyse|relation=Direct|requirement=Thing|subject=ThingDef/TableButcher",
			first.Key.Value);
		Assert.Equal(first.Key, OpportunityKey.Parse(first.Key.Value));
	}

	[Fact]
	public void EveryDeclaredKeyComponentChangesTheKey()
	{
		var baseline = Key(Electricity, Analyse, ResearchRelation.Direct, RequirementSpec.ForThing(TableButcher));
		var variants = new[]
		{
			baseline,
			Key(new DefIdentity("ResearchProjectDef", "MicroelectronicsBasics"), Analyse, ResearchRelation.Direct, RequirementSpec.ForThing(TableButcher)),
			Key(Electricity, new DefIdentity("ResearchOpportunityTypeDef", "AnalyseProductionFacility"), ResearchRelation.Direct, RequirementSpec.ForThing(TableButcher)),
			Key(Electricity, Analyse, ResearchRelation.Ancestor, RequirementSpec.ForThing(TableButcher)),
			Key(Electricity, Analyse, ResearchRelation.Direct, RequirementSpec.ForTerrain(new DefIdentity("TerrainDef", "TableButcher"))),
			Key(Electricity, Analyse, ResearchRelation.Direct, RequirementSpec.ForThing(new DefIdentity("ThingDef", "AncientTableButcher"))),
		};

		Assert.Equal(variants.Length, variants.Distinct().Count());
	}

	[Fact]
	public void DefinitionIdentityNormalizesUnicodeAndEscapesKeyDelimiters()
	{
		var composed = new DefIdentity("Thing|Def", "Caf\u00e9/Bench");
		var decomposed = new DefIdentity("Thing|Def", "Cafe\u0301/Bench");

		Assert.Equal(composed, decomposed);
		Assert.Equal("Thing%7CDef/Caf%C3%A9%2FBench", composed.CanonicalValue);
		Assert.Throws<ArgumentException>(() => new DefIdentity("ThingDef", " "));
	}

	[Fact]
	public void RequirementValuesSnapshotAndCanonicalizeAlternates()
	{
		var mutableAlternates = new List<DefIdentity>
		{
			new("ThingDef", "WallLamp"),
			new("ThingDef", "HiddenConduit"),
			new("ThingDef", "WallLamp"),
			TableButcher,
		};
		var requirement = RequirementSpec.ForThing(TableButcher, AlternateSubjectMode.Equivalent, mutableAlternates);

		mutableAlternates.Clear();
		Assert.Equal(
			new[] { "HiddenConduit", "WallLamp" },
			requirement.AlternateSubjects.Select(subject => subject.DefName));
		Assert.Throws<NotSupportedException>(() => ((IList<DefIdentity>)requirement.AlternateSubjects).Clear());
		Assert.Equal(
			requirement,
			RequirementSpec.ForThing(
				new DefIdentity("ThingDef", "TableButcher"),
				AlternateSubjectMode.Equivalent,
				new[] { new DefIdentity("ThingDef", "WallLamp"), new DefIdentity("ThingDef", "HiddenConduit") }));
	}

	[Fact]
	public void SpecificationAndProgressHaveSeparateLifetimes()
	{
		var mutableReasons = new List<GenerationReason> { LegacyReason("direct analysis") };
		var spec = new OpportunitySpec(
			Electricity,
			Analyse,
			ResearchRelation.Direct,
			RequirementSpec.ForThing(TableButcher),
			1f,
			rare: false,
			freebie: false,
			mutableReasons);
		var progress = new OpportunityProgressState(spec.Key, 10f, 25f);

		mutableReasons.Clear();
		Assert.Single(spec.Reasons);
		Assert.Equal(15f, progress.Apply(20f));
		Assert.Equal(25f, progress.CurrentProgress);
		Assert.Equal(spec.Key, progress.Key);
		Assert.Equal(1f, spec.Importance);
		Assert.Throws<ArgumentOutOfRangeException>(() => progress.Restore(float.NaN, 25f));
	}

	[Fact]
	public void ExplicitKeyMustMatchSemanticFields()
	{
		var wrongKey = Key(
			Electricity,
			new DefIdentity("ResearchOpportunityTypeDef", "BasicResearch"),
			ResearchRelation.Direct,
			RequirementSpec.Nothing());

		Assert.Throws<ArgumentException>(() => new OpportunitySpec(
			wrongKey,
			Electricity,
			Analyse,
			ResearchRelation.Direct,
			RequirementSpec.ForThing(TableButcher),
			1f,
			false,
			false));
	}

	[Fact]
	public void EvidenceAndDiagnosticReasonsRetainProvenance()
	{
		var recipe = new DefIdentity("RecipeDef", "Make_ComponentIndustrial");
		var evidence = new SubjectEvidence(
			Electricity,
			TableButcher,
			ResearchRelation.Direct,
			SubjectRole.ProductionFacility,
			EvidenceSource.RecipeDefinition,
			recipe,
			0.75f);
		var generated = new GenerationReason(
			GenerationReasonKind.Recipe,
			EvidenceSource.RecipeDefinition,
			recipe,
			"recipe user");
		var rejected = new RejectionReason(
			RejectionReasonKind.WeakEvidence,
			"Ingredient-filter membership alone is not a direct product relationship.",
			recipe);

		Assert.Equal(recipe, evidence.SourceDef);
		Assert.Equal(SubjectRole.ProductionFacility, evidence.Role);
		Assert.Equal(0.75f, evidence.Confidence);
		Assert.Equal(recipe, generated.SourceDef);
		Assert.Equal(recipe, rejected.SourceDef);
		Assert.Equal(
			evidence,
			new SubjectEvidence(
				new DefIdentity("ResearchProjectDef", "Electricity"),
				new DefIdentity("ThingDef", "TableButcher"),
				ResearchRelation.Direct,
				SubjectRole.ProductionFacility,
				EvidenceSource.RecipeDefinition,
				new DefIdentity("RecipeDef", "Make_ComponentIndustrial"),
				0.75f));
		Assert.Equal(
			generated,
			new GenerationReason(
				GenerationReasonKind.Recipe,
				EvidenceSource.RecipeDefinition,
				new DefIdentity("RecipeDef", "Make_ComponentIndustrial"),
				"recipe user"));
		Assert.Equal(
			rejected,
			new RejectionReason(
				RejectionReasonKind.WeakEvidence,
				"Ingredient-filter membership alone is not a direct product relationship.",
				new DefIdentity("RecipeDef", "Make_ComponentIndustrial")));
		Assert.Throws<ArgumentOutOfRangeException>(() => new SubjectEvidence(
			Electricity,
			TableButcher,
			ResearchRelation.Direct,
			SubjectRole.Unlock,
			EvidenceSource.ProjectDefinition,
			Electricity,
			1.1f));
	}

	[Fact]
	public void Phase0FixturesMapToAllLegacyRequirementValuesAndStableKeys()
	{
		var opportunities = new[]
		{
			"phase0/legacy-generator-minimal.json",
			"phase0/legacy-generator-compat-ferny-butcher-table.json",
		}
			.Select(Phase0Fixture.Load)
			.SelectMany(fixture => fixture.RequiredArray("projects").Objects())
			.SelectMany(project => project.RequiredArray("opportunities").Objects())
			.Select(opportunity => new
			{
				SemanticTuple = SemanticTuple(opportunity),
				Spec = SpecFromFixture(opportunity),
			})
			.ToArray();

		Assert.Equal(579, opportunities.Length);
		Assert.Equal(
			Enum.GetValues<RequirementKind>().Order(),
			opportunities.Select(value => value.Spec.Requirement.Kind).Distinct().Order());
		Assert.All(opportunities, value => Assert.Equal(value.Spec.Key, OpportunityKey.Parse(value.Spec.Key.Value)));
		Assert.All(
			opportunities.GroupBy(value => value.SemanticTuple, StringComparer.Ordinal),
			group => Assert.Single(group.Select(value => value.Spec.Key).Distinct()));
		Assert.All(
			opportunities.GroupBy(value => value.Spec.Key),
			group => Assert.Single(group.Select(value => value.SemanticTuple).Distinct(StringComparer.Ordinal)));

		var butcherTable = opportunities.Single(value =>
			value.Spec.Project.DefName == "Ferny_ButcherTable"
			&& value.Spec.Type.DefName == "Analyse"
			&& value.Spec.Requirement.CanonicalSubject.DefName == "TableButcher");
		Assert.Equal(
			"rr1|project=ResearchProjectDef/Ferny_ButcherTable|type=ResearchOpportunityTypeDef/Analyse|relation=Direct|requirement=Thing|subject=ThingDef/TableButcher",
			butcherTable.Spec.Key.Value);
		Assert.Equal(new[] { "AncientTableButcher" }, butcherTable.Spec.Requirement.AlternateSubjects.Select(subject => subject.DefName));
	}

	private static OpportunitySpec SpecFromFixture(JsonObject opportunity)
	{
		var project = new DefIdentity("ResearchProjectDef", opportunity.RequiredString("project"));
		var type = new DefIdentity("ResearchOpportunityTypeDef", opportunity.RequiredString("type"));
		var relation = Enum.Parse<ResearchRelation>(opportunity.RequiredString("relation"));
		var requirement = RequirementFromFixture(opportunity.RequiredObject("requirement"));
		return new OpportunitySpec(
			project,
			type,
			relation,
			requirement,
			opportunity.RequiredFloat("importance"),
			opportunity.RequiredBool("rare"),
			opportunity.RequiredBool("freebie"),
			new[] { LegacyReason(opportunity.RequiredString("generationSource"), project) });
	}

	private static RequirementSpec RequirementFromFixture(JsonObject requirement)
	{
		var identity = requirement.RequiredString("canonicalIdentity");
		var alternates = requirement.RequiredArray("alternates").Select(value => value!.GetValue<string>()).ToArray();
		switch (requirement.RequiredString("kind"))
		{
			case "ROComp_RequiresNothing":
				return RequirementSpec.Nothing();
			case "ROComp_RequiresThing":
				return RequirementSpec.ForThing(
					new DefIdentity("ThingDef", identity),
					alternates.Length == 0 ? AlternateSubjectMode.None : AlternateSubjectMode.Equivalent,
					alternates.Select(name => new DefIdentity("ThingDef", name)));
			case "ROComp_RequiresTerrain":
				return RequirementSpec.ForTerrain(
					new DefIdentity("TerrainDef", identity),
					alternates.Length == 0 ? AlternateSubjectMode.None : AlternateSubjectMode.Equivalent,
					alternates.Select(name => new DefIdentity("TerrainDef", name)));
			case "ROComp_RequiresRecipe":
				return RequirementSpec.ForRecipe(
					new DefIdentity("RecipeDef", identity),
					alternates.Length == 0 ? AlternateSubjectMode.None : AlternateSubjectMode.Equivalent,
					alternates.Select(name => new DefIdentity("RecipeDef", name)));
			case "ROComp_RequiresFaction":
				return RequirementSpec.ForFaction(new DefIdentity("FactionDef", identity));
			case "ROComp_RequiresFactionlessPawn":
				return RequirementSpec.FactionlessPawn();
			case "ROComp_RequiresSchematicWithProject":
				return RequirementSpec.ForSchematic(new DefIdentity("ResearchProjectDef", identity));
			default:
				throw new InvalidDataException($"Unknown fixture requirement kind: {requirement.RequiredString("kind")}");
		}
	}

	private static string SemanticTuple(JsonObject opportunity)
	{
		var requirement = opportunity.RequiredObject("requirement");
		return string.Join(
			"|",
			opportunity.RequiredString("project"),
			opportunity.RequiredString("type"),
			opportunity.RequiredString("relation"),
			requirement.RequiredString("kind"),
			requirement.RequiredString("canonicalIdentity"));
	}

	private static OpportunityKey Key(
		DefIdentity project,
		DefIdentity type,
		ResearchRelation relation,
		RequirementSpec requirement)
	{
		return OpportunityKey.Create(project, type, relation, requirement);
	}

	private static GenerationReason LegacyReason(string detail, DefIdentity? source = null)
	{
		return new GenerationReason(
			GenerationReasonKind.LegacyGenerator,
			EvidenceSource.LegacyAdapter,
			source ?? Electricity,
			detail);
	}
}
