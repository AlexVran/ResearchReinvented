using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace PeteTimesSix.ResearchReinvented.Tests;

public sealed class LegacySaveCharacterizationTests
{
	public static IEnumerable<object[]> SaveFixtures => new[]
	{
		new object[] { "phase0/legacy-save-ferny-workbench.json", "Ferny_Workbench", 89, 89, 177 },
		new object[] { "phase0/legacy-save-ferny-butcher-table-partial.json", "Ferny_ButcherTable", 106, 178, 283 },
	};

	public static IEnumerable<object[]> SaveFixtureNames =>
		SaveFixtures.Select(values => new[] { values[0] });

	[Theory]
	[MemberData(nameof(SaveFixtures))]
	public void LegacyManagerStateRetainsStableContiguousLoadIds(
		string fixtureName,
		string project,
		int opportunityCount,
		int firstLoadId,
		int lastLoadId)
	{
		var fixture = Phase0Fixture.Load(fixtureName);
		var state = fixture.RequiredObject("state");
		var opportunities = state.RequiredArray("opportunities").Objects().ToArray();
		var loadIds = opportunities.Select(opportunity => opportunity.RequiredInt("legacyLoadId")).ToArray();

		Assert.Equal("research-reinvented-legacy-save-baseline-v1", fixture.RequiredString("schema"));
		Assert.Equal(project, state.RequiredString("researchManagerProject"));
		Assert.Equal(project, state.RequiredString("opportunityManagerProject"));
		Assert.Equal(new[] { project }, state.RequiredArray("generatedProjects").Select(value => value!.GetValue<string>()));
		Assert.Equal(opportunityCount, opportunities.Length);
		Assert.Equal(Enumerable.Range(firstLoadId, opportunityCount), loadIds);
		Assert.Equal(lastLoadId, loadIds[^1]);
		Assert.Equal(loadIds.Length, loadIds.Distinct().Count());
		Assert.Empty(state.RequiredArray("prototypeReferenceIds"));
	}

	[Fact]
	public void PartialAncestorProgressRemainsAttachedToItsSemanticSubject()
	{
		var fixture = Phase0Fixture.Load("phase0/legacy-save-ferny-butcher-table-partial.json");
		var partial = fixture.RequiredObject("state").RequiredArray("opportunities").Objects()
			.Single(opportunity =>
				opportunity.RequiredFloat("currentProgress") > 0f
				&& opportunity.RequiredFloat("currentProgress") < opportunity.RequiredFloat("maximumProgress"));
		var facts = partial.RequiredObject("requirement").RequiredObject("facts");

		Assert.Equal("Ferny_ButcherTable", partial.RequiredString("project"));
		Assert.Equal("Analyse", partial.RequiredString("type"));
		Assert.Equal("Ancestor", partial.RequiredString("relation"));
		Assert.Equal("ForwardEngineering", partial.RequiredString("category"));
		Assert.Equal("ROComp_RequiresThing", partial.RequiredObject("requirement").RequiredString("kind"));
		Assert.Equal("VFE_CoffeeTable", facts.RequiredString("targetDef"));
		Assert.InRange(partial.RequiredFloat("currentProgress"), 10.9743f, 10.9744f);
		Assert.InRange(partial.RequiredFloat("maximumProgress"), 16.6666f, 16.6667f);
	}

	[Theory]
	[MemberData(nameof(SaveFixtures))]
	public void HeadlessSaveSnapshotRoundTripsWithoutSemanticLoss(
		string fixtureName,
		string project,
		int opportunityCount,
		int firstLoadId,
		int lastLoadId)
	{
		var snapshot = LegacySaveStateSnapshot.From(Phase0Fixture.Load(fixtureName));
		var serialized = JsonSerializer.Serialize(snapshot);
		var restored = JsonSerializer.Deserialize<LegacySaveStateSnapshot>(serialized);

		Assert.NotNull(restored);
		Assert.Equal(serialized, JsonSerializer.Serialize(restored));
		Assert.Equal(project, restored.ResearchManagerProject);
		Assert.Equal(opportunityCount, restored.Opportunities.Length);
		Assert.Equal(firstLoadId, restored.Opportunities[0].LegacyLoadId);
		Assert.Equal(lastLoadId, restored.Opportunities[^1].LegacyLoadId);
	}

	[Theory]
	[MemberData(nameof(SaveFixtureNames))]
	public void LegacyForcedFlagSerializationDefaultsRemainCharacterized(string fixtureName)
	{
		var fixture = Phase0Fixture.Load(fixtureName);
		var notes = fixture.RequiredObject("serializationNotes");
		var opportunities = fixture.RequiredObject("state").RequiredArray("opportunities").Objects();

		Assert.True(notes.RequiredBool("forcedFreebieIsNotSerializedByLegacyCode"));
		Assert.True(notes.RequiredBool("forcedRareIsOnlyWrittenWhenTrue"));
		Assert.True(notes.RequiredBool("relationDefaultsToDirectWhenAbsent"));
		Assert.All(opportunities, opportunity =>
		{
			Assert.Null(opportunity["forcedRarePersisted"]);
			Assert.Null(opportunity["forcedFreebiePersisted"]);
		});
	}

	[Fact]
	public void SavedCategoryBudgetsRetainProjectAndSettingsContext()
	{
		var fixture = Phase0Fixture.Load("phase0/legacy-save-ferny-butcher-table-partial.json");
		var state = fixture.RequiredObject("state");
		var stores = state.RequiredArray("categoryStores").Objects().ToDictionary(
			store => store.RequiredString("category"),
			store => store.RequiredFloat("researchPoints"),
			StringComparer.Ordinal);
		var settings = fixture.RequiredObject("settings").RequiredObject("settings");

		Assert.Equal(8, stores.Count);
		Assert.Equal(1f, stores["Theory"]);
		Assert.Equal(150f, stores["ForwardEngineering"]);
		Assert.Equal(180f, stores["Prototyping"]);
		Assert.Equal(300f, stores["Books"]);
		Assert.Equal("SettingsPreset_RR_Default", settings.RequiredString("activePreset"));
		Assert.Equal(3, settings.RequiredInt("changeTicker"));
		Assert.Equal(4, state.RequiredInt("changeTicker"));
	}
}
