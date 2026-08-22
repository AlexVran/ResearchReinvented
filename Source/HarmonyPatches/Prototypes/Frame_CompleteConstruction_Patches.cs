using HarmonyLib;
using PeteTimesSix.ResearchReinvented.Domain.Prototypes;
using PeteTimesSix.ResearchReinvented.Extensions;
using PeteTimesSix.ResearchReinvented.Managers;
using RimWorld;
using System.Linq;
using Verse;

namespace PeteTimesSix.ResearchReinvented.HarmonyPatches.Prototypes
{
    [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
    public static class Frame_CompleteConstruction_Patches
    {
        private const string FeatureId = "construction-completion";

        public sealed class CompletionState
        {
            public PrototypeArtifactKey Key;
            public bool IsPrototype;
            public Map Map;
            public IntVec3 Position;
            public BuildableDef Buildable;
            public float Work;
        }

        [HarmonyPrefix]
        public static void Prefix(Frame __instance, out CompletionState __state)
        {
            __state = new CompletionState
            {
                Map = __instance.Map,
                Position = __instance.Position,
                Buildable = __instance.def.entityDefToBuild,
            };
            if (!PrototypeKeeper.Instance.IsFeatureEnabled(FeatureId)) return;
            __state.IsPrototype = PrototypeKeeper.Instance.IsPrototype(__instance)
                || __state.Buildable.IsAvailableOnlyForPrototyping(true);
            if (!__state.IsPrototype) return;
            __state.Key = PrototypeKeeper.Instance.RegisterThing(__instance, PrototypeArtifactKind.Frame, PrototypeLifecycleState.Active);
            __state.Work = __instance.WorkToBuild;
        }

        [HarmonyPostfix]
        public static void Postfix(Frame __instance, Pawn worker, CompletionState __state)
        {
            if (__state == null || __state.Map == null) return;
            if (__state.Buildable is TerrainDef terrain)
            {
                if (!__state.IsPrototype)
                {
                    PrototypeKeeper.Instance.UnmarkTerrainAsPrototype(__state.Position, __state.Map);
                    PrototypeKeeper.Instance.UnmarkFoundationTerrainAsPrototype(__state.Position, __state.Map);
                    return;
                }
                if (__state.Key == null) return;
                var completed = PrototypeKeeper.Instance.FinishThing(__state.Key, __instance, PrototypeLifecycleState.Completed);
                if (!completed) return;
                if (__state.Map.terrainGrid.TerrainAt(__state.Position) == terrain)
                    PrototypeKeeper.Instance.MarkTerrainAsPrototype(__state.Position, __state.Map, terrain);
                else
                    PrototypeKeeper.Instance.MarkFoundationTerrainAsPrototype(__state.Position, __state.Map, terrain);
                PrototypeUtilities.DoPostFinishTerrainResearch(worker, __state.Work, terrain);
                return;
            }

            if (!__state.IsPrototype || __state.Key == null) return;
            if (!(__state.Buildable is ThingDef thingDef)) return;
            var product = __state.Map.listerThings.ThingsOfDef(thingDef)
                .FirstOrDefault(thing => thing.Position == __state.Position && thing != __instance && !thing.Destroyed);
            if (product == null)
            {
                PrototypeKeeper.Instance.DisableFeature(FeatureId,
                    $"could not locate the completed {thingDef.defName} at {__state.Position}; only construction prototype completion/credit is disabled for this game");
                return;
            }

            if (!PrototypeKeeper.Instance.TransitionThing(__state.Key, __instance, product, PrototypeArtifactKind.CompletedProduct, PrototypeLifecycleState.Completed))
                return;
            PrototypeUtilities.DoPrototypeQualityDecreaseExisting(product, worker, null);
            PrototypeUtilities.DoPrototypeHealthDecrease(product, null);
            PrototypeUtilities.DoPrototypeBadComps(product, null);
            PrototypeUtilities.DoPostFinishThingResearch(worker, __state.Work, product, null);
        }
    }
}
