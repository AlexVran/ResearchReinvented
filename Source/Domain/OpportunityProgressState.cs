#nullable enable

using System;

namespace PeteTimesSix.ResearchReinvented.Domain
{
	/// <summary>
	/// Mutable progress kept separate from OpportunitySpec. Phase 8 will own its
	/// serialization and migration; this type is not wired into saved games yet.
	/// </summary>
	public sealed class OpportunityProgressState
	{
		public OpportunityProgressState(OpportunityKey key, float currentProgress, float maximumProgress)
		{
			Key = key ?? throw new ArgumentNullException(nameof(key));
			Restore(currentProgress, maximumProgress);
		}

		public OpportunityKey Key { get; }

		public float CurrentProgress { get; private set; }

		public float MaximumProgress { get; private set; }

		public void Restore(float currentProgress, float maximumProgress)
		{
			ValidateProgress(currentProgress, nameof(currentProgress));
			ValidateProgress(maximumProgress, nameof(maximumProgress));
			CurrentProgress = currentProgress;
			MaximumProgress = maximumProgress;
		}

		public float Apply(float amount)
		{
			ValidateProgress(amount, nameof(amount));
			var applied = Math.Min(amount, Math.Max(0f, MaximumProgress - CurrentProgress));
			CurrentProgress += applied;
			return applied;
		}

		private static void ValidateProgress(float value, string parameterName)
		{
			if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
				throw new ArgumentOutOfRangeException(parameterName, "Opportunity progress must be finite and non-negative.");
		}
	}
}
