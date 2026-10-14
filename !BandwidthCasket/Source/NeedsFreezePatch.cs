using HarmonyLib;
using RimWorld;
using Verse;

namespace BandwidthCasket
{
    /// <summary>
    /// 双保险补丁：直接拦截 Pawn_NeedsTracker.NeedsTrackerTick。
    /// 只要 pawn 的父持有者是 Building_BandwidthCasket，就跳过原方法，
    /// 需求（食物/休息/娱乐/美观/户外…）完全停止 tick。
    ///
    /// 说明：Building_BandwidthCasket 已经实现 ISuspendableThingHolder，
    /// 正常情况下 Pawn.Suspended 会返回 true，NeedsTrackerTick 内部会自动早退。
    /// 本补丁用于兜底，防止某些特殊路径绕过 Suspended 检查。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class NeedsFreezePatch
    {
        static NeedsFreezePatch()
        {
            var harmony = new Harmony("BandwidthCasket.NeedsFreeze");
            harmony.PatchAll();   // 扫描本程序集所有 [HarmonyPatch]
        }

        // 通过反射拿到 Pawn_NeedsTracker 里的私有字段 "pawn"
        private static readonly AccessTools.FieldRef<Pawn_NeedsTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_NeedsTracker, Pawn>("pawn");

        // Prefix 返回 false 表示跳过原方法
        [HarmonyPatch(typeof(Pawn_NeedsTracker))]
        [HarmonyPatch("NeedsTrackerTick")]
        [HarmonyPrefix]
        public static bool Prefix(Pawn_NeedsTracker __instance)
        {
            Pawn pawn = PawnField(__instance);
            if (pawn != null && pawn.ParentHolder is Building_BandwidthCasket)
                return false;   // 跳过原方法，需求完全不动
            return true;
        }
    }
}