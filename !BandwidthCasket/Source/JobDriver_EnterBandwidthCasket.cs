using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace BandwidthCasket
{
    /// <summary>
    /// "进入带宽仓"任务的执行逻辑。
    /// 流程：走到交互格 → 等待 2 秒 → 把自己放进容器。
    /// </summary>
    public class JobDriver_EnterBandwidthCasket : JobDriver
    {
        /// <summary>目标建筑（Job 的 targetA）。</summary>
        private Building_BandwidthCasket Casket => (Building_BandwidthCasket)job.targetA.Thing;

        /// <summary>预定建筑，防止多个小人同时抢占。</summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
            => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 目标消失/被禁用时中止任务
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);

            // 1) 走到建筑的交互格
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            // 2) 原地等待 120 tick（约 2 秒），期间显示进度条
            var wait = Toils_General.Wait(120, TargetIndex.A);
            wait.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            wait.WithProgressBarToilDelay(TargetIndex.A);
            yield return wait;

            // 3) 瞬发 toil：把小人放进容器
            var enter = ToilMaker.MakeToil("EnterBandwidthCasket");
            enter.initAction = () =>
            {
                var casket = Casket;
                var p = pawn;

                // DeSpawnOrDeselect：把小人从地图上移除（同时取消选中）。
                // 返回 true 表示之前是选中的状态，之后要补一次选中容器里的新 pawn。
                bool deselected = p.DeSpawnOrDeselect();

                if (casket.TryAcceptThing(p) && deselected)
                    Find.Selector.Select(p, playSound: false, forceDesignatorDeselect: false);
            };
            enter.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return enter;
        }
    }
}