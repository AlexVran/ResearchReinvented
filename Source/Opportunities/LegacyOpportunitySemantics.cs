using System;

namespace PeteTimesSix.ResearchReinvented.Opportunities
{
	internal static class LegacyOpportunitySemantics
	{
		internal const float MinimumResearchPoints = 1f;

		internal static bool ResolveFlag(bool forced, bool requirementFlag)
		{
			return forced || requirementFlag;
		}

		internal static float NormalizeTotalMultiplier(float totalMultiplier)
		{
			return totalMultiplier < 1f ? 1f : totalMultiplier;
		}

		internal static float CalculateCategoryBudget(
			float projectResearchPoints,
			float totalMultiplier,
			float importanceMultiplier,
			float importanceStatic)
		{
			var normalizedMultiplier = NormalizeTotalMultiplier(totalMultiplier);
			var budget = ((projectResearchPoints / normalizedMultiplier) * importanceMultiplier)
				+ projectResearchPoints * importanceStatic;
			return EnsureMinimumProgress(budget);
		}

		internal static float CalculateBaseImportance(float typeImportanceTotal, float targetIterations)
		{
			if (typeImportanceTotal < 1f)
				typeImportanceTotal = 1f;
			if (typeImportanceTotal > targetIterations)
				typeImportanceTotal = targetIterations;
			return 1f / typeImportanceTotal;
		}

		internal static float CalculateOpportunityMaximum(
			float typeResearchPoints,
			float minimumOpportunityResearchPoints,
			float baseImportance,
			float opportunityImportance,
			bool isRare)
		{
			float opportunityResearchPoints;
			if (isRare)
			{
				opportunityResearchPoints = Math.Max(typeResearchPoints, minimumOpportunityResearchPoints);
			}
			else
			{
				opportunityResearchPoints = Math.Max(
					(typeResearchPoints * baseImportance) * opportunityImportance,
					minimumOpportunityResearchPoints * opportunityImportance);
			}

			return EnsureMinimumProgress(opportunityResearchPoints);
		}

		internal static float EnsureMinimumProgress(float value)
		{
			return Math.Max(MinimumResearchPoints, value);
		}

		internal static string FormatLoadId(int loadId)
		{
			return "ResearchOpportunity_" + loadId;
		}
	}
}
