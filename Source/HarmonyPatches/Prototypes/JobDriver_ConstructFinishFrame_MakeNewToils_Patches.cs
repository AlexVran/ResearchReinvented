using HarmonyLib;
using PeteTimesSix.ResearchReinvented.Managers;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;
using Verse.AI;

namespace PeteTimesSix.ResearchReinvented.HarmonyPatches.Prototypes
{
    [HarmonyPatch]
    public static class JobDriver_ConstructFinishFrame_MakeNewToils_Patches
    {
        private static readonly CodeMatch[] toMatch = new CodeMatch[]
        {
            new CodeMatch(OpCodes.Ldsfld, AccessTools.Field(typeof(StatDefOf), nameof(StatDefOf.ConstructSuccessChance))),
            new CodeMatch(OpCodes.Ldc_I4_1),
            new CodeMatch(OpCodes.Ldc_I4_M1),
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(StatExtension), nameof(StatExtension.GetStatValue))),
            new CodeMatch(OpCodes.Stloc_S)
        };

        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> CalculateMethods(Harmony instance)
        {
            var candidates = typeof(JobDriver_ConstructFinishFrame).GetNestedTypes(AccessTools.all).SelectMany(t => AccessTools.GetDeclaredMethods(t));

            foreach (var method in candidates)
            {
                var instructions = PatchProcessor.GetCurrentInstructions(method);
                var matched = new CodeMatcher(instructions).MatchStartForward(toMatch).IsValid;
                if(matched)
                    yield return method;
            }
            yield break;
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> JobDriver_ConstructFinishFrame_MakeNewToils_initAction_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);

            CodeInstruction[] toInsert = new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JobDriver_ConstructFinishFrame_MakeNewToils_Patches), nameof(PrototypeFailureChanceIncrease)))
            };

            codeMatcher.MatchEndForward(toMatch);
            if(codeMatcher.IsInvalid)
            {
                Log.WarningOnce("RR prototypes: construction failure-chance hook did not match the RimWorld method shape; only the extra prototype failure chance is disabled. Ordinary research remains enabled.", 1940317011);
                return instructions;
            }
            codeMatcher.Insert(toInsert);
            return codeMatcher.InstructionEnumeration();
        }

        public static float PrototypeFailureChanceIncrease(float statValue, object closure)
        {
            var driver = closure as JobDriver_ConstructFinishFrame
                ?? closure?.GetType().GetFields(AccessTools.all)
                    .Select(field => field.GetValue(closure)).OfType<JobDriver_ConstructFinishFrame>().FirstOrDefault();
            var frame = driver?.job?.GetTarget(TargetIndex.A).Thing as Frame;
            if (frame == null)
            {
                PrototypeKeeper.Instance.DisableFeature("construction-failure-chance",
                    "could not resolve the construction frame from RimWorld's generated closure; only the extra prototype failure chance is disabled");
                return statValue;
            }
            return PrototypeKeeper.Instance.IsPrototype(frame) ? statValue * 0.75f : statValue;
        }
    }
}
