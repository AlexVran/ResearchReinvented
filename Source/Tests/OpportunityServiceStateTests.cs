using System.Text.Json.Nodes;
using PeteTimesSix.ResearchReinvented.Domain;
using PeteTimesSix.ResearchReinvented.Domain.State;
using PeteTimesSix.ResearchReinvented.Opportunities;
using Xunit;

namespace PeteTimesSix.ResearchReinvented.Tests;

public sealed class OpportunityServiceStateTests
{
	public static IEnumerable<object[]> Phase0SaveFixtures => new[]
	{
		new object[] { "phase0/legacy-save-ferny-workbench.json", 89 },
		new object[] { "phase0/legacy-save-ferny-butcher-table-partial.json", 106 },
	};

	[Fact]
	public void NewSchemaRoundTripRetainsStableKeyProgressBudgetsAndSpecialState()
	{
		var fixture = Specification("ProjectA", "Analyse", "ThingA", "ReverseEngineering", 25f);
		var prototype = Specification("ProjectA", "PrototypeConstruction", "BenchA", "Prototyping", 40f, PreservedOpportunityStateKind.Prototype);
		var service = new OpportunityService();
		service.SetSpecifications(Project("ProjectA"), new[] { fixture, prototype }, Budgets("ProjectA", ("ReverseEngineering", 100f), ("Prototyping", 120f)));
		service.ActiveProject = Project("ProjectA");
		Assert.Equal(11f, service.ApplyProgress(fixture.Spec.Key, 11f));
		Assert.Equal(17f, service.ApplyProgress(prototype.Spec.Key, 17f));

		var saved = service.CreateSnapshot(settingsChangeTicker: 7);
		Assert.Equal(OpportunitySaveSnapshot.CurrentSchemaVersion, saved.SchemaVersion);
		Assert.Single(saved.Progress);
		Assert.Single(saved.SpecialState);
		Assert.True(saved.RequiresSpecificationRegeneration);

		var restored = new OpportunityService();
		restored.Restore(saved);
		restored.SetSpecifications(Project("ProjectA"), new[] { prototype, fixture }.Reverse(), saved.CategoryBudgets);

		Assert.Equal(11f, restored.ProgressFor(fixture.Spec.Key));
		Assert.Equal(17f, restored.ProgressFor(prototype.Spec.Key));
		Assert.Equal(28f, restored.CategoryProgress(Project("ProjectA"), Category("Prototyping")) + restored.CategoryProgress(Project("ProjectA"), Category("ReverseEngineering")));
		Assert.Equal(saved.SettingsChangeTicker, restored.CreateSnapshot(7).SettingsChangeTicker);
	}

	[Theory]
	[MemberData(nameof(Phase0SaveFixtures))]
	public void EveryPhase0LegacySaveFixtureMigratesAllRecords(string fixtureName, int expectedCount)
	{
		var migrated = LoadLegacyFixture(fixtureName);
		var service = new OpportunityService();
		service.RestoreLegacy(Dtos(migrated.State), migrated.Budgets, migrated.ActiveProject, new[] { migrated.ActiveProject }, settingsChangeTicker: 4);
		service.SetSpecifications(migrated.ActiveProject, migrated.Specifications.Reverse(), migrated.Budgets);

		Assert.Equal(OpportunityStateLoadStatus.MigratedLegacy, service.LoadStatus);
		Assert.Equal(expectedCount, service.SpecificationsFor(migrated.ActiveProject).Count);
		Assert.Empty(service.Orphans);
		Assert.Equal(
			migrated.State.Sum(item => item.CurrentProgress),
			migrated.Specifications.Sum(item => service.ProgressFor(item.Spec.Key)),
			precision: 3);
		var rewritten = service.CreateSnapshot(4);
		Assert.Equal(OpportunitySaveSnapshot.CurrentSchemaVersion, rewritten.SchemaVersion);
		Assert.Equal(expectedCount, rewritten.Progress.Count + rewritten.SpecialState.Count);
	}

