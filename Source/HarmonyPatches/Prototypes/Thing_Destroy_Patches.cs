using HarmonyLib;
using PeteTimesSix.ResearchReinvented.Managers;
using Verse;

namespace PeteTimesSix.ResearchReinvented.HarmonyPatches.Prototypes
{
    [HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
    public static class Thing_Destroy_Patches
    {
        [HarmonyPrefix]
        public static void Prefix(Thing __instance)
        {
            if (PrototypeKeeper.Instance.IsPrototype(__instance))
                PrototypeKeeper.Instance.UnmarkAsPrototype(__instance);
        }
    }
}
