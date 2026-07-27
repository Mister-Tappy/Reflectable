using UnityEngine;

namespace Reflectable
{
    /// <summary>Central balance authority for progression, combat, and encounter durability.</summary>
    public static class ReflectableGameBalance
    {
        public const int StartingDamage = 10;
        public const float PowerMultiplier = 1.30f;

        // Requirements to advance from the current level. These intentionally rise
        // gradually so Stage 1 reaches a meaningful build before its final boss.
        static readonly int[] ExpRequirements = { 6, 10, 15, 21, 28, 36, 45, 55, 66, 78, 91, 105, 120, 136, 153, 171, 190, 210, 231, 253, 276, 300, 325, 351, 378, 406, 435, 465, 496, 528 };

        public static int ProjectileDamage(int power) => Mathf.RoundToInt(StartingDamage * Mathf.Pow(PowerMultiplier, power));
        public static int ExpRequired(int level)
        {
            int index = Mathf.Max(0, level - 1);
            return index < ExpRequirements.Length ? ExpRequirements[index] : ExpRequirements[ExpRequirements.Length - 1] + (index - ExpRequirements.Length + 1) * 24;
        }
        public static float ComboMultiplier(int combo) => combo < 10 ? 1f : combo < 20 ? 1.05f : combo < 30 ? 1.10f : combo < 50 ? 1.20f : 1.30f;
        public static float ComboExpMultiplier(int combo) => combo < 10 ? 1f : combo < 20 ? 1.05f : combo < 30 ? 1.10f : combo < 50 ? 1.15f : 1.20f;
        public static int BlockExperience(ReflectableBlockType type, int maximumHp, float stageProgress)
        {
            int baseExp = type == ReflectableBlockType.Tough ? 5 : type == ReflectableBlockType.Armored ? 7 : type == ReflectableBlockType.Elite ? 10 : type == ReflectableBlockType.Anchor ? 12 : type == ReflectableBlockType.Gem ? 8 : type == ReflectableBlockType.Bomb ? 10 : 3;
            float encounterValue = baseExp + Mathf.Max(0, maximumHp / 75);
            return Mathf.RoundToInt(encounterValue * Mathf.Lerp(1f, 3f, Mathf.Clamp01(stageProgress)));
        }
        public static int BossExperience(int maximumHp) => 60 + Mathf.Max(0, maximumHp / 80);

        public static int BlockHp(int stage, int destroyed, ReflectableBlockType type)
        {
            var definition = ReflectableStageConfig.For(stage);
            float progress = Mathf.Clamp01(destroyed / (float)definition.BlockTarget);
            float tier = type == ReflectableBlockType.Tough ? 1.35f : type == ReflectableBlockType.Armored ? 1.70f : type == ReflectableBlockType.Elite ? 2.05f : type == ReflectableBlockType.Anchor ? 2.55f : 1f;
            return Mathf.RoundToInt(definition.NormalHpAt(progress) * tier * Random.Range(.90f, 1.10f));
        }
        public static int BossHp(int stage,int bossNumber,bool finalBoss)
        {
            if(stage==1){int[] values={400,850,1400,2200,2900,3700,4800,7000};return values[Mathf.Clamp(finalBoss?7:bossNumber-1,0,7)];}
            float value=1100f+(bossNumber-1)*950f;return Mathf.RoundToInt(value*(1f+(stage-2)*.55f)*(finalBoss?1.35f:1f));
        }
        public static float ExpMultiplier(ReflectableBlockType type)=>type==ReflectableBlockType.Tough?1.5f:type==ReflectableBlockType.Armored?2f:type==ReflectableBlockType.Elite?3f:type==ReflectableBlockType.Anchor?3.5f:1f;
    }
}