	[Fact]
	public void Phase0PartialProgressSurvivesSemanticMigration()
	{
		var migrated = LoadLegacyFixture("phase0/legacy-save-ferny-butcher-table-partial.json");
		var partial = migrated.State.Single(item => item.CurrentProgress > 0f && item.CurrentProgress < item.MaximumProgress);
		var service = new OpportunityService();
		service.RestoreLegacy(Dtos(migrated.State), migrated.Budgets, migrated.ActiveProject, new[] { migrated.ActiveProject }, 4);
		service.SetSpecifications(migrated.ActiveProject, migrated.Specifications, migrated.Budgets);

		var key = migrated.Specifications.Single(item =>
			item.Spec.Type.DefName == "Analyse"
			&& item.Spec.Relation == ResearchRelation.Ancestor
			&& item.Spec.Requirement.CanonicalSubject.DefName == "VFE_CoffeeTable").Spec.Key;
		Assert.InRange(service.ProgressFor(key), 10.9743f, 10.9744f);
		Assert.Equal(partial.CurrentProgress, service.ProgressFor(key));
	}

	[Fact]
	public void RemovedModAndMissingDefinitionsBecomeCreditedOrphans()
	{
		var removed = Specification("RemovedProject", "Analyse", "RemovedThing", "Material", 30f);
		var record = SavedOpportunityState.FromSpecification(removed, 19f);
		var service = new OpportunityService();
		service.RestoreLegacy(Dtos(new[] { record }), Budgets("RemovedProject", ("Material", 50f)), Project("RemovedProject"), new[] { Project("RemovedProject") }, 0);
		service.SetSpecifications(Project("RemovedProject"), Array.Empty<OpportunitySpecificationState>(), Budgets("RemovedProject", ("Material", 50f)));

		Assert.Single(service.Orphans);
		Assert.Equal(19f, service.CategoryProgress(Project("RemovedProject"), Category("Material")));
		Assert.Contains(service.Diagnostics, item => item.Kind == "Orphaned");
		Assert.Single(service.CreateSnapshot(0).Progress);
	}

	[Fact]
	public void UnparseableLegacyRecordIsRetainedAsAnOrphanInsteadOfDropped()
	{
		var broken = new SavedOpportunityState(null, null, null, ResearchRelation.Direct, RequirementKind.None, null, null, 6f, 9f, sourceId: "legacy-404");
		var service = new OpportunityService();
		service.RestoreLegacy(Dtos(new[] { broken }), Array.Empty<SavedCategoryBudget>(), null, null, 0);

		Assert.Single(service.Orphans);
		Assert.Equal(6f, service.Orphans[0].CurrentProgress);
		Assert.Contains(service.Diagnostics, item => item.Kind == "MissingProjectIdentity");
		Assert.Single(service.CreateSnapshot(0).Progress);
	}

	[Fact]
	public void MigratedCategoryBudgetsAndTotalsRemainExact()
	{
		var migrated = LoadLegacyFixture("phase0/legacy-save-ferny-butcher-table-partial.json");
		var service = new OpportunityService();
		service.RestoreLegacy(Dtos(migrated.State), migrated.Budgets, migrated.ActiveProject, new[] { migrated.ActiveProject }, 4);
		service.SetSpecifications(migrated.ActiveProject, migrated.Specifications, migrated.Budgets);
		var saved = service.CreateSnapshot(4);
		var budgets = saved.CategoryBudgets.ToDictionary(item => item.Category.DefName, item => item.Budget, StringComparer.Ordinal);

		Assert.Equal(8, budgets.Count);
		Assert.Equal(1f, budgets["Theory"]);
		Assert.Equal(150f, budgets["ForwardEngineering"]);
		Assert.Equal(180f, budgets["Prototyping"]);
		foreach (var category in migrated.State.Select(item => item.Category).Where(item => item != null).Distinct())
		{
			var expected = migrated.State.Where(item => item.Category == category).Sum(item => item.CurrentProgress);
			Assert.Equal(expected, service.CategoryProgress(migrated.ActiveProject, category!), precision: 3);
		}
	}

