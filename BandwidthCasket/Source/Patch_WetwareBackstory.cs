using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BandwidthCasket
{
    [StaticConstructorOnStartup]
    public static class Patch_WetwareBackstory
    {
        static Patch_WetwareBackstory()
        {
            new Harmony("BandwidthCasket.WetwareBackstory").PatchAll();
        }
    }

    [HarmonyPatch(typeof(PawnGenerator),
                  nameof(PawnGenerator.GeneratePawn),
                  new Type[] { typeof(PawnGenerationRequest) })]
    public static class Patch_GeneratePawn_WetwareBackstory
    {
        // 缓存反射句柄，只查一次
        private static readonly MethodInfo GenerateSkillsMethod =
            AccessTools.Method(
                typeof(PawnGenerator),
                "GenerateSkills",
                new[] { typeof(Pawn), typeof(PawnKindDef) });

        [HarmonyPostfix]
        public static void Postfix(Pawn __result)
        {
            if (__result?.genes == null || __result.story == null) return;
            if (__result.genes.Xenotype?.defName != "Wetware") return;

            var target = DefDatabase<BackstoryDef>.GetNamedSilentFail("NeuralChip");
            if (target == null) return;
            if (__result.story.Adulthood == target) return;

            // 换背景故事
            __result.story.Adulthood = target;

            // 反射重算技能
            if (GenerateSkillsMethod == null)
            {
                Log.WarningOnce(
                    "[BandwidthCasket] 找不到 PawnGenerator.GenerateSkills，Wetware 背景故事已替换但技能未重算。",
                    20181001);
                return;
            }

            try
            {
                GenerateSkillsMethod.Invoke(null, new object[] { __result, __result.kindDef });
            }
            catch (Exception ex)
            {
                Log.Warning($"[BandwidthCasket] 重算 Wetware 技能失败: {ex.Message}");
            }
        }
    }
}