using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BandwidthCasket
{
    public class Gene_RemoveAllPassions : Gene
    {
        // 记录被修改过的技能，以及它们原始的热情，方便 PostRemove 恢复
        private List<SkillDef> affectedSkills;
        private List<Passion> originalPassions;

        public override void PostAdd()
        {
            base.PostAdd();

            // 已经应用过就不要重复应用（读档时某些流程可能再次触发）
            if (affectedSkills != null && affectedSkills.Count > 0) return;
            if (!Active) return;

            affectedSkills = new List<SkillDef>();
            originalPassions = new List<Passion>();

            foreach (SkillRecord record in pawn.skills.skills)
            {
                if (record.passion == Passion.None) continue;

                affectedSkills.Add(record.def);
                originalPassions.Add(record.passion);
                record.passion = Passion.None;
            }

            pawn.skills.DirtyAptitudes();
        }

        public override void PostRemove()
        {
            base.PostRemove();

            if (!affectedSkills.NullOrEmpty())
            {
                for (int i = 0; i < affectedSkills.Count; i++)
                {
                    SkillRecord record = pawn.skills.GetSkill(affectedSkills[i]);
                    if (record != null)
                        record.passion = originalPassions[i];
                }
                pawn.skills.DirtyAptitudes();

                affectedSkills.Clear();
                originalPassions.Clear();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref affectedSkills, "affectedSkills", LookMode.Def);
            Scribe_Collections.Look(ref originalPassions, "originalPassions");
        }
    }
}