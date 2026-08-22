using HarmonyLib;
using PeteTimesSix.ResearchReinvented.Extensions;
using PeteTimesSix.ResearchReinvented.Domain.Prototypes;
using PeteTimesSix.ResearchReinvented.Managers;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace PeteTimesSix.ResearchReinvented.HarmonyPatches.Prototypes
{
    [HarmonyPatch(typeof(Blueprint), nameof(Blueprint.TryReplaceWithSolidThing))]
    public static class Blueprint_TryReplaceWithSolidThing_Patches
    {
        [HarmonyPrefix]
        public static void Prefix(Blueprint __instance, out PrototypeArtifactKey __state)
        {
            __state = null;
            if (__instance.def.entityDefToBuild.IsAvailableOnlyForPrototyping(true))
                __state = PrototypeKeeper.Instance.RegisterThing(__instance, PrototypeArtifactKind.Blueprint, PrototypeLifecycleState.Active);
        }

        [HarmonyPostfix] public static void Postfix(Blueprint __instance, Pawn workerPawn, ref Thing createdThing, ref bool jobEnded, PrototypeArtifactKey __state)
        {
            if (__state != null && createdThing is Frame)
                PrototypeKeeper.Instance.TransitionThing(__state, __instance, createdThing, PrototypeArtifactKind.Frame, PrototypeLifecycleState.Active);
        }
    }
}