	[Fact]
	public void ExplicitRenameAliasesReconcileProjectTypeAndSubjectDeterministically()
	{
		var old = Specification("OldProject", "OldAnalyse", "OldThing", "Material", 30f);
		var replacement = Specification("NewProject", "NewAnalyse", "NewThing", "Material", 35f);
		var record = SavedOpportunityState.FromSpecification(old, 12f);
		var aliases = new OpportunityReconciliationAliases(new[]
		{
			Pair(Project("OldProject"), Project("NewProject")),
			Pair(Type("OldAnalyse"), Type("NewAnalyse")),
			Pair(Thing("OldThing"), Thing("NewThing")),
		});
		var service = new OpportunityService();
		service.RestoreLegacy(Dtos(new[] { record }), Array.Empty<SavedCategoryBudget>(), Project("NewProject"), new[] { Project("NewProject") }, 0);
		service.SetSpecifications(Project("NewProject"), new[] { replacement }, Array.Empty<SavedCategoryBudget>(), aliases);

		Assert.Equal(12f, service.ProgressFor(replacement.Spec.Key));
		Assert.Empty(service.Orphans);
	}

	[Fact]
	public void RelationOrRequirementMismatchNeverReceivesUnrelatedProgress()
	{
		var direct = Specification("ProjectA", "Analyse", "ThingA", "Material", 20f);
		var saved = SavedOpportunityState.FromSpecification(direct, 13f);
		var ancestorSpec = new OpportunitySpec(direct.Spec.Project, direct.Spec.Type, ResearchRelation.Ancestor, direct.Spec.Requirement, 1f, false, false);
		var ancestor = new OpportunitySpecificationState(ancestorSpec, direct.Category, 20f);
		var service = new OpportunityService();
		service.RestoreLegacy(Dtos(new[] { saved }), Array.Empty<SavedCategoryBudget>(), Project("ProjectA"), new[] { Project("ProjectA") }, 0);
		service.SetSpecifications(Project("ProjectA"), new[] { ancestor }, Array.Empty<SavedCategoryBudget>());

		Assert.Equal(0f, service.ProgressFor(ancestor.Spec.Key));
		Assert.Single(service.Orphans);
		Assert.Equal(13f, service.CategoryProgress(Project("ProjectA"), Category("Material")));
	}

	[Fact]
	public void DuplicateSemanticStateIsMergedWithoutTruncatingResearch()
	{
		var spec = Specification("ProjectA", "Analyse", "ThingA", "Material", 10f);
		var first = SavedOpportunityState.FromSpecification(spec, 7f);
		var second = new SavedOpportunityState(null, spec.Spec.Project, spec.Spec.Type, spec.Spec.Relation, spec.Spec.Requirement.Kind, spec.Spec.Requirement.CanonicalSubject, spec.Category, 8f, 10f, sourceId: "duplicate");
		var service = new OpportunityService();
		service.RestoreLegacy(Dtos(new[] { second, first }), Array.Empty<SavedCategoryBudget>(), Project("ProjectA"), new[] { Project("ProjectA") }, 0);
		service.SetSpecifications(Project("ProjectA"), new[] { spec }, Array.Empty<SavedCategoryBudget>());

		Assert.Equal(15f, service.ProgressFor(spec.Spec.Key));
		Assert.Equal(10f, service.MaximumFor(spec.Spec.Key));
		Assert.Contains(service.Diagnostics, item => item.Kind == "DuplicateMerged");
	}

	[Fact]
	public void ReconciliationAndSnapshotOrderingAreDeterministic()
	{
		var specs = new[]
		{
			Specification("ProjectA", "Analyse", "ThingB", "Material", 20f),
			Specification("ProjectA", "Analyse", "ThingA", "Material", 20f),
		};
		var records = specs.Select((item, index) => SavedOpportunityState.FromSpecification(item, index + 3f)).ToArray();
		var forward = Reconcile(specs, records);
		var reverse = Reconcile(specs.Reverse(), records.Reverse());

		Assert.Equal(
			forward.CreateSnapshot(0).Progress.Select(item => item.StableSortKey),
			reverse.CreateSnapshot(0).Progress.Select(item => item.StableSortKey));
		Assert.Equal(
			forward.CreateSnapshot(0).Progress.Select(item => item.CurrentProgress),
			reverse.CreateSnapshot(0).Progress.Select(item => item.CurrentProgress));
	}

