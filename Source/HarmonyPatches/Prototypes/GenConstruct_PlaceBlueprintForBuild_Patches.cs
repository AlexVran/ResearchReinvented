using HarmonyLib;
using PeteTimesSix.ResearchReinvented.Domain.Prototypes;
using PeteTimesSix.ResearchReinvented.Extensions;
using PeteTimesSix.ResearchReinvented.Managers;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace PeteTimesSix.ResearchReinvented.HarmonyPatches.Prototypes
{
    [HarmonyPatch]
    public static class GenConstruct_PlaceBlueprintForBuild_Patches
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var methods = AccessTools.GetDeclaredMethods(typeof(GenConstruct))
                .Where(method => method.Name == nameof(GenConstruct.PlaceBlueprintForBuild)
                    && typeof(Blueprint).IsAssignableFrom(method.ReturnType)).ToArray();
            if (methods.Length == 0)
                Log.WarningOnce("RR prototypes: RimWorld's blueprint placement hook was not found; only pre-frame prototype blueprint tracking is disabled. Ordinary research remains enabled.", 1940317013);
            return methods;
        }

        [HarmonyPostfix]
        public static void Postfix(Blueprint __result)
        {
            if (__result?.def.entityDefToBuild != null && __result.def.entityDefToBuild.IsAvailableOnlyForPrototyping(true))
                PrototypeKeeper.Instance.RegisterThing(__result, PrototypeArtifactKind.Blueprint, PrototypeLifecycleState.Active);
        }
    }
}
