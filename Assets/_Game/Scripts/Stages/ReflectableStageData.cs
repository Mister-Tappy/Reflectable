using UnityEngine;

namespace Reflectable
{
    [CreateAssetMenu(menuName = "Reflectable/Stage Data", fileName = "Stage_Data")]
    public sealed class ReflectableStageData : ScriptableObject
    {
        [Header("Identity")]
        [Min(1)] public int stageNumber = 1;
        public string stageName = "MEADOW";
        public string difficultyLabel = "NORMAL";
        [TextArea] public string description;

        [Header("Presentation")]
        public GameObject stageVisualPrefab;
        public Sprite stageSelectPreview;
        public Color stageSelectTint = Color.white;
        public Gradient backgroundGradient = new Gradient();

        [Header("Stage Progression")]
        [Min(1)] public int clearRequirement = 200;
        [Min(1)] public int bossInterval = 25;
        [Min(1)] public int startingBlockCount = 3;
        [Min(1)] public int earlyBlocksPerTurn = 2;
        [Min(1)] public int lateBlocksPerTurn = 6;
        [Min(.01f)] public float rewardMultiplier = 1f;

        [Header("Normal Block Durability")]
        [Min(1)] public float normalHpStart = 30f;
        [Min(1)] public float normalHpEnd = 450f;
        public AnimationCurve hpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Range(0f, .5f)] public float hpRandomVariation = .10f;

        [Header("Spawn Weights")]
        [Range(0f, 1f)] public float gemChance = .05f;
        [Range(0f, 1f)] public float bombChance = .05f;
        public ReflectableStageBlockPalette blockPalette;
    }
}
