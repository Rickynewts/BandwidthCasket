using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BandwidthCasket
{
    /// <summary>
    /// 带宽节点组件。行为参考原版 CompBandNode：
    ///   - 只有"容器内有 pawn 且通电"时才算 IsActive；
    ///   - 可以把节点"协调"到某个机械师身上，给该机械师 +bandwidth 带宽；
    ///   - 首次协调需要 tuneSeconds 秒；重新协调需要 retuneDays 天；
    ///   - 重新协调期间，旧机械师保留 hediff，协调完成后才切换。
    /// </summary>
    [StaticConstructorOnStartup]
    public class CompBandwidthCasketNode : ThingComp
    {
        // 协调按钮图标（原版带宽节点用的同一个图标）
        private static readonly CachedTexture TuningIcon = new CachedTexture("UI/Gizmos/BandNodeTuning");

        // ──────── 状态字段 ────────
        private Pawn tunedTo;         // 当前已协调到的机械师
        private Pawn tuningTo;        // 正在协调中的新目标
        private int tuningTimeLeft;   // 协调剩余 tick 数

        // 电力组件缓存（懒加载）
        private CompPowerTrader powerComp;
        private CompPowerTrader PowerTrader =>
            powerComp ?? (powerComp = parent.TryGetComp<CompPowerTrader>());

        // ──────── 常用访问器 ────────
        public CompProperties_BandwidthCasketNode Props => (CompProperties_BandwidthCasketNode)props;

        /// <summary>父建筑（带宽仓）。</summary>
        public Building_BandwidthCasket Casket => parent as Building_BandwidthCasket;

        /// <summary>是否处于"正在工作"状态：仓内有 pawn 且通电。</summary>
        public bool IsActive =>
            Casket != null && Casket.HasPawn &&
            (PowerTrader == null || PowerTrader.PowerOn);

        /// <summary>对外暴露的"已协调目标"。</summary>
        public Pawn TunedTo => tunedTo;

        /// <summary>首次协调所需 tick 数。</summary>
        private int TuneTimeTicks => (int)(Props.tuneSeconds * 60f);

        /// <summary>重新协调所需 tick 数（默认 3 天）。</summary>
        private int RetuneTimeTicks => (int)(Props.retuneDays * 60000f);

        // ──────── 存档 ────────
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref tunedTo, "tunedTo");
            Scribe_References.Look(ref tuningTo, "tuningTo");
            Scribe_Values.Look(ref tuningTimeLeft, "tuningTimeLeft", 0);
        }

        // ──────── 状态变化通知 ────────
        /// <summary>由 Building_BandwidthCasket 在放入/弹出 pawn 时调用。</summary>
        public void OnCasketOccupancyChanged()
        {
            if (!IsActive)
            {
                ClearTuning();
            }
        }

        /// <summary>
        /// 清空所有协调状态：移除目标的 hediff、重置字段。
        /// 用于：pawn 被弹出、建筑被拆除、当前目标死亡等。
        /// </summary>
        public void ClearTuning()
        {
            // 移除目标身上的带宽 hediff
            if (tunedTo != null && !tunedTo.Dead && tunedTo.health != null)
            {
                var h = tunedTo.health.hediffSet.GetFirstHediffOfDef(Props.hediff);
                if (h != null) tunedTo.health.RemoveHediff(h);
            }
            tunedTo = null;
            tuningTo = null;
            tuningTimeLeft = 0;
        }

        // ──────── 每 tick ────────
        public override void CompTick()
        {
            base.CompTick();

            if (!IsActive) return;   // 仓内没人 / 断电 → 什么都不做

            // 目标死亡/销毁 → 清理
            if (tunedTo != null && (tunedTo.Dead || tunedTo.Destroyed))
                ClearTuning();
            if (tuningTo != null && (tuningTo.Dead || tuningTo.Destroyed))
                tuningTo = null;

            // 正在协调中
            if (tuningTo != null)
            {
                tuningTimeLeft--;
                if (tuningTimeLeft <= 0)
                {
                    // 协调完成：移除旧目标的 hediff，把 tunedTo 换成新目标
                    if (tunedTo != null && !tunedTo.Dead)
                    {
                        var old = tunedTo.health.hediffSet.GetFirstHediffOfDef(Props.hediff);
                        if (old != null) tunedTo.health.RemoveHediff(old);
                    }
                    tunedTo = tuningTo;
                    tuningTo = null;

                    Props.tuningCompleteSound?.PlayOneShot(parent);
                }
            }

            // 保证已协调目标身上有 hediff（比如中途被移除过，要补挂）
            if (tunedTo != null && !tunedTo.health.hediffSet.HasHediff(Props.hediff))
            {
                tunedTo.health.AddHediff(Props.hediff, tunedTo.health.hediffSet.GetBrain());
            }
        }

        // ──────── 检查面板文本 ────────
        public override string CompInspectStringExtra()
        {
            if (!IsActive)
                return "Idle - no pawn inside".Translate();

            string s = "BandNodeTunedTo".Translate() + ": " +
                       (tunedTo == null ? "Nobody".Translate().Resolve() : tunedTo.Name.ToStringFull);

            // 如果正在重新协调，额外显示进度
            if (tuningTo != null)
                s += "\n" + "BandNodeTuningTo".Translate() + ": " +
                     tuningTo.Name.ToStringFull + " - " + tuningTimeLeft.ToStringTicksToPeriod();

            return s;
        }

        // ──────── 命令条 Gizmos ────────
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!IsActive) yield break;   // 空仓/断电不显示协调按钮

            // 主协调按钮
            var cmd = new Command_Action
            {
                defaultLabel = tunedTo == null
                    ? "BandNodeTuneTo".Translate() + "..."
                    : "BandNodeRetuneTo".Translate() + "...",
                defaultDesc = tunedTo == null
                    ? "BandNodeTuningDesc".Translate("PeriodSeconds".Translate(Props.tuneSeconds))
                    : "BandNodeRetuningDesc".Translate(Props.retuneDays + " " + "Days".Translate()),
                icon = TuningIcon.Texture,
                onHover = () =>
                {
                    // 悬停时画线：绿线=已协调，橙线=正在协调
                    if (tunedTo != null)
                        GenDraw.DrawLineBetween(parent.DrawPos, tunedTo.DrawPos, SimpleColor.Green);
                    if (tuningTo != null)
                        GenDraw.DrawLineBetween(parent.DrawPos, tuningTo.DrawPos, SimpleColor.Orange);
                }
            };

            // 地图上没有候选机械师 → 按钮置灰
            bool anyMechanitor = parent.Map.mapPawns.AllPawnsSpawned.Any(p =>
                MechanitorUtility.IsMechanitor(p) &&
                p != tunedTo && p != tuningTo &&
                p != Casket.ContainedPawn);
            cmd.Disabled = !anyMechanitor;

            // 点击：弹出机械师选择菜单
            cmd.action = () =>
            {
                var options = new List<FloatMenuOption>();
                foreach (var pawn in parent.Map.mapPawns.AllPawnsSpawned)
                {
                    if (!MechanitorUtility.IsMechanitor(pawn)) continue;
                    if (pawn == Casket.ContainedPawn) continue;
                    if (pawn == tunedTo || pawn == tuningTo) continue;

                    var p = pawn;
                    string label = p.Name.ToStringFull;
                    // 附上本次协调的耗时提示
                    if (tunedTo == null)
                        label += " (" + Props.tuneSeconds + " " + "SecondsLower".Translate() + ")";
                    else
                        label += " (" + RetuneTimeTicks.ToStringTicksToPeriod() + ")";

                    options.Add(new FloatMenuOption(label, () => StartTuning(p)));
                }
                if (options.Any())
                    Find.WindowStack.Add(new FloatMenu(options));
            };

            yield return cmd;

            // 开发者模式：一键完成协调
            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: complete tuning",
                    action = () => tuningTimeLeft = 0
                };
            }
        }

        /// <summary>开始协调：根据是否已有目标，决定用首次/重新协调的时长。</summary>
        private void StartTuning(Pawn p)
        {
            tuningTo = p;
            tuningTimeLeft = tunedTo == null ? TuneTimeTicks : RetuneTimeTicks;
        }
        // 在类的任意合适位置加上这段
        public int EffectiveBandwidth
        {
            get
            {
                Pawn inner = Casket?.ContainedPawn;
                if (inner?.genes != null &&
                    inner.genes.HasActiveGene(BandwidthCasket_GeneDefOf.BandwidthCasket_Booster))
                {
                    return Props.boostedBandwidth;
                }
                return Props.bandwidth;
            }
        }
    }
}