	[Fact]
	public void ProjectSwitchThenSaveLoadRetainsIndependentProgress()
	{
		var first = Specification("ProjectA", "Analyse", "ThingA", "Material", 20f);
		var second = Specification("ProjectB", "Analyse", "ThingB", "Material", 20f);
		var service = new OpportunityService();
		service.SetSpecifications(Project("ProjectA"), new[] { first }, Array.Empty<SavedCategoryBudget>());
		service.ApplyProgress(first.Spec.Key, 4f);
		service.SetSpecifications(Project("ProjectB"), new[] { second }, Array.Empty<SavedCategoryBudget>());
		service.ApplyProgress(second.Spec.Key, 9f);
		service.ActiveProject = Project("ProjectB");

		var saved = service.CreateSnapshot(2);
		var restored = new OpportunityService();
		restored.Restore(saved);
		restored.SetSpecifications(Project("ProjectB"), new[] { second }, Array.Empty<SavedCategoryBudget>());
		restored.SetSpecifications(Project("ProjectA"), new[] { first }, Array.Empty<SavedCategoryBudget>());

		Assert.Equal(Project("ProjectB"), restored.ActiveProject);
		Assert.Equal(4f, restored.ProgressFor(first.Spec.Key));
		Assert.Equal(9f, restored.ProgressFor(second.Spec.Key));
	}

	[Fact]
	public void SettingsDrivenRegenerationUpdatesBudgetsAndMaximumsWithoutResettingProgress()
	{
		var before = Specification("ProjectA", "Analyse", "ThingA", "Material", 20f);
		var service = new OpportunityService();
		service.SetSpecifications(Project("ProjectA"), new[] { before }, Budgets("ProjectA", ("Material", 100f)));
		service.ApplyProgress(before.Spec.Key, 7f);
		var after = Specification("ProjectA", "Analyse", "ThingA", "Material", 45f);

		service.SetSpecifications(Project("ProjectA"), new[] { after }, Budgets("ProjectA", ("Material", 225f)));

		Assert.Equal(7f, service.ProgressFor(after.Spec.Key));
		Assert.Equal(45f, service.MaximumFor(after.Spec.Key));
		Assert.Equal(225f, service.CreateSnapshot(9).CategoryBudgets.Single(item => item.Category == Category("Material")).Budget);
	}

	[Fact]
	public void MalformedProgressIsQuarantinedAndFutureSchemaIsPreservedReadOnly()
	{
		var spec = Specification("ProjectA", "Analyse", "ThingA", "Material", 20f);
		var malformed = new SavedOpportunityState(null, spec.Spec.Project, spec.Spec.Type, spec.Spec.Relation, spec.Spec.Requirement.Kind, spec.Spec.Requirement.CanonicalSubject, spec.Category, float.NaN, -4f, sourceId: "broken");
		var service = new OpportunityService();
		service.Restore(new OpportunitySaveSnapshot(0, Project("ProjectA"), new[] { Project("ProjectA") }, new[] { malformed }, null, null));
		service.SetSpecifications(Project("ProjectA"), new[] { spec }, Array.Empty<SavedCategoryBudget>());
		Assert.Equal(OpportunityStateLoadStatus.SalvagedMalformed, service.LoadStatus);
		Assert.Single(service.Orphans);
		Assert.Equal(0f, service.CategoryProgress(Project("ProjectA"), Category("Material")));

		var future = new OpportunitySaveSnapshot(99, Project("ProjectA"), new[] { Project("ProjectA") }, new[] { SavedOpportunityState.FromSpecification(spec, 8f) }, null, null);
		var older = new OpportunityService();
		older.Restore(future);
		older.SetSpecifications(Project("ProjectA"), new[] { spec }, Array.Empty<SavedCategoryBudget>());
		Assert.True(older.IsReadOnly);
		Assert.Equal(0f, older.ApplyProgress(spec.Spec.Key, 3f));
		Assert.Same(future, older.CreateSnapshot(0));
	}

