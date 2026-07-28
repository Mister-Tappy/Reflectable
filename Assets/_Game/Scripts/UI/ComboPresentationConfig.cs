using System;
using UnityEngine;

namespace Reflectable
{
    public enum ArcadeEffectQuality { Low, Medium, High }

    [Serializable]
    public struct ComboTierSettings
    {
        [Min(0)] public int minimumCombo;
        public string hypeWord;
        public Color primaryColor;
        public Color secondaryColor;
        [Range(1f, 1.5f)] public float punchScale;
        [Range(0f, 8f)] public float rotation;
        [Range(0f, 3f)] public float glow;
        [Range(0f, 3f)] public float fire;
        [Range(0f, 3f)] public float lightning;
        [Range(1, 32)] public int particleCount;
        [Range(0f, .25f)] public float cameraShake;
        [Range(0f, .25f)] public float hitStop;
    }

    [Serializable]
    public struct ComboMilestoneSettings
    {
        [Min(1)] public int combo;
        public string announcement;
        public bool characterCutIn;
        [Range(.2f, 2f)] public float cutInDuration;
        [Range(0f, .25f)] public float hitStop;
        [Range(0f, .3f)] public float cameraShake;
    }

    [CreateAssetMenu(menuName = "Reflectable/UI/Combo Presentation Config", fileName = "ComboPresentationConfig")]
    public sealed class ComboPresentationConfig : ScriptableObject
    {
        [HideInInspector] public int installedHudVersion;
        public ArcadeEffectQuality quality = ArcadeEffectQuality.High;
        [Header("Animation")]
        [Range(.03f, .25f)] public float punchInDuration = .07f;
        [Range(.05f, .35f)] public float punchOutDuration = .15f;
        [Range(.1f, 1.5f)] public float comboBreakFade = .48f;
        [Range(.2f, 1.5f)] public float announcementDuration = .72f;
        [Header("Battlefield Combo Orb")]
        [Range(.35f, 2.5f)] public float orbBaseSize = .82f;
        [Range(0f, .35f)] public float orbHoverAmount = .09f;
        [Range(0f, .25f)] public float orbHorizontalDrift = .06f;
        [Range(.1f, 8f)] public float orbHoverSpeed = 1.7f;
        [Range(0f, 90f)] public float orbRotationSpeed = 13f;
        [Range(.1f, 12f)] public float orbPulseSpeed = 3.2f;
        [Range(0f, .2f)] public float orbBreathAmount = .045f;
        [Range(.25f, .4f)] public float orbFormationDuration = .32f;
        [Range(.12f, 1.2f)] public float energyTravelDuration = .42f;
        [Range(1, 8)] public int energyParticlesPerBlock = 2;
        [Header("Readability-safe post processing")]
        [Range(0f, 3f)] public float maximumBloom = 1.8f;
        [Range(0f, 20f)] public float maximumSaturation = 12f;
        [Header("Tiers")]
        public ComboTierSettings[] tiers = new ComboTierSettings[0];
        [Header("Milestones")]
        public ComboMilestoneSettings[] milestones = new ComboMilestoneSettings[0];
        [Header("Optional audio hooks")]
        public AudioClip comboIncrease;
        public AudioClip comboMilestone;
        public AudioClip criticalHit;
        public AudioClip blockDestruction;
        public AudioClip characterCutIn;
        public AudioClip characterVoice;
        public AudioClip highComboAmbience;
        public AudioClip hyperCombo;

        public ComboTierSettings TierFor(int combo)
        {
            if (tiers == null || tiers.Length == 0) return DefaultTier();
            ComboTierSettings result = tiers[0];
            for (int i = 0; i < tiers.Length; i++)
                if (combo >= tiers[i].minimumCombo) result = tiers[i];
            return result;
        }

        public bool TryGetMilestone(int combo, out ComboMilestoneSettings result)
        {
            if (milestones != null)
                for (int i = 0; i < milestones.Length; i++)
                    if (milestones[i].combo == combo) { result = milestones[i]; return true; }
            result = default;
            return false;
        }

        static ComboTierSettings DefaultTier() => new ComboTierSettings
        {
            minimumCombo = 0,
            primaryColor = Color.white,
            secondaryColor = new Color(.72f, .82f, 1f),
            punchScale = 1.12f,
            rotation = 2f
        };
    }
}
