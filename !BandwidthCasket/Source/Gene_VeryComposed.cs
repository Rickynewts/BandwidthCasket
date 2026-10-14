using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace BandwidthCasket
{
    public static class GeneCache
    {
        private static GeneDef veryComposed;

        public static GeneDef VeryComposed
        {
            get
            {
                if (veryComposed == null)
                {
                    veryComposed = DefDatabase<GeneDef>.GetNamedSilentFail("VeryComposed");
                }
                return veryComposed;
            }
        }

        public static bool HasVeryComposed(Pawn pawn)
        {
            return pawn?.genes != null
                && VeryComposed != null
                && pawn.genes.HasActiveGene(VeryComposed);
        }
    }

    // 直接阻止精神崩溃
    [HarmonyPatch(typeof(MentalStateHandler), "TryStartMentalState")]
    public static class Patch_MentalStateHandler_TryStartMentalState
    {
        public static bool Prefix(ref bool __result, Pawn ___pawn)
        {
            if (GeneCache.HasVeryComposed(___pawn))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    // 不能参与奴隶叛乱
    [HarmonyPatch(typeof(SlaveRebellionUtility), "CanParticipateInSlaveRebellion")]
    public static class Patch_CanParticipateInSlaveRebellion
    {
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (!__result) return;

            if (GeneCache.HasVeryComposed(pawn))
            {
                __result = false;
            }
        }
    }

    // 额外保险：永远不会发起叛乱
    [HarmonyPatch(typeof(SlaveRebellionUtility), "InitiateSlaveRebellionMtbDays")]
    public static class Patch_InitiateSlaveRebellionMtbDays
    {
        public static void Postfix(Pawn pawn, ref float __result)
        {
            if (__result < 0f) return;

            if (GeneCache.HasVeryComposed(pawn))
            {
                __result = -1f;
            }
        }
    }

    // 下面三个是隐藏精神崩溃风险告警，可选
    [HarmonyPatch(typeof(MentalBreaker), "BreakMinorIsImminent", MethodType.Getter)]
    public static class Patch_BreakMinorIsImminent
    {
        public static bool Prefix(Pawn ___pawn, ref bool __result)
        {
            if (GeneCache.HasVeryComposed(___pawn))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MentalBreaker), "BreakMajorIsImminent", MethodType.Getter)]
    public static class Patch_BreakMajorIsImminent
    {
        public static bool Prefix(Pawn ___pawn, ref bool __result)
        {
            if (GeneCache.HasVeryComposed(___pawn))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MentalBreaker), "BreakExtremeIsImminent", MethodType.Getter)]
    public static class Patch_BreakExtremeIsImminent
    {
        public static bool Prefix(Pawn ___pawn, ref bool __result)
        {
            if (GeneCache.HasVeryComposed(___pawn))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            new Harmony("yourname.deadcalmslave").PatchAll();
        }
    }
}