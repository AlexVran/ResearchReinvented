using PeteTimesSix.ResearchReinvented.Defs;
using System;
using PeteTimesSix.ResearchReinvented.Domain.Settings;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace PeteTimesSix.ResearchReinvented.Data
{
    public class CategorySettingsChanges : IExposable
    {
        public ResearchOpportunityCategoryDef category;

        public bool? enabled;

        public float? importanceStatic;
        public float? importanceMultiplier;
        public float? importanceMultiplierCounted;
        public bool? infiniteOverflow;
        public float? targetIterations;
        public float? researchSpeedMultiplier;

        public FloatRange? availableAtOverallProgress;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref category, "category");

            Scribe_Values.Look(ref enabled, "enabled", null);

            Scribe_Values.Look(ref importanceStatic, "importanceStatic", null);
            Scribe_Values.Look(ref importanceMultiplier, "importanceMultiplier", null);
            Scribe_Values.Look(ref importanceMultiplierCounted, "importanceMultiplierCounted", null);
            Scribe_Values.Look(ref infiniteOverflow, "infiniteOverflow", null);
            Scribe_Values.Look(ref targetIterations, "targetIterations", null);
            Scribe_Values.Look(ref researchSpeedMultiplier, "researchSpeedMultiplier", null);

            Scribe_Values.Look(ref availableAtOverallProgress, "availableAtOverallProgress", null);
        }

        public void UpdateChanges(CategorySettingsPreset defaults, CategorySettingsFinal finals)
        {
            var difference = CategorySettingsResolver.SparseDifference(
                CategorySettingsDomainAdapter.ToValue(defaults),
                CategorySettingsDomainAdapter.ToValue(finals));
            CategorySettingsDomainAdapter.Apply(difference, this);
        }
    }
}
