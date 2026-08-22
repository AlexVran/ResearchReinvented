using PeteTimesSix.ResearchReinvented.Domain.Settings;
using System;
using Verse;

namespace PeteTimesSix.ResearchReinvented.Data
{
    internal static class CategorySettingsDomainAdapter
    {
        internal static CategorySettingsValue ToValue(CategorySettingsPreset source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var multiplier = NonNegative(source.importanceMultiplier);
            var rangeMin = Clamp01(source.availableAtOverallProgress.min);
            var rangeMax = Clamp01(source.availableAtOverallProgress.max);
            return new CategorySettingsValue(
                source.enabled,
                NonNegative(source.importanceStatic),
                multiplier,
                Math.Min(multiplier, NonNegative(source.importanceMultiplierCounted)),
                source.infiniteOverflow,
                Positive(source.targetIterations, 1f),
                Positive(source.researchSpeedMultiplier, 1f),
                new ProgressRange(Math.Min(rangeMin, rangeMax), Math.Max(rangeMin, rangeMax)));
        }

        internal static CategorySettingsOverride ToOverride(CategorySettingsChanges source)
        {
            if (source == null) return new CategorySettingsOverride();
            ProgressRange? range = null;
            if (source.availableAtOverallProgress.HasValue)
            {
                var rangeMin = Clamp01(source.availableAtOverallProgress.Value.min);
                var rangeMax = Clamp01(source.availableAtOverallProgress.Value.max);
                range = new ProgressRange(Math.Min(rangeMin, rangeMax), Math.Max(rangeMin, rangeMax));
            }
            return new CategorySettingsOverride(
                source.enabled,
                OptionalNonNegative(source.importanceStatic),
                OptionalNonNegative(source.importanceMultiplier),
                OptionalNonNegative(source.importanceMultiplierCounted),
                source.infiniteOverflow,
                OptionalPositive(source.targetIterations),
                OptionalPositive(source.researchSpeedMultiplier),
                range);
        }

        internal static void Apply(CategorySettingsValue source, CategorySettingsPreset target)
        {
            target.enabled = source.Enabled;
            target.importanceStatic = source.ImportanceStatic;
            target.importanceMultiplier = source.ImportanceMultiplier;
            target.importanceMultiplierCounted = source.ImportanceMultiplierCounted;
            target.infiniteOverflow = source.InfiniteOverflow;
            target.targetIterations = source.TargetIterations;
            target.researchSpeedMultiplier = source.ResearchSpeedMultiplier;
            target.availableAtOverallProgress = new FloatRange(source.AvailableAtOverallProgress.Minimum, source.AvailableAtOverallProgress.Maximum);
        }

        internal static void Apply(CategorySettingsOverride source, CategorySettingsChanges target)
        {
            target.enabled = source.Enabled;
            target.importanceStatic = source.ImportanceStatic;
            target.importanceMultiplier = source.ImportanceMultiplier;
            target.importanceMultiplierCounted = source.ImportanceMultiplierCounted;
            target.infiniteOverflow = source.InfiniteOverflow;
            target.targetIterations = source.TargetIterations;
            target.researchSpeedMultiplier = source.ResearchSpeedMultiplier;
            target.availableAtOverallProgress = source.AvailableAtOverallProgress.HasValue
                ? new FloatRange(source.AvailableAtOverallProgress.Value.Minimum, source.AvailableAtOverallProgress.Value.Maximum)
                : (FloatRange?)null;
        }

        private static float NonNegative(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, value);
        private static float Positive(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? fallback : value;
        private static float? OptionalNonNegative(float? value) => value.HasValue ? NonNegative(value.Value) : (float?)null;
        private static float? OptionalPositive(float? value) => value.HasValue ? Positive(value.Value, 1f) : (float?)null;
        private static float Clamp01(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Min(1f, Math.Max(0f, value));
    }
}
