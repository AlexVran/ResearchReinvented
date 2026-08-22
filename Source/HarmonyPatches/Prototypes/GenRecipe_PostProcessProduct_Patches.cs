using HarmonyLib;
using PeteTimesSix.ResearchReinvented.Domain.Prototypes;
using PeteTimesSix.ResearchReinvented.Extensions;
using PeteTimesSix.ResearchReinvented.Managers;
using RimWorld;
using Verse;
using Verse.AI;

namespace PeteTimesSix.ResearchReinvented.HarmonyPatches.Prototypes
{
    [HarmonyPatch(typeof(GenRecipe), "PostProcessProduct")]
    public static class GenRecipe_PostProcessProduct_Patches
    {
        [HarmonyPostfix]
        public static void Postfix(Thing product, RecipeDef recipeDef, Pawn worker, Precept_ThingStyle precept = null)
        {
            if (product == null || recipeDef == null) return;
            if (!product.def.IsAvailableOnlyForPrototyping() && !recipeDef.IsAvailableOnlyForPrototyping()) return;
            var unfinished = worker?.CurJob?.GetTarget(TargetIndex.B).Thing as UnfinishedThing;
            bool firstCompletion;
            if (unfinished != null && PrototypeKeeper.Instance.IsPrototype(unfinished))
            {
                var unfinishedKey = PrototypeKeeper.Instance.RegisterThing(unfinished, PrototypeArtifactKind.UnfinishedItem, PrototypeLifecycleState.Active, recipeDef);
                firstCompletion = PrototypeKeeper.Instance.TransitionThing(unfinishedKey, unfinished, product, PrototypeArtifactKind.CompletedProduct, PrototypeLifecycleState.Completed, worker?.MapHeld);
            }
            else
                firstCompletion = PrototypeKeeper.Instance.TryRegisterThing(product, PrototypeArtifactKind.CompletedProduct, PrototypeLifecycleState.Completed, out _, recipeDef, worker?.MapHeld);
            if (!firstCompletion) return;
            PrototypeUtilities.DoPrototypeQualityDecreaseExisting(product, worker, recipeDef);
            PrototypeUtilities.DoPrototypeHealthDecrease(product, recipeDef);
            PrototypeUtilities.DoPrototypeBadComps(product, recipeDef);
            PrototypeUtilities.DoPostFinishThingResearch(worker, recipeDef.WorkAmountTotal(product), product, recipeDef);
        }
    }
}
