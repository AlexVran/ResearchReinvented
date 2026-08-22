using Xunit;

namespace PeteTimesSix.ResearchReinvented.Tests;

public sealed class DeferredRegressionContractTests
{
	[Fact]
	public void LaterPhaseDefectsRemainExplicitAndUnfixedInPhase2()
	{
		var fixture = Phase0Fixture.Load("phase2/deferred-regressions.json");
		var cases = fixture.RequiredArray("cases").Objects().ToDictionary(
			value => value.RequiredString("id"),
			value => value.RequiredString("ownerPhase"),
			StringComparer.Ordinal);
		var expected = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["github-33-melee-weapon-cache"] = "9",
			["github-24-settings-scroll"] = "10",
			["github-22-missing-xml-parent"] = "12",
			["github-20-bills-vanish"] = "11",
			["github-19-combat-extended-patch"] = "11-12",
			["argonic-core-prototype-completion"] = "11",
			["optional-anomaly-study"] = "9-13",
		};

		Assert.Equal("research-reinvented-deferred-regressions-v1", fixture.RequiredString("schema"));
		Assert.False(fixture.RequiredBool("fixAllowedInPhase2"));
		Assert.Equal(expected.OrderBy(pair => pair.Key), cases.OrderBy(pair => pair.Key));
		Assert.All(fixture.RequiredArray("cases").Objects(), value =>
			Assert.False(string.IsNullOrWhiteSpace(value.RequiredString("phase2Evidence"))));
	}

	[Fact]
	public void Phase0SaveEvidenceDoesNotInventAnActivePrototypeFixture()
	{
		var butcher = Phase0Fixture.Load("phase0/legacy-save-ferny-butcher-table-partial.json");
		var workbench = Phase0Fixture.Load("phase0/legacy-save-ferny-workbench.json");

		Assert.Empty(butcher.RequiredObject("state").RequiredArray("prototypeReferenceIds"));
		Assert.Empty(workbench.RequiredObject("state").RequiredArray("prototypeReferenceIds"));
	}
}
