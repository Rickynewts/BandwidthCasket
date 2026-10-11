using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace BandwidthCasket
{
    /// <summary>把 SelectedPawn 搬运进带宽仓。</summary>
    public class JobDriver_CarryToBandwidthCasket : JobDriver
    {
        private Pawn Takee => job.targetA.Pawn;
        private Building_BandwidthCasket Casket => job.targetB.Thing as Building_BandwidthCasket;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Reserve(Takee, job, 1, -1, null, errorOnFailed))
                return pawn.Reserve(Casket, job, 1, -1, null, errorOnFailed);
            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);
            this.FailOn(() => Casket == null
                || Casket.SelectedPawn != Takee
                || !Casket.Accepts(Takee)
                || Takee.InMentalState
                || Takee.IsQuestLodger()
                || (Takee.Faction != Faction.OfPlayer && !Takee.IsPrisonerOfColony));

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell)
                .FailOnDespawnedNullOrForbidden(TargetIndex.A)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.A);

            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.InteractionCell);

            yield return Toils_General.Wait(180, TargetIndex.B)
                .FailOnCannotTouch(TargetIndex.B, PathEndMode.InteractionCell)
                .WithProgressBarToilDelay(TargetIndex.B);

            Toil load = ToilMaker.MakeToil("CarryToBandwidthCasket");
            load.initAction = () =>
            {
                if (!Casket.TryAcceptThing(Takee))
                    EndJobWith(JobCondition.Incompletable);
            };
            load.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return load;
        }
    }
}