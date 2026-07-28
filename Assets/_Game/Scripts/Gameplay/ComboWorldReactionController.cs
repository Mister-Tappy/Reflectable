using UnityEngine;

namespace Reflectable
{
    public sealed class ComboWorldReactionController : MonoBehaviour
    {
        [SerializeField] SpriteRenderer groundGlow;
        [SerializeField] SpriteRenderer energyCircle;
        ComboPresentationConfig config;
        int combo;

        public void Initialize(ComboPresentationConfig value, Sprite circle, Sprite ring, Material material)
        {
            config = value;
            Setup(groundGlow, circle, material);
            Setup(energyCircle, ring, material);
            SetCombo(0);
        }

        public void SetCombo(int value)
        {
            combo = Mathf.Max(0, value);
            if (!config) return;
            ComboTierSettings tier = config.TierFor(combo);
            if (groundGlow)
            {
                groundGlow.enabled = combo >= 100;
                var color = tier.primaryColor;
                color.a = combo >= 500 ? .16f : .08f;
                groundGlow.color = color;
            }
            if (energyCircle)
            {
                energyCircle.enabled = combo >= 1000;
                var color = tier.secondaryColor;
                color.a = .22f;
                energyCircle.color = color;
            }
        }

        void Update()
        {
            if (combo <= 0) return;
            float time = Time.unscaledTime;
            if (groundGlow)
                groundGlow.transform.localScale = new Vector3(8f + Mathf.Sin(time * 2f) * .18f, 1.2f, 1f);
            if (energyCircle && energyCircle.enabled)
            {
                energyCircle.transform.Rotate(0f, 0f, Time.unscaledDeltaTime * 9f);
                energyCircle.transform.localScale = Vector3.one * (7.2f + Mathf.Sin(time * 2.8f) * .2f);
            }
        }

        static void Setup(SpriteRenderer renderer, Sprite sprite, Material material)
        {
            if (!renderer) return;
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
        }
    }
}
