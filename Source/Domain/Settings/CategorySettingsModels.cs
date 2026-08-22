#nullable enable

using System;

namespace PeteTimesSix.ResearchReinvented.Domain.Settings
{
	public readonly struct ProgressRange : IEquatable<ProgressRange>
	{
		public ProgressRange(float minimum, float maximum)
		{
			if (!Finite(minimum) || !Finite(maximum) || minimum < 0f || maximum > 1f || minimum > maximum)
				throw new ArgumentOutOfRangeException(nameof(minimum), "Progress range must be finite, ordered, and between zero and one.");
			Minimum = minimum;
			Maximum = maximum;
		}

		public float Minimum { get; }
		public float Maximum { get; }
		public bool Equals(ProgressRange other) => Minimum.Equals(other.Minimum) && Maximum.Equals(other.Maximum);
		public override bool Equals(object? obj) => obj is ProgressRange other && Equals(other);
		public override int GetHashCode() => (Minimum.GetHashCode() * 397) ^ Maximum.GetHashCode();
		private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
	}

	public sealed class CategorySettingsValue
	{
		public CategorySettingsValue(
			bool enabled,
			float importanceStatic,
			float importanceMultiplier,
			float importanceMultiplierCounted,
			bool infiniteOverflow,
			float targetIterations,
			float researchSpeedMultiplier,
			ProgressRange availableAtOverallProgress)
		{
			if (!FiniteNonNegative(importanceStatic)) throw new ArgumentOutOfRangeException(nameof(importanceStatic));
			if (!FiniteNonNegative(importanceMultiplier)) throw new ArgumentOutOfRangeException(nameof(importanceMultiplier));
			if (!FiniteNonNegative(importanceMultiplierCounted) || importanceMultiplierCounted > importanceMultiplier)
				throw new ArgumentOutOfRangeException(nameof(importanceMultiplierCounted));
			if (!FiniteNonNegative(targetIterations) || targetIterations <= 0f) throw new ArgumentOutOfRangeException(nameof(targetIterations));
			if (!FiniteNonNegative(researchSpeedMultiplier) || researchSpeedMultiplier <= 0f) throw new ArgumentOutOfRangeException(nameof(researchSpeedMultiplier));
			Enabled = enabled;
			ImportanceStatic = importanceStatic;
			ImportanceMultiplier = importanceMultiplier;
			ImportanceMultiplierCounted = importanceMultiplierCounted;
			InfiniteOverflow = infiniteOverflow;
			TargetIterations = targetIterations;
			ResearchSpeedMultiplier = researchSpeedMultiplier;
			AvailableAtOverallProgress = availableAtOverallProgress;
		}

		public bool Enabled { get; }
		public float ImportanceStatic { get; }
		public float ImportanceMultiplier { get; }
		public float ImportanceMultiplierCounted { get; }
		public bool InfiniteOverflow { get; }
		public float TargetIterations { get; }
		public float ResearchSpeedMultiplier { get; }
		public ProgressRange AvailableAtOverallProgress { get; }

		private static bool FiniteNonNegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
	}

	public sealed class CategorySettingsOverride
	{
		public CategorySettingsOverride(
			bool? enabled = null,
			float? importanceStatic = null,
			float? importanceMultiplier = null,
			float? importanceMultiplierCounted = null,
			bool? infiniteOverflow = null,
			float? targetIterations = null,
			float? researchSpeedMultiplier = null,
			ProgressRange? availableAtOverallProgress = null)
		{
			Enabled = enabled;
			ImportanceStatic = importanceStatic;
			ImportanceMultiplier = importanceMultiplier;
			ImportanceMultiplierCounted = importanceMultiplierCounted;
			InfiniteOverflow = infiniteOverflow;
			TargetIterations = targetIterations;
			ResearchSpeedMultiplier = researchSpeedMultiplier;
			AvailableAtOverallProgress = availableAtOverallProgress;
		}

		public bool? Enabled { get; }
		public float? ImportanceStatic { get; }
		public float? ImportanceMultiplier { get; }
		public float? ImportanceMultiplierCounted { get; }
		public bool? InfiniteOverflow { get; }
		public float? TargetIterations { get; }
		public float? ResearchSpeedMultiplier { get; }
		public ProgressRange? AvailableAtOverallProgress { get; }
		public bool IsEmpty => Enabled == null && ImportanceStatic == null && ImportanceMultiplier == null
			&& ImportanceMultiplierCounted == null && InfiniteOverflow == null && TargetIterations == null
			&& ResearchSpeedMultiplier == null && AvailableAtOverallProgress == null;
	}

	public static class CategorySettingsResolver
	{
		public static CategorySettingsValue Resolve(CategorySettingsValue preset, CategorySettingsOverride? changes)
		{
			if (preset == null) throw new ArgumentNullException(nameof(preset));
			changes ??= new CategorySettingsOverride();
			var multiplier = changes.ImportanceMultiplier ?? preset.ImportanceMultiplier;
			var counted = Math.Min(multiplier, changes.ImportanceMultiplierCounted ?? preset.ImportanceMultiplierCounted);
			return new CategorySettingsValue(
				changes.Enabled ?? preset.Enabled,
				changes.ImportanceStatic ?? preset.ImportanceStatic,
				multiplier,
				counted,
				changes.InfiniteOverflow ?? preset.InfiniteOverflow,
				changes.TargetIterations ?? preset.TargetIterations,
				changes.ResearchSpeedMultiplier ?? preset.ResearchSpeedMultiplier,
				changes.AvailableAtOverallProgress ?? preset.AvailableAtOverallProgress);
		}

		public static CategorySettingsOverride SparseDifference(CategorySettingsValue preset, CategorySettingsValue resolved)
		{
			if (preset == null) throw new ArgumentNullException(nameof(preset));
			if (resolved == null) throw new ArgumentNullException(nameof(resolved));
			return new CategorySettingsOverride(
				resolved.Enabled == preset.Enabled ? null : resolved.Enabled,
				Equal(resolved.ImportanceStatic, preset.ImportanceStatic) ? null : resolved.ImportanceStatic,
				Equal(resolved.ImportanceMultiplier, preset.ImportanceMultiplier) ? null : resolved.ImportanceMultiplier,
				Equal(resolved.ImportanceMultiplierCounted, preset.ImportanceMultiplierCounted) ? null : resolved.ImportanceMultiplierCounted,
				resolved.InfiniteOverflow == preset.InfiniteOverflow ? null : resolved.InfiniteOverflow,
				Equal(resolved.TargetIterations, preset.TargetIterations) ? null : resolved.TargetIterations,
				Equal(resolved.ResearchSpeedMultiplier, preset.ResearchSpeedMultiplier) ? null : resolved.ResearchSpeedMultiplier,
				resolved.AvailableAtOverallProgress.Equals(preset.AvailableAtOverallProgress) ? null : resolved.AvailableAtOverallProgress);
		}

		private static bool Equal(float left, float right) => Math.Abs(left - right) <= 0.00001f;
	}
}
