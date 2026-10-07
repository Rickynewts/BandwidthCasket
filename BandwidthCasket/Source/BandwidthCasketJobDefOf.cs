using RimWorld;
using Verse;

namespace BandwidthCasket
{
    /// <summary>
    /// JobDef 静态引用表。
    /// [DefOf] 特性让 RimWorld 在加载 XML 后自动把对应 defName 的 JobDef 赋值到这里，
    /// 这样 C# 里就可以直接写 BandwidthCasket_JobDefOf.BandwidthCasket_Enter 拿到这个 JobDef。
    /// 对应 XML：Defs/JobDefs/Jobs.xml 中的 <defName>BandwidthCasket_Enter</defName>
    /// </summary>
    [DefOf]
    public static class BandwidthCasket_JobDefOf
    {
        /// <summary>进入带宽仓的 JobDef（用于小人自己走进去的场景）。</summary>
        public static JobDef BandwidthCasket_Enter;

        // 静态构造函数：强制 DefOfHelper 在本类第一次被访问时完成初始化，
        // 确保上面字段已被 RimWorld 从 XML 赋值好，而不是 null。
        static BandwidthCasket_JobDefOf()
            => DefOfHelper.EnsureInitializedInCtor(typeof(BandwidthCasket_JobDefOf));
    }
}