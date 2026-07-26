using UnityEngine;

namespace Reflectable
{
    /// <summary>Single source of truth for finite-stage progression and encounter pacing.</summary>
    public readonly struct ReflectableStageDefinition
    {
        public readonly int BlockTarget, BossInterval;
        public readonly float BlockHpMultiplier, RewardMultiplier;
        public ReflectableStageDefinition(int blockTarget, int bossInterval, float blockHpMultiplier, float rewardMultiplier)
        { BlockTarget = blockTarget; BossInterval = bossInterval; BlockHpMultiplier = blockHpMultiplier; RewardMultiplier = rewardMultiplier; }
    }

    public static class ReflectableStageConfig
    {
        static readonly ReflectableStageDefinition[] Definitions =
        {
            new ReflectableStageDefinition(200, 25, 1.00f, 1.00f),
            new ReflectableStageDefinition(500, 25, 1.45f, 1.25f),
            new ReflectableStageDefinition(750, 25, 1.95f, 1.50f),
            new ReflectableStageDefinition(1000,25, 2.55f, 1.80f),
            new ReflectableStageDefinition(1300,25, 3.25f, 2.10f)
        };

        public static ReflectableStageDefinition For(int stage)
        {
            var definition = Definitions[Mathf.Clamp(stage, 1, Definitions.Length) - 1];
            if (definition.BlockTarget <= 0 || definition.BossInterval <= 0)
                Debug.LogError($"[Stage] ERROR: Stage {stage} has invalid progression data.");
            return definition;
        }
    }
}
