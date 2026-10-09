using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace BandwidthCasket
{
    /// <summary>
    /// 带宽仓建筑本体。
    /// 继承 Building_Casket（自带容器、休眠逻辑、弹出按钮）。
    /// 额外实现：
    ///   - ISuspendableThingHolder：容器内的东西应当被挂起（冻结需求）。
    ///   - IThingHolderWithDrawnPawn：容器内的 pawn自己来绘制（用于躺在建筑上）。
    /// </summary>
    public class Building_BandwidthCasket : Building_Casket,
                                             ISuspendableThingHolder,
                                             IThingHolderWithDrawnPawn
    {
        // ─────────────── 接口实现：容器内容挂起 ───────────────
        // 显式实现（带接口名前缀）是必须的：只有派生类重新声明接口并显式实现，
        // 才能让 "ParentHolder is ISuspendableThingHolder { IsContentsSuspended: true }" 这个
        // 模式匹配命中我们这份实现，从而让 RimWorld 判定仓内 pawn 是 Suspended。
        // 效果：仓内 pawn 的需求、年龄、心情等 tick 全部跳过。
        bool ISuspendableThingHolder.IsContentsSuspended => true;

        // ─────────────── 4 个方向的绘制偏移 ───────────────
        // 美术贴图在四个方向上的"器皿中心"并不居中，因此每个方向需要独立配置。
        // 单位：世界坐标格（1.0 = 一格）。
        // 想往右(x+)/左(x-)、上(z+)/下(z-) 调。
        // 注意：下面这几个静态字段目前没有直接用到（被 getter 里的 switch 覆盖），
        //       保留它们只是为了方便你对照四个方向的"基准值"。
        private static readonly Vector3 PawnOffsetNorth = new Vector3(0.75f, 0f, -0.35f);
        private static readonly Vector3 PawnOffsetEast = new Vector3(0.35f, 0f, 0.75f);
        private static readonly Vector3 PawnOffsetSouth = new Vector3(-0.75f, 0f, 0.35f); // South 基准值
        private static readonly Vector3 PawnOffsetWest = new Vector3(-0.35f, 0f, -0.75f);

        /// <summary>
        /// pawn 相对建筑中心的绘制偏移（世界坐标）。
        /// 根据建筑当前朝向 Rotation.AsInt 选择对应方向的偏移值。
        ///   AsInt: 0=North, 1=East, 2=South, 3=West
        /// </summary>
     
        public Vector3 PawnDrawOffset
        {
            get
            {
                switch (Rotation.AsInt)
                {
                    case 0: return new Vector3(-0.72f, 0f, 0.00f);   // North
                    case 1: return new Vector3(-0.10f, 0f, 0.45f);   // East
                    case 2: return new Vector3(-0.70f, 0f, 0.05f);   // South（你调好的值）
                    case 3: return new Vector3(0.00f, 0f, -0.65f);  // West
                }
                return Vector3.zero;
            }
        }
    

        // ─────────────── IThingHolderWithDrawnPawn 的三个成员 ───────────────
        // 这三个属性告诉 RimWorld "仓内 pawn 应当以什么姿态、朝向、高度被绘制"。

        /// <summary>pawn 绘制时的 y 高度（世界坐标），比建筑本身高 1/26 格。</summary>
        public float HeldPawnDrawPos_Y => DrawPos.y + 1f / 26f;

        /// <summary>pawn 身体朝向（弧度）。Rotation.AsAngle 让朝向跟随建筑，+180f 让头朝向"建筑背面"。</summary>
        public float HeldPawnBodyAngle => Rotation.AsAngle + 180f;

        /// <summary>pawn 姿势：躺在地上面朝上。</summary>
        public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;

        // ─────────────── "选择小人"Gizmo 用的图标 ───────────────
        // 贴图路径
        private const string InsertPawnIconPath = "UI/Gizmos/InsertBandwidthCasket";

        // 缓存已加载的贴图
        [Unsaved(false)]
        private static Texture2D cachedInsertPawnTex;

        /// <summary>懒加载"放入小人"按钮图标。</summary>
        private static Texture2D InsertPawnTex
        {
            get
            {
                if (cachedInsertPawnTex == null)
                    cachedInsertPawnTex = ContentFinder<Texture2D>.Get(InsertPawnIconPath);
                return cachedInsertPawnTex;
            }
        }

        // ─────────────── 常用属性 ───────────────

        /// <summary>容器内的 Pawn（没有则为 null）。</summary>
        public Pawn ContainedPawn => ContainedThing as Pawn;

        /// <summary>是否真的有一个活着的 pawn 在仓内。</summary>
        public bool HasPawn => ContainedPawn != null && !ContainedPawn.Dead;

        /// <summary>获取本建筑上的带宽节点组件。</summary>
        public CompBandwidthCasketNode BandNode => GetComp<CompBandwidthCasketNode>();

        // ─────────────── 容器是否接受某个 Thing ───────────────
        /// <summary>
        /// 判断该 Thing 能否进入容器,且容器必须为空。
        /// </summary>
        public override bool Accepts(Thing thing)
        {
            if (innerContainer.Count > 0) return false;
            if (!(thing is Pawn p)) return false;
            if (p.Dead || p.Destroyed) return false;
            if (!p.RaceProps.Humanlike) return false;
            if (p.IsMutant) return false;
            return true;
        }

        // ─────────────── 放入流程 ───────────────
        public override bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
        {
            // 交给基类执行实际放入（含 DeSpawn、innerContainer.TryAdd 等）
            if (!base.TryAcceptThing(thing, allowSpecialEffects))
                return false;

            // 入场音效
            if (allowSpecialEffects)
                SoundDefOf.CryptosleepCasket_Accept.PlayOneShot(new TargetInfo(Position, Map));

            // 通知带宽节点：占位状态变了
            BandNode?.OnCasketOccupancyChanged();
            return true;
        }

        // ─────────────── 弹出流程 ───────────────
        public override void EjectContents()
        {
            // 1) 在 base.EjectContents() 之前遍历 innerContainer，因为基类会把容器清空。
            //    给每个 pawn：恢复出生所需组件、加音效，加低温休眠症（与低温舱一致）。
            foreach (Thing t in innerContainer)
            {
                if (t is Pawn p)
                {
                    PawnComponentsUtility.AddComponentsForSpawn(p);
                    p.filth.GainFilth(ThingDefOf.Filth_Slime);
                    if (p.RaceProps.IsFlesh)
                        p.health.AddHediff(HediffDefOf.CryptosleepSickness);
                }
            }

            // 2) 弹出音效。Destroyed 时 Map 可能已失效，所以要判断一下防止 NPE。
            if (!Destroyed)
            {
                SoundDefOf.CryptosleepCasket_Eject.PlayOneShot(
                    SoundInfo.InMap(new TargetInfo(Position, Map)));
            }

            // 3) 清协调状态（此刻 HasPawn 还是 true，不能依赖 OnCasketOccupancyChanged 的判断）
            BandNode?.ClearTuning();

            // 4) 交给基类真正弹出内容物
            base.EjectContents();
        }

        // ─────────────── 拆除时也要清协调 ───────────────
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            BandNode?.ClearTuning();
            base.Destroy(mode);
        }

        // ─────────────── 仓内 pawn 的绘制 ───────────────
        /// <summary>
        /// 按渲染阶段绘制容器内的 Pawn。
        /// 必须用 DynamicDrawPhaseAt 而不是 DrawAt，否则头发、衣服、眼睛等分层无法正确画出。
        /// </summary>
        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            base.DynamicDrawPhaseAt(phase, drawLoc, flip);

            Pawn pawn = ContainedPawn;
            if (pawn != null)
            {
                pawn.Drawer.renderer.DynamicDrawPhaseAt(
                    phase,
                    drawLoc + PawnDrawOffset,   // 加上朝向相关的偏移
                    null,
                    neverAimWeapon: true);       // 不显示武器瞄准动画
            }
        }

        // ─────────────── 命令条 Gizmos ───────────────
        public override IEnumerable<Gizmo> GetGizmos()
        {
            // 先让基类输出它自己的 Gizmos（弹出按钮等）
            foreach (Gizmo g in base.GetGizmos())
                yield return g;

            // 已经有 pawn 在里面就不显示"选择小人"按钮
            if (HasPawn) yield break;

            Command_Action cmd = new Command_Action
            {
                defaultLabel = "InsertPerson".Translate() + "...",
                defaultDesc = "RSC_BandwidthCasket_InsertPersonDesc".Translate(),
                icon = InsertPawnTex,
                action = ShowSelectPawnMenu
            };

            // 没通电时置灰
            CompPowerTrader power = GetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn)
                cmd.Disable("NoPower".Translate().CapitalizeFirst());

            yield return cmd;
        }

        /// <summary>弹出浮动菜单，列出所有可以进入的小人。</summary>
        private void ShowSelectPawnMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            foreach (Pawn p in Map.mapPawns.AllPawnsSpawned)
            {
                if (!Accepts(p)) continue;                                          // 不符合条件
                if (p.Downed) continue;                                             // 倒地不能自己进去
                if (!p.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))   // 不可达
                    continue;

                Pawn pawn = p;  // 闭包捕获
                options.Add(new FloatMenuOption(
                    pawn.LabelShortCap,
                    () =>
                    {
                        // 派发"进入带宽仓"任务
                        pawn.jobs.TryTakeOrderedJob(
                            JobMaker.MakeJob(BandwidthCasket_JobDefOf.BandwidthCasket_Enter, this),
                            JobTag.Misc);
                    },
                    MenuOptionPriority.Default,
                    null,
                    pawn));   // 第 5 参 revalidateClickTarget，让菜单在 pawn 消失时自动刷新
            }

            // 没有任何可进入的小人时，给一个占位项
            if (!options.Any())
                options.Add(new FloatMenuOption("NoExtractablePawns".Translate(), null));

            Find.WindowStack.Add(new FloatMenu(options));
        }

        // ─────────────── 右键浮动菜单 ───────────────
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption opt in base.GetFloatMenuOptions(selPawn))
                yield return opt;

            if (HasPawn) yield break;

            // 断电检查
            CompPowerTrader power = GetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn)
            {
                yield return new FloatMenuOption(
                    "CannotEnterBuilding".Translate(this) + ": " + "NoPower".Translate().CapitalizeFirst(),
                    null);
                yield break;
            }

            if (!Accepts(selPawn))
            {
                yield return new FloatMenuOption(
                    "CannotEnterBuilding".Translate(this) + ": " + "NotAllowed".Translate(),
                    null);
                yield break;
            }
            if (!selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
            {
                yield return new FloatMenuOption(
                    "CannotEnterBuilding".Translate(this) + ": " + "NoPath".Translate().CapitalizeFirst(),
                    null);
                yield break;
            }

            yield return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption("EnterBuilding".Translate(this), () =>
                {
                    selPawn.jobs.TryTakeOrderedJob(
                        JobMaker.MakeJob(BandwidthCasket_JobDefOf.BandwidthCasket_Enter, this),
                        JobTag.Misc);
                }),
                selPawn, this);
        }
    }
}