using UnityEngine;

namespace Reflectable
{
    /// <summary>Central balance authority for progression, combat, and encounter durability.</summary>
    public static class ReflectableGameBalance
    {
        public const int StartingDamage = 10;
        public const float PowerMultiplier = 1.25f;

        static readonly Vector2Int[] StageOneNormal = { new Vector2Int(10,30),new Vector2Int(30,60),new Vector2Int(60,100),new Vector2Int(100,150),new Vector2Int(150,220),new Vector2Int(200,300),new Vector2Int(250,350),new Vector2Int(300,400) };
        static readonly Vector2Int[] StageOneTough = { new Vector2Int(30,50),new Vector2Int(60,100),new Vector2Int(100,150),new Vector2Int(150,220),new Vector2Int(220,300),new Vector2Int(300,350),new Vector2Int(350,400),new Vector2Int(400,450) };
        static readonly Vector2Int[] StageOneArmored = { new Vector2Int(45,65),new Vector2Int(100,120),new Vector2Int(150,200),new Vector2Int(200,300),new Vector2Int(300,350),new Vector2Int(350,400),new Vector2Int(400,450),new Vector2Int(450,500) };
        static readonly Vector2Int[] StageOneElite = { new Vector2Int(65,90),new Vector2Int(110,140),new Vector2Int(180,230),new Vector2Int(250,320),new Vector2Int(330,390),new Vector2Int(400,450),new Vector2Int(450,500),new Vector2Int(500,600) };

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

        // A gentle catch-up only: power that is materially behind the current board
        // can reduce new block HP by at most 18%. Ahead-of-curve runs receive no bonus.
        public static float AdaptiveHpMultiplier(int destroyed, float playerPowerScore)
        {
            float expectedPower = 10f + destroyed * .58f;
            float ratio = Mathf.Clamp(playerPowerScore / expectedPower, .55f, 1f);
            return Mathf.Lerp(.82f, 1f, (ratio - .55f) / .45f);
        }
        public static int BlockHp(int stage, int destroyed, ReflectableBlockType type)
        {
            if (stage > 1)
            {
                float p=Mathf.Clamp01(destroyed/(float)ReflectableStageConfig.For(stage).BlockTarget);
                float normal=Mathf.Lerp(300f,700f,p);
                float tier=type==ReflectableBlockType.Tough?1.35f:type==ReflectableBlockType.Armored?1.70f:type==ReflectableBlockType.Elite?2.05f:type==ReflectableBlockType.Anchor?2.55f:1f;
                return Mathf.RoundToInt(normal*tier*Random.Range(.90f,1.10f));
            }
            int band=Mathf.Clamp(destroyed/25,0,7);
            Vector2Int range=type==ReflectableBlockType.Tough?StageOneTough[band]:type==ReflectableBlockType.Armored?StageOneArmored[band]:type==ReflectableBlockType.Elite?StageOneElite[band]:type==ReflectableBlockType.Anchor?new Vector2Int(StageOneElite[band].y,StageOneElite[band].y+160):StageOneNormal[band];
            return Random.Range(range.x,range.y+1);
        }
        public static int BossHp(int stage,int bossNumber,bool finalBoss)
        {
            if(stage==1){int[] values={400,850,1400,2200,2900,3700,4800,7000};return values[Mathf.Clamp(finalBoss?7:bossNumber-1,0,7)];}
            float value=1100f+(bossNumber-1)*950f;return Mathf.RoundToInt(value*(1f+(stage-2)*.55f)*(finalBoss?1.35f:1f));
        }
        public static int BossSkillPointReward(int bossNumber, bool finalBoss) => finalBoss || bossNumber == 4 ? 2 : 1;
        public static float ExpMultiplier(ReflectableBlockType type)=>type==ReflectableBlockType.Tough?1.5f:type==ReflectableBlockType.Armored?2f:type==ReflectableBlockType.Elite?3f:type==ReflectableBlockType.Anchor?3.5f:1f;
    }
}