	[Fact]
	public void MigrationIsIdempotentAndKeepsSpecialStateSeparate()
	{
		var ordinary = Specification("ProjectA", "Analyse", "ThingA", "Material", 20f);
		var special = Specification("ProjectA", "TrialDrug", "DrugA", "Clinical", 30f, PreservedOpportunityStateKind.Special);
		var prototype = Specification("ProjectA", "PrototypeProduction", "RecipeA", "Prototyping", 40f, PreservedOpportunityStateKind.Prototype, RequirementKind.Recipe);
		var records = new[]
		{
			SavedOpportunityState.FromSpecification(ordinary, 3f),
			SavedOpportunityState.FromSpecification(special, 5f),
			SavedOpportunityState.FromSpecification(prototype, 7f),
		};
		var first = Reconcile(new[] { prototype, ordinary, special }, records);
		var firstSnapshot = first.CreateSnapshot(6);
		var second = new OpportunityService();
		second.Restore(firstSnapshot);
		second.SetSpecifications(Project("ProjectA"), new[] { special, prototype, ordinary }, Array.Empty<SavedCategoryBudget>());
		var secondSnapshot = second.CreateSnapshot(6);

		Assert.Single(secondSnapshot.Progress);
		Assert.Equal(2, secondSnapshot.SpecialState.Count);
		Assert.Equal(firstSnapshot.Progress.Select(StateTuple), secondSnapshot.Progress.Select(StateTuple));
		Assert.Equal(firstSnapshot.SpecialState.Select(StateTuple), secondSnapshot.SpecialState.Select(StateTuple));
	}

	[Fact]
	public void ServiceAcceptsCuratedOrHandlerBackedSpecificationsWithoutKnowingTheirExecutionModel()
	{
		var futureCurated = Specification("FutureProject", "CuratedActivity", "RegisteredEvent", "Fieldwork", 50f, PreservedOpportunityStateKind.Special);
		var service = new OpportunityService();
		service.SetSpecifications(Project("FutureProject"), new[] { futureCurated }, Budgets("FutureProject", ("Fieldwork", 50f)));
		Assert.Equal(8f, service.ApplyProgress(futureCurated.Spec.Key, 8f));

		var saved = service.CreateSnapshot(0);
		Assert.Single(saved.SpecialState);
		Assert.Equal(futureCurated.Spec.Key.Value, saved.SpecialState[0].Key);
	}

	private static OpportunityService Reconcile(IEnumerable<OpportunitySpecificationState> specs, IEnumerable<SavedOpportunityState> records)
	{
		var service = new OpportunityService();
		service.RestoreLegacy(Dtos(records), Array.Empty<SavedCategoryBudget>(), Project("ProjectA"), new[] { Project("ProjectA") }, 0);
		service.SetSpecifications(Project("ProjectA"), specs, Array.Empty<SavedCategoryBudget>());
		return service;
	}

	private static (string? Key, float Current, float Maximum) StateTuple(SavedOpportunityState state) => (state.Key, state.CurrentProgress, state.MaximumProgress);

	private static LegacyFixtureMigration LoadLegacyFixture(string fixtureName)
	{
		var fixture = Phase0Fixture.Load(fixtureName);
		var stateRoot = fixture.RequiredObject("state");
		var active = Project(stateRoot.RequiredString("opportunityManagerProject"));
		var states = new List<SavedOpportunityState>();
		var specs = new List<OpportunitySpecificationState>();
		foreach (var item in stateRoot.RequiredArray("opportunities").Objects())
		{
			var project = Project(item.RequiredString("project"));
			var type = Type(item.RequiredString("type"));
			var category = Category(item.RequiredString("category"));
			var relation = Enum.Parse<ResearchRelation>(item.RequiredString("relation"));
			var requirement = Requirement(item.RequiredObject("requirement"));
			var opportunity = new OpportunitySpec(project, type, relation, requirement, item.RequiredFloat("importance"), false, false);
			var kind = StateKind(type.DefName);
			var specification = new OpportunitySpecificationState(opportunity, category, item.RequiredFloat("maximumProgress"), kind);
			specs.Add(specification);
			states.Add(new SavedOpportunityState(
				null,
				project,
				type,
				relation,
				requirement.Kind,
				requirement.CanonicalSubject,
				category,
				item.RequiredFloat("currentProgress"),
				item.RequiredFloat("maximumProgress"),
				kind,
				requirement.AlternateSubjects,
				item.RequiredInt("legacyLoadId").ToString()));
		}
		var budgets = stateRoot.RequiredArray("categoryStores").Objects().Select(item =>
			new SavedCategoryBudget(Project(item.RequiredString("project")), Category(item.RequiredString("category")), item.RequiredFloat("researchPoints"))).ToArray();
		return new LegacyFixtureMigration(active, states, specs, budgets);
	}

