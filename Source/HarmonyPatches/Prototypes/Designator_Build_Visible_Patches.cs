using HarmonyLib;
using PeteTimesSix.ResearchReinvented.Extensions;
using RimWorld;

namespace PeteTimesSix.ResearchReinvented.HarmonyPatches.Prototypes
{
    [HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.Visible), MethodType.Getter)]
    public static class Designator_Build_Visible_Patches
    {
        [HarmonyPostfix]
        public static void Postfix(Designator_Build __instance, ref bool __result)
        {
            if (!__result && __instance.PlacingDef != null)
                __result = __instance.PlacingDef.IsAvailableOnlyForPrototyping(true);
        }
    }
}
