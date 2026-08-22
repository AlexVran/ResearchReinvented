using HarmonyLib;
using PeteTimesSix.ResearchReinvented.Domain.Prototypes;
using PeteTimesSix.ResearchReinvented.Extensions;
using PeteTimesSix.ResearchReinvented.Managers;
using RimWorld;
using Verse;

namespace PeteTimesSix.ResearchReinvented.HarmonyPatches.Prototypes
{
    [HarmonyPatch(typeof(Frame), nameof(Frame.FailConstruction))]
    public static class Frame_FailConstruction_Patches
    {
        public sealed class FailureState
        {
            public PrototypeArtifactKey Key;
            public BuildableDef Buildable;
            public float TotalWork;
            public float DoneWork;
        }

        [HarmonyPrefix]
        public static void Prefix(Frame __instance, out FailureState __state)
        {
            __state = new FailureState { Buildable = __instance.def.entityDefToBuild };
            if (!PrototypeKeeper.Instance.IsPrototype(__instance) && !__state.Buildable.IsAvailableOnlyForPrototyping(true)) return;
            __state.Key = PrototypeKeeper.Instance.RegisterThing(__instance, PrototypeArtifactKind.Frame, PrototypeLifecycleState.Active);
            __state.TotalWork = __instance.WorkToBuild;
            __state.DoneWork = __instance.workDone;
        }

        [HarmonyPostfix]
        public static void Postfix(Frame __instance, Pawn worker, FailureState __state)
        {
            if (__state?.Key == null || !PrototypeKeeper.Instance.FinishThing(__state.Key, __instance, PrototypeLifecycleState.Failed)) return;
            if (__state.Buildable is TerrainDef terrain)
                PrototypeUtilities.DoPostFailToFinishTerrainResearch(worker, __state.TotalWork, __state.DoneWork, terrain);
            else if (__state.Buildable is ThingDef thing)
                PrototypeUtilities.DoPostFailToFinishThingResearch(worker, __state.TotalWork, __state.DoneWork, thing, null);
        }
    }
}
