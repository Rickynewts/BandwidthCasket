using Verse;

namespace BandwidthCasket
{
    /// <summary>
    /// CompBandwidthCasketNode 的属性配置类，字段由 XML 提供。
    /// 对应 XML 里的：
    ///   <li Class="BandwidthCasket.CompProperties_BandwidthCasketNode">
    ///     <hediff>...</hediff> ...
    ///   </li>
    /// </summary>
    public class CompProperties_BandwidthCasketNode : CompProperties
    {
        /// <summary>协调完成后加在机械师身上的 Hediff（提供带宽加成）。</summary>
        public HediffDef hediff;

        /// <summary>重新协调所需的"游戏天数"（默认 3 天，与原版带宽节点一致）。</summary>
        public float retuneDays = 3f;

        /// <summary>首次协调所需的"秒数"。</summary>
        public float tuneSeconds = 5f;

        /// <summary>协调完成时播放的音效。</summary>
        public SoundDef tuningCompleteSound;

        /// <summary>该节点提供的机械带宽数值（例如 7 就 +7）。</summary>
        public int bandwidth = 1;

        //携带 BandwidthCasket_Booster 基因时的带宽
        public int boostedBandwidth = 12;

        public CompProperties_BandwidthCasketNode()
        {
            compClass = typeof(CompBandwidthCasketNode);
        }
    }
}