	private static RequirementSpec Requirement(JsonObject requirement)
	{
		var kind = requirement.RequiredString("kind");
		var facts = requirement["facts"] as JsonObject;
		var target = facts?["targetDef"]?.GetValue<string>();
		return kind switch
		{
			"ROComp_RequiresNothing" => RequirementSpec.Nothing(),
			"ROComp_RequiresThing" => RequirementSpec.ForThing(Thing(target!), Alternate(facts)),
			"ROComp_RequiresTerrain" => RequirementSpec.ForTerrain(Terrain(facts?["terrainDef"]?.GetValue<string>() ?? target!), Alternate(facts)),
			"ROComp_RequiresRecipe" => RequirementSpec.ForRecipe(Recipe(target!), Alternate(facts)),
			"ROComp_RequiresFaction" => RequirementSpec.ForFaction(new DefIdentity("FactionDef", facts?["faction"]?.GetValue<string>() ?? "missing-faction")),
			"ROComp_RequiresFactionlessPawn" => RequirementSpec.FactionlessPawn(),
			"ROComp_RequiresSchematicWithProject" => RequirementSpec.ForSchematic(Project(facts?["projectDef"]?.GetValue<string>() ?? facts?["project"]?.GetValue<string>() ?? "missing-project")),
			_ => throw new InvalidDataException("Unknown legacy requirement " + kind),
		};
	}

	private static AlternateSubjectMode Alternate(JsonObject? facts) =>
		Enum.TryParse<AlternateSubjectMode>(facts?["altsMode"]?.GetValue<string>(), ignoreCase: true, out var value) ? value : AlternateSubjectMode.None;

	private static OpportunitySpecificationState Specification(
		string project,
		string type,
		string subject,
		string category,
		float maximum,
		PreservedOpportunityStateKind stateKind = PreservedOpportunityStateKind.Ordinary,
		RequirementKind requirementKind = RequirementKind.Thing)
	{
		var requirement = requirementKind == RequirementKind.Recipe ? RequirementSpec.ForRecipe(Recipe(subject)) : RequirementSpec.ForThing(Thing(subject));
		return new OpportunitySpecificationState(new OpportunitySpec(Project(project), Type(type), ResearchRelation.Direct, requirement, 1f, false, false), Category(category), maximum, stateKind);
	}

	private static PreservedOpportunityStateKind StateKind(string type) =>
		type.StartsWith("Prototype", StringComparison.Ordinal) ? PreservedOpportunityStateKind.Prototype
			: type is "TrialDrug" ? PreservedOpportunityStateKind.Special
			: PreservedOpportunityStateKind.Ordinary;

	private static SavedCategoryBudget[] Budgets(string project, params (string Category, float Budget)[] values) =>
		values.Select(value => new SavedCategoryBudget(Project(project), Category(value.Category), value.Budget)).ToArray();
	private static LegacyOpportunityMigrationDto[] Dtos(IEnumerable<SavedOpportunityState> values) =>
		values.Select(value => new LegacyOpportunityMigrationDto(value, int.TryParse(value.SourceId, out var id) ? id : null)).ToArray();
	private static KeyValuePair<DefIdentity, DefIdentity> Pair(DefIdentity from, DefIdentity to) => new(from, to);
	private static DefIdentity Project(string name) => new("ResearchProjectDef", name);
	private static DefIdentity Type(string name) => new("ResearchOpportunityTypeDef", name);
	private static DefIdentity Category(string name) => new("ResearchOpportunityCategoryDef", name);
	private static DefIdentity Thing(string name) => new("ThingDef", name);
	private static DefIdentity Terrain(string name) => new("TerrainDef", name);
	private static DefIdentity Recipe(string name) => new("RecipeDef", name);

	private sealed record LegacyFixtureMigration(
		DefIdentity ActiveProject,
		IReadOnlyList<SavedOpportunityState> State,
		IReadOnlyList<OpportunitySpecificationState> Specifications,
		IReadOnlyList<SavedCategoryBudget> Budgets);
}
