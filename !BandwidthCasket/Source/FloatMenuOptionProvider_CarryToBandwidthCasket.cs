using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace BandwidthCasket
{
    /// <summary>
    /// 右键倒地/囚犯 pawn 时提供"搬运到带宽仓"选项。
    /// 前提是该 pawn 已经被某个带宽仓预定（Gizmo 里选过）。
    /// </summary>
    public class FloatMenuOptionProvider_CarryToBandwidthCasket : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool RequiresManipulation => true;

        protected override bool AppliesInt(FloatMenuContext context)
        {
            Pawn sel = context.FirstSelectedPawn;
            if (sel.IsMutant)
                return sel.mutant.Def.canCarryPawns;
            return true;
        }

        public override bool TargetPawnValid(Pawn pawn, FloatMenuContext context)
        {
            if (pawn.Faction != Faction.OfPlayer
                && !pawn.IsPrisonerOfColony
                && !pawn.Downed)
            {
                return false;
            }
            if (pawn.InAggroMentalState) return false;
            if (pawn.def?.thingClass?.Name?.EndsWith("VehiclePawn") == true) return false;
            return base.TargetPawnValid(pawn, context);
        }

        protected override FloatMenuOption GetSingleOptionFor(
            Pawn clickedPawn, FloatMenuContext context)
        {
            // 找地图上已经预定了这个 pawn 的 casket
            var casket = context.map.listerBuildings
                .AllBuildingsColonistOfClass<Building_BandwidthCasket>()
                .FirstOrDefault(c => c.SelectedPawn == clickedPawn
                    && c.Faction == Faction.OfPlayer);

            if (casket == null) return null;

            Pawn carrier = context.FirstSelectedPawn;
            string text = "RSC_BandwidthCasket_MakeLoad".Translate();

            if (!casket.CanCarryTo(carrier, out var reason))
                return new FloatMenuOption(text + " (" + reason + ")", null);

            return new FloatMenuOption(text, () =>
            {
                if (casket.SelectedPawn != clickedPawn
                    || !casket.CanCarryTo(carrier, out var _))
                {
                    Messages.Message("RSC_BandwidthCasket_Unavailable".Translate(),
                        clickedPawn, MessageTypeDefOf.RejectInput, false);
                }
                else
                {
                    carrier.jobs.TryTakeOrderedJob(casket.MakeLoadJob(), JobTag.Misc);
                }
            });
        }
    }
}