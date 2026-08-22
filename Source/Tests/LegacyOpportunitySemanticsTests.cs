using PeteTimesSix.ResearchReinvented.Opportunities;
using Xunit;

namespace PeteTimesSix.ResearchReinvented.Tests;

public sealed class LegacyOpportunitySemanticsTests
{
	[Theory]
	[InlineData(false, false, false)]
	[InlineData(false, true, true)]
	[InlineData(true, false, true)]
	[InlineData(true, true, true)]
	public void ForcedAndRequirementFlagsUseLegacyOrSemantics(bool forced, bool requirementFlag, bool expected)
	{
		Assert.Equal(expected, LegacyOpportunitySemantics.ResolveFlag(forced, requirementFlag));
	}

	[Fact]
	public void CategoryBudgetUsesLegacyMinimumAndMultiplierFormula()
	{
		Assert.Equal(1f, LegacyOpportunitySemantics.CalculateCategoryBudget(0f, 0f, 0.5f, 0f));
		Assert.InRange(
			LegacyOpportunitySemantics.CalculateCategoryBudget(500f, 1.2f, 0.5f, 0f),
			208.3333f,
			208.3334f);
		Assert.Equal(250f, LegacyOpportunitySemantics.CalculateCategoryBudget(500f, 1.2f, 0.6f, 0f));
	}

	[Fact]
	public void CountedOpportunitiesRetainLegacyPerTypeAllocation()
	{
		const float categoryBudget = 208.333328f;
		const float targetIterations = 8f;
		var baseImportance = LegacyOpportunitySemantics.CalculateBaseImportance(8f, targetIterations);
		var maximum = LegacyOpportunitySemantics.CalculateOpportunityMaximum(
			categoryBudget,
			categoryBudget / targetIterations,
			baseImportance,
			1f,
			isRare: false);

		Assert.Equal(0.125f, baseImportance);
		Assert.InRange(maximum, 26.0416f, 26.0417f);
	}

	[Fact]
	public void RareOpportunityReceivesTheWholeLegacyTypeBudget()
	{
		var maximum = LegacyOpportunitySemantics.CalculateOpportunityMaximum(
			83.3333359f,
			10f,
			0.125f,
			0.25f,
			isRare: true);

		Assert.InRange(maximum, 83.3333f, 83.3334f);
	}

	[Theory]
	[InlineData(0, "ResearchOpportunity_0")]
	[InlineData(89, "ResearchOpportunity_89")]
	[InlineData(283, "ResearchOpportunity_283")]
	public void LoadIdFormattingRemainsStable(int loadId, string expected)
	{
		Assert.Equal(expected, LegacyOpportunitySemantics.FormatLoadId(loadId));
	}
}
