using PeteTimesSix.ResearchReinvented.Defs;
using PeteTimesSix.ResearchReinvented.HarmonyPatches.Prototypes;
using PeteTimesSix.ResearchReinvented.Managers;
using PeteTimesSix.ResearchReinvented.Opportunities;
using PeteTimesSix.ResearchReinvented.OpportunityComps;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace PeteTimesSix.ResearchReinvented.Extensions
{
    public static class ThingDefExtensions
    {
        public static bool IsTrulyRawFood(this ThingDef x)
        {
            return x.IsNutritionGivingIngestible && !x.IsCorpse && x.ingestible.HumanEdible && x.ingestible.preferability < FoodPreferability.MealAwful;
        }

        public static bool IsInstantBuild(this ThingDef x)
        {
            if (x is BuildableDef asBuildable)
            {
                if (asBuildable.GetStatValueAbstract(StatDefOf.WorkToBuild) == 0f)
                    return true;
            }
            return false;
        }

        public static bool IsAvailableOnlyForPrototyping(this ThingDef def)
		{
			if (def.researchPrerequisites != null)
			{
                var unfinishedPreregs = def.researchPrerequisites.Where((ResearchProjectDef r) => !r.IsFinished);
				if (!unfinishedPreregs.Any())
					return false;
				if (unfinishedPreregs.Any((ResearchProjectDef r) => Find.ResearchManager.GetProject() != r))
					return false;

				return true;
			}
			return false;
		}

        public static bool IsAvailableOnlyForPrototyping(this ThingDef def, bool evenIfFinished = true)
        {
            if (def.researchPrerequisites != null && def.researchPrerequisites.Count > 0)
            {
                var unfinishedPreregs = def.researchPrerequisites.Where((ResearchProjectDef r) => !r.IsFinished);
                if (!unfinishedPreregs.Any())
                    return false;
                if (unfinishedPreregs.Any((ResearchProjectDef r) => Find.ResearchManager.GetProject() != r))
                    return false;
                var opportunity = PrototypeKeeper.Instance.GetPrototypeOpportunity(def);
                if (opportunity == null)
                    return false;
                if (!evenIfFinished)
                    return opportunity.CurrentAvailability == OpportunityAvailability.Available;
                else
                    return opportunity.CurrentAvailability == OpportunityAvailability.Available || opportunity.CurrentAvailability == OpportunityAvailability.Finished || opportunity.CurrentAvailability == OpportunityAvailability.CategoryFinished;
            }
            return false;
        }
    }
}
