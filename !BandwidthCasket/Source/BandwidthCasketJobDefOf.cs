using RimWorld;
using Verse;

namespace BandwidthCasket
{
    [DefOf]
    public static class BandwidthCasket_JobDefOf
    {
        public static JobDef BandwidthCasket_Enter;
        public static JobDef BandwidthCasket_CarryTo;

        static BandwidthCasket_JobDefOf()
            => DefOfHelper.EnsureInitializedInCtor(typeof(BandwidthCasket_JobDefOf));
    }
}