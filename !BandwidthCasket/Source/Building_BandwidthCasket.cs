using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace BandwidthCasket
{
    public class Building_BandwidthCasket : Building_Casket,
                                             ISuspendableThingHolder,
                                             IThingHolderWithDrawnPawn
    {
        bool ISuspendableThingHolder.IsContentsSuspended => true;

        // ─────────────── 状态字段 ───────────────
        /// <summary>被"预定"要送进来的 pawn；由 Gizmo 设置，由 WorkGiver 或右键触发搬运。</summary>
        private Pawn selectedPawn;

        public Pawn SelectedPawn => selectedPawn;
        public bool WaitingForPawn => selectedPawn != null && !HasPawn;

        // ─────────────── 4 个方向的绘制偏移 ───────────────
        public Vector3 PawnDrawOffset
        {
            get
            {
                switch (Rotation.AsInt)
                {
                    case 0: return new Vector3(-0.72f, 0f, 0.00f);   // North
                    case 1: return new Vector3(-0.10f, 0f, 0.45f);   // East
                    case 2: return new Vector3(-0.70f, 0f, 0.05f);   // South
                    case 3: return new Vector3(0.00f, 0f, -0.65f);   // West
                }
                return Vector3.zero;
            }
        }

        // ─────────────── IThingHolderWithDrawnPawn ───────────────
        public float HeldPawnDrawPos_Y => DrawPos.y + 1f / 26f;
        public float HeldPawnBodyAngle => Rotation.AsAngle + 180f;
        public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;

        // ─────────────── 图标 ───────────────
        private const string InsertPawnIconPath = "UI/Gizmos/InsertBandwidthCasket";

        [Unsaved(false)] private static Texture2D cachedInsertPawnTex;
        [Unsaved(false)] private static Texture2D cachedCancelTex;

        private static Texture2D InsertPawnTex
        {
            get
            {
                if (cachedInsertPawnTex == null)
                    cachedInsertPawnTex = ContentFinder<Texture2D>.Get(InsertPawnIconPath);
                return cachedInsertPawnTex;
            }
        }

        private static Texture2D CancelTex
        {
            get
            {
                if (cachedCancelTex == null)
                    cachedCancelTex = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", true);
                return cachedCancelTex;
            }
        }

        // ─────────────── 常用属性 ───────────────
        public Pawn ContainedPawn => ContainedThing as Pawn;
        public bool HasPawn => ContainedPawn != null && !ContainedPawn.Dead;
        public CompBandwidthCasketNode BandNode => GetComp<CompBandwidthCasketNode>();

        // ─────────────── 存档 ───────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref selectedPawn, "selectedPawn");
        }

        // ─────────────── 容器过滤 ───────────────
        public override bool Accepts(Thing thing)
        {
            if (innerContainer.Count > 0) return false;
            if (!(thing is Pawn p)) return false;
            if (p.Dead || p.Destroyed) return false;
            if (!p.RaceProps.Humanlike) return false;
            if (p.IsMutant) return false;
            return true;
        }

        // ─────────────── 可选性判断 ───────────────
        /// <summary>pawn 是否可以被送入带宽仓（包含容器可接受、通电、派系检查）。</summary>
        public bool CanLoad(Pawn p)
        {
            if (!Spawned || Faction != Faction.OfPlayer) return false;
            if (p == null || !p.Spawned) return false;
            if (p.Map != Map) return false;
            if (p.InMentalState) return false;
            if (p.IsQuestLodger()) return false;
            if (!Accepts(p)) return false;

            CompPowerTrader power = GetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) return false;

            // 玩家派系成员 或 囚犯
            if (p.Faction != Faction.OfPlayer && !p.IsPrisonerOfColony) return false;

            return true;
        }

        /// <summary>该 pawn 能否自己走进带宽仓（false = 需要搬运）。</summary>
        public static bool CanWalkInto(Pawn pawn)
        {
            if (pawn == null) return false;
            if (pawn.Downed) return false;
            if (pawn.IsPrisonerOfColony) return false;
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) return false;
            return true;
        }

        // ─────────────── 选择 ───────────────
        /// <summary>
        /// 预定某个 pawn 送入带宽仓。
        /// - 能自己走的：立即派 Enter job
        /// - 需要搬运的：只设置 selectedPawn，让 WorkGiver 或右键来派 CarryTo job
        /// </summary>
        public bool SelectPawn(Pawn target)
        {
            if (!Spawned || Faction != Faction.OfPlayer || !CanLoad(target))
                return false;

            // 清掉地图上其他 casket 对同一 pawn 的预定，避免重复
            foreach (var other in Map.listerBuildings
                .AllBuildingsColonistOfClass<Building_BandwidthCasket>())
            {
                if (other != this && other.selectedPawn == target)
                    other.selectedPawn = null;
            }

            selectedPawn = target;

            if (CanWalkInto(target))
            {
                target.jobs.TryTakeOrderedJob(
                    JobMaker.MakeJob(BandwidthCasket_JobDefOf.BandwidthCasket_Enter, this),
                    JobTag.Misc);
            }
            // 需要搬运的：等待 WorkGiver_CarryToBandwidthCasket 自动派 job

            return true;
        }

        // ─────────────── 取消 ───────────────
        public void CancelLoad()
        {
            Pawn target = selectedPawn;
            selectedPawn = null;
            if (target == null) return;

            // 目标是能自己走的，可能正在跑 Enter job
            if (target.CurJob != null
                && target.CurJob.def == BandwidthCasket_JobDefOf.BandwidthCasket_Enter
                && target.CurJob.targetA.Thing == this)
            {
                target.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            // 找搬运者的 CarryTo job
            if (Map != null)
            {
                foreach (Pawn p in Map.mapPawns.AllPawnsSpawned)
                {
                    if (p.CurJob == null) continue;
                    if (p.CurJob.def != BandwidthCasket_JobDefOf.BandwidthCasket_CarryTo) continue;
                    if (p.CurJob.targetA.Thing != target) continue;
                    if (p.CurJob.targetB.Thing != this) continue;

                    p.jobs.EndCurrentJob(JobCondition.InterruptForced);

                    if (p.carryTracker != null && p.carryTracker.CarriedThing == target)
                        p.carryTracker.TryDropCarriedThing(p.Position, ThingPlaceMode.Near, out _);
                    break;
                }
            }
        }

        // ─────────────── 搬运可行性 ───────────────
        /// <summary>搬运者能否把 SelectedPawn 搬进来。</summary>
        public bool CanCarryTo(Pawn carrier, out string reason)
        {
            reason = "RSC_BandwidthCasket_Unavailable".Translate();

            if (!Spawned || Faction != Faction.OfPlayer) return false;
            if (selectedPawn == null || !CanLoad(selectedPawn)) return false;
            if (carrier == null || carrier == selectedPawn) return false;
            if (!carrier.Spawned || carrier.Map != Map) return false;
            if (carrier.Downed || carrier.InMentalState) return false;
            if (!carrier.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return false;
            if (!carrier.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) return false;
            if (this.IsForbidden(carrier) || selectedPawn.IsForbidden(carrier)) return false;
            if (Map.designationManager.DesignationOn(this, DesignationDefOf.Deconstruct) != null)
                return false;

            reason = "RSC_BandwidthCasket_Blocked".Translate();
            if (!carrier.CanReserveAndReach(this, PathEndMode.InteractionCell, Danger.Deadly))
                return false;
            if (!carrier.CanReserveAndReach(selectedPawn, PathEndMode.OnCell, Danger.Deadly))
                return false;

            reason = null;
            return true;
        }

        /// <summary>生成搬运 job。</summary>
        public Job MakeLoadJob()
        {
            Job job = JobMaker.MakeJob(
                BandwidthCasket_JobDefOf.BandwidthCasket_CarryTo,
                selectedPawn, this);
            job.count = 1;
            return job;
        }

        // ─────────────── 放入 ───────────────
        public override bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
        {
            if (!base.TryAcceptThing(thing, allowSpecialEffects))
                return false;

            selectedPawn = null;

            if (allowSpecialEffects)
                SoundDefOf.CryptosleepCasket_Accept.PlayOneShot(new TargetInfo(Position, Map));

            BandNode?.OnCasketOccupancyChanged();
            return true;
        }

        // ─────────────── Tick ───────────────
        protected override void Tick()
        {
            base.Tick();

            // 只清理"确实已经不能再用"的目标。
            // 关键：不要检查 Map —— 被搬运途中 Takee 会短暂 despawn，Map 会变成 null / 不一致，
            // 检查它会误杀正常搬运中的 selectedPawn。
            if (selectedPawn != null && !HasPawn)
            {
                if (selectedPawn.Dead || selectedPawn.Destroyed)
                    selectedPawn = null;
            }
        }

        // ─────────────── 弹出 ───────────────
        public override void EjectContents()
        {
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

            if (!Destroyed)
            {
                SoundDefOf.CryptosleepCasket_Eject.PlayOneShot(
                    SoundInfo.InMap(new TargetInfo(Position, Map)));
            }

            BandNode?.ClearTuning();
            base.EjectContents();
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            selectedPawn = null;
            BandNode?.ClearTuning();
            base.Destroy(mode);
        }

        // ─────────────── 绘制 ───────────────
        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            base.DynamicDrawPhaseAt(phase, drawLoc, flip);

            Pawn pawn = ContainedPawn;
            if (pawn != null)
            {
                pawn.Drawer.renderer.DynamicDrawPhaseAt(
                    phase,
                    drawLoc + PawnDrawOffset,
                    null,
                    neverAimWeapon: true);
            }
        }

        // ─────────────── Gizmos ───────────────
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
                yield return g;

            if (HasPawn) yield break;

            CompPowerTrader power = GetComp<CompPowerTrader>();
            bool hasPower = power == null || power.PowerOn;

            if (selectedPawn != null)
            {
                // 装载中：显示"取消装载"
                yield return new Command_Action
                {
                    defaultLabel = "CommandCancelLoad".Translate(),
                    defaultDesc = "CommandCancelLoadDesc".Translate(),
                    icon = CancelTex,
                    activateSound = SoundDefOf.Designate_Cancel,
                    action = CancelLoad
                };
            }
            else
            {
                Command_Action cmd = new Command_Action
                {
                    defaultLabel = "InsertPerson".Translate() + "...",
                    defaultDesc = "RSC_BandwidthCasket_InsertPersonDesc".Translate(),
                    icon = InsertPawnTex,
                    action = ChooseOccupant
                };
                if (!hasPower)
                    cmd.Disable("NoPower".Translate().CapitalizeFirst());
                yield return cmd;
            }
        }

        /// <summary>弹菜单：列出所有可送入带宽仓的 pawn。</summary>
        private void ChooseOccupant()
        {
            List<FloatMenuOption> list = new List<FloatMenuOption>();

            foreach (Pawn target in Map.mapPawns.AllPawnsSpawned
                .Where(CanLoad)
                .OrderBy(p => p.LabelShort))
            {
                Pawn local = target;

                // 用 12 参版 FloatMenuOption，第 3 参传 pawn 显示头像
                list.Add(new FloatMenuOption(
                    local.LabelShortCap,                    // label（不带后缀）
                    () => SelectPawn(local),                // action
                    local,                                  // iconThing ← 自动画头像
                    Color.white,                            // iconColor
                    MenuOptionPriority.Default,             // priority
                    null,                                   // mouseoverGuiAction
                    null,                                   // revalidateClickTarget
                    0f,                                     // extraPartWidth
                    null,                                   // extraPartOnGUI
                    null,                                   // revalidateWorldClickTarget
                    true,                                   // playSelectionSound
                    0));                                    // orderInCategory
            }

            if (list.Count == 0)
                list.Add(new FloatMenuOption("NoExtractablePawns".Translate(), null));

            Find.WindowStack.Add(new FloatMenu(list));
        }

        // ─────────────── 右键菜单 ───────────────
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption opt in base.GetFloatMenuOptions(selPawn))
                yield return opt;

            if (HasPawn) yield break;

            if (selectedPawn != null && selectedPawn != selPawn)
            {
                yield return new FloatMenuOption(
                    "CannotEnterBuilding".Translate(this) + ": "
                        + "RSC_BandwidthCasket_AlreadySelected".Translate(),
                    null);
                yield break;
            }

            if (!CanLoad(selPawn))
            {
                CompPowerTrader power = GetComp<CompPowerTrader>();
                if (power != null && !power.PowerOn)
                {
                    yield return new FloatMenuOption(
                        "CannotEnterBuilding".Translate(this) + ": "
                            + "NoPower".Translate().CapitalizeFirst(),
                        null);
                }
                else
                {
                    yield return new FloatMenuOption(
                        "CannotEnterBuilding".Translate(this) + ": "
                            + "RSC_BandwidthCasket_NotAllowed".Translate(),
                        null);
                }
                yield break;
            }

            // 只有能自己走的才允许右键直接进入
            if (!CanWalkInto(selPawn))
            {
                yield return new FloatMenuOption(
                    "CannotEnterBuilding".Translate(this) + ": "
                        + "RSC_BandwidthCasket_NeedsCarrier".Translate(),
                    null);
                yield break;
            }

            if (!selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
            {
                yield return new FloatMenuOption(
                    "CannotEnterBuilding".Translate(this) + ": "
                        + "NoPath".Translate().CapitalizeFirst(),
                    null);
                yield break;
            }

            yield return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption("EnterBuilding".Translate(this), () =>
                {
                    SelectPawn(selPawn);
                }),
                selPawn, this);
        }

        // ─────────────── 检查面板 ───────────────
        public override string GetInspectString()
        {
            string s = base.GetInspectString();
            if (selectedPawn != null && !HasPawn)
            {
                if (!s.NullOrEmpty()) s += "\n";
                s += "WaitingForPawn".Translate(selectedPawn.Named("PAWN")).Resolve();
            }
            return s;
        }
    }
}