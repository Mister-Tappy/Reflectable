using UnityEngine;

namespace Reflectable
{
    /// <summary>Single source of truth for finite-stage progression and encounter pacing.</summary>
    public readonly struct ReflectableStageDefinition
    {
        public readonly int BlockTarget, BossInterval, StartingBlockCount, EarlyBlocksPerTurn, LateBlocksPerTurn;
        public readonly float RewardMultiplier, NormalHpStart, NormalHpEnd, HpRandomVariation, GemChance, BombChance;
        public readonly AnimationCurve HpCurve;
        public readonly ReflectableStageData Data;
        public ReflectableStageDefinition(int blockTarget, int bossInterval, float rewardMultiplier, float normalHpStart, float normalHpEnd, AnimationCurve hpCurve)
        { BlockTarget = blockTarget; BossInterval = bossInterval; RewardMultiplier = rewardMultiplier; NormalHpStart = normalHpStart; NormalHpEnd = normalHpEnd; HpCurve = hpCurve; StartingBlockCount=3;EarlyBlocksPerTurn=2;LateBlocksPerTurn=6;HpRandomVariation=.10f;GemChance=.05f;BombChance=.05f;Data=null; }
        ReflectableStageDefinition(ReflectableStageData data)
        { BlockTarget=data.clearRequirement;BossInterval=data.bossInterval;RewardMultiplier=data.rewardMultiplier;NormalHpStart=data.normalHpStart;NormalHpEnd=data.normalHpEnd;HpCurve=data.hpCurve;StartingBlockCount=data.startingBlockCount;EarlyBlocksPerTurn=data.earlyBlocksPerTurn;LateBlocksPerTurn=data.lateBlocksPerTurn;HpRandomVariation=data.hpRandomVariation;GemChance=data.gemChance;BombChance=data.bombChance;Data=data; }
        public static ReflectableStageDefinition FromData(ReflectableStageData data)=>new ReflectableStageDefinition(data);

        public float NormalHpAt(float progress) => Mathf.Lerp(NormalHpStart, NormalHpEnd, Mathf.Clamp01(HpCurve.Evaluate(Mathf.Clamp01(progress))));
    }

    public static class ReflectableStageConfig
    {
        // These curves are the authoritative, editable difficulty settings. Their X axis is
        // destroyed blocks / stage target; their Y axis is normalized normal-block HP.
        static AnimationCurve Curve(params Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }

        static readonly ReflectableStageDefinition[] Definitions =
        {
            new ReflectableStageDefinition(200, 25, 1.00f, 30f, 450f, Curve(
                new Keyframe(0f, 0f), new Keyframe(.10f, .05f), new Keyframe(.25f, .13f), new Keyframe(.40f, .21f),
                new Keyframe(.60f, .34f), new Keyframe(.80f, .62f), new Keyframe(.95f, .85f), new Keyframe(1f, 1f))),
            new ReflectableStageDefinition(300, 25, 1.25f, 375f, 1100f, Curve(
                new Keyframe(0f, 0f), new Keyframe(.20f, .18f), new Keyframe(.50f, .42f), new Keyframe(.75f, .70f), new Keyframe(1f, 1f))),
            new ReflectableStageDefinition(450, 25, 1.50f, 850f, 2400f, Curve(
                new Keyframe(0f, 0f), new Keyframe(.25f, .20f), new Keyframe(.50f, .36f), new Keyframe(.75f, .73f), new Keyframe(1f, 1f))),
            new ReflectableStageDefinition(650, 25, 1.80f, 1850f, 5100f, Curve(
                new Keyframe(0f, 0f), new Keyframe(.25f, .18f), new Keyframe(.50f, .35f), new Keyframe(.75f, .76f), new Keyframe(1f, 1f))),
            new ReflectableStageDefinition(900, 25, 2.10f, 3700f, 11000f, Curve(
                new Keyframe(0f, 0f), new Keyframe(.25f, .18f), new Keyframe(.50f, .36f), new Keyframe(.75f, .72f), new Keyframe(1f, 1f)))
            ,new ReflectableStageDefinition(1200,30,2.50f,7000f,18000f,Curve(new Keyframe(0,0),new Keyframe(.5f,.4f),new Keyframe(1,1)))
            ,new ReflectableStageDefinition(1500,30,2.90f,12000f,30000f,Curve(new Keyframe(0,0),new Keyframe(.5f,.42f),new Keyframe(1,1)))
            ,new ReflectableStageDefinition(1850,35,3.40f,20000f,48000f,Curve(new Keyframe(0,0),new Keyframe(.5f,.44f),new Keyframe(1,1)))
            ,new ReflectableStageDefinition(2250,35,4.00f,32000f,75000f,Curve(new Keyframe(0,0),new Keyframe(.5f,.46f),new Keyframe(1,1)))
            ,new ReflectableStageDefinition(2700,40,4.80f,50000f,115000f,Curve(new Keyframe(0,0),new Keyframe(.5f,.48f),new Keyframe(1,1)))
        };

        public static int StageCount=>Mathf.Max(10,ReflectableStageCatalog.StageCount);
        public static ReflectableStageData DataFor(int stage)=>ReflectableStageCatalog.Get(Mathf.Clamp(stage,1,StageCount));

        public static ReflectableStageDefinition For(int stage)
        {
            stage=Mathf.Clamp(stage,1,StageCount);var data=ReflectableStageCatalog.Get(stage);if(data)return ReflectableStageDefinition.FromData(data);var definition = Definitions[Mathf.Clamp(stage, 1, Definitions.Length) - 1];
            if (definition.BlockTarget <= 0 || definition.BossInterval <= 0)
                Debug.LogError($"[Stage] ERROR: Stage {stage} has invalid progression data.");
            return definition;
        }
    }
}
