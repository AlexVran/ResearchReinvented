#nullable enable

using System;

namespace PeteTimesSix.ResearchReinvented.Domain.Execution
{
	public static class OpportunityProgressMath
	{
		public static float CalculateAppliedAmount(
			float baseAmount,
			float currentProgress,
			float maximumProgress,
			float storytellerFactor,
			float categoryFactor,
			float researcherTechCostFactor,
			bool fastResearch)
		{
			ValidateNonNegative(baseAmount, nameof(baseAmount));
			ValidateNonNegative(currentProgress, nameof(currentProgress));
			ValidateNonNegative(maximumProgress, nameof(maximumProgress));
			ValidateNonNegative(storytellerFactor, nameof(storytellerFactor));
			ValidateNonNegative(categoryFactor, nameof(categoryFactor));
			if (float.IsNaN(researcherTechCostFactor) || float.IsInfinity(researcherTechCostFactor) || researcherTechCostFactor <= 0f)
				throw new ArgumentOutOfRangeException(nameof(researcherTechCostFactor));

			var modified = baseAmount * storytellerFactor * categoryFactor / researcherTechCostFactor;
			if (fastResearch) modified *= 500f;
			if (float.IsNaN(modified) || float.IsInfinity(modified))
				throw new ArgumentOutOfRangeException(nameof(baseAmount), "Combined progress modifiers must produce a finite amount.");
			return Math.Min(modified, Math.Max(0f, maximumProgress - currentProgress));
		}

		public static float ClampMoteAmount(float moteAmount, float currentProgress, float maximumProgress)
		{
			ValidateNonNegative(moteAmount, nameof(moteAmount));
			ValidateNonNegative(currentProgress, nameof(currentProgress));
			ValidateNonNegative(maximumProgress, nameof(maximumProgress));
			return Math.Min(moteAmount, Math.Max(0f, maximumProgress - currentProgress));
		}

		private static void ValidateNonNegative(float value, string parameterName)
		{
			if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
				throw new ArgumentOutOfRangeException(parameterName);
		}
	}
}
