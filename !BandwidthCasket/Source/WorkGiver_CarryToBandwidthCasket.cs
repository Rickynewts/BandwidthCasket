using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace BandwidthCasket
{
    /// <summary>
    /// 自动搬运：扫描地图上的带宽仓，找有 SelectedPawn 的，派搬运 job。
    /// 这是"Gizmo 选人后自动有人去搬"的关键。
    /// </summary>
    public class WorkGiver_CarryToBandwidthCasket : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.listerBuildings
                .AllBuildingsColonistOfClass<Building_BandwidthCasket>();
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            // 非强制任务时需要 Hauling 工作类型启用
            if (!forced && (pawn.Drafted
                || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling)
                || pawn.workSettings == null
                || !pawn.workSettings.WorkIsActive(WorkTypeDefOf.Hauling)))
            {
                return false;
            }

            if (!(t is Building_BandwidthCasket casket))
                return false;

            if (casket.SelectedPawn == null)
                return false;

            // 能自己走的不需要搬运
            if (Building_BandwidthCasket.CanWalkInto(casket.SelectedPawn))
                return false;

            string reason;
            return casket.CanCarryTo(pawn, out reason);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!HasJobOnThing(pawn, t, forced))
                return null;
            return ((Building_BandwidthCasket)t).MakeLoadJob();
        }
    }
}