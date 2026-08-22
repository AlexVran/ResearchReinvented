using PeteTimesSix.ResearchReinvented.HarmonyPatches.Prototypes;
using PeteTimesSix.ResearchReinvented.Rimworld.WorkGivers;
using PeteTimesSix.ResearchReinvented.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PeteTimesSix.ResearchReinvented.Utilities
{
    public static class CacheClearer
    {
        public static void ClearCaches() 
        {
            ResearchOpportunityManager.Instance.Execution.InvalidateMapIndexes();
            PrototypeUtilities.ClearPrototypeOpportunityCache();
        }
    }
}
