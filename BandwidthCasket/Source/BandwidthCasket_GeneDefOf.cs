using RimWorld;
using Verse;

namespace BandwidthCasket
{
    [DefOf]
    public static class BandwidthCasket_GeneDefOf
    {
        public static GeneDef BandwidthCasket_Booster;

        static BandwidthCasket_GeneDefOf()
            => DefOfHelper.EnsureInitializedInCtor(typeof(BandwidthCasket_GeneDefOf));
    }
}