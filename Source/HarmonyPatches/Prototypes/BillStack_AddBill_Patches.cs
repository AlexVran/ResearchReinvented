using HarmonyLib;
using PeteTimesSix.ResearchReinvented.Managers;
using RimWorld;

namespace PeteTimesSix.ResearchReinvented.HarmonyPatches.Prototypes
{
    [HarmonyPatch(typeof(BillStack), nameof(BillStack.AddBill))]
    public static class BillStack_AddBill_Patches
    {
        [HarmonyPostfix]
        public static void Postfix(BillStack __instance, Bill bill)
        {
            PrototypeKeeper.Instance.RegisterBill(bill, __instance.billGiver);
        }
    }
}
