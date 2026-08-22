using HarmonyLib;
using PeteTimesSix.ResearchReinvented.Domain.Prototypes;
using PeteTimesSix.ResearchReinvented.Managers;
using RimWorld;

namespace PeteTimesSix.ResearchReinvented.HarmonyPatches.Prototypes
{
    [HarmonyPatch(typeof(BillStack), nameof(BillStack.Delete))]
    public static class BillStack_Delete_Patches
    {
        [HarmonyPrefix]
        public static void Prefix(BillStack __instance, Bill bill)
        {
            PrototypeKeeper.Instance.FinishBill(bill, __instance.billGiver, PrototypeLifecycleState.Cancelled);
        }
    }
}
