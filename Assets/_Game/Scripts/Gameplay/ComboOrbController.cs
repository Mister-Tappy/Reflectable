using System.Collections;
using UnityEngine;

namespace Reflectable
{
    public sealed class ComboOrbController : MonoBehaviour
    {
        [SerializeField] Transform visualRoot;
        [SerializeField] SpriteRenderer glow;
        [SerializeField] SpriteRenderer core;
        [SerializeField] SpriteRenderer flame;
        [SerializeField] SpriteRenderer ring;
        [SerializeField] SpriteRenderer lightning;
        [SerializeField] SpriteRenderer corona;
        [SerializeField] Transform comboDisplayRoot;
        [SerializeField] TextMesh comboNumber;
        [SerializeField] TextMesh comboNumberGlow;
        [SerializeField] TextMesh comboCaption;
        [SerializeField] TextMesh[] milestoneLabels = new TextMesh[0];
        [SerializeField] Font displayFont;

        ComboPresentationConfig config;
        Vector3 anchor;
        Vector3 orbBaseScale;
        Coroutine pulseRoutine;
        Coroutine summonRoutine;
        Coroutine despawnRoutine;
        int labelCursor;
        int combo;
        int evolutionStage;
        float orbPulse = 1f;
        bool forming;
        bool orbFormed;
        Color targetNumberColor = Color.white;
        Color displayedNumberColor = Color.white;
        float displayAlpha = 1f;

        public bool IsSummoned => gameObject.activeSelf;
        public bool IsOrbFormed => orbFormed;
        public Vector3 WorldPosition => transform.position;

        public void Initialize(ComboPresentationConfig value, Sprite circle, Sprite ringSprite, Material material, Font font)
        {
            config = value;
            Font resolvedFont = displayFont ? displayFont : font;
            Setup(glow, circle, material);
            Setup(core, circle, material);
            Setup(flame, circle, material);
            Setup(ring, ringSprite, material);
            Setup(lightning, ringSprite, material);
            Setup(corona, ringSprite, material);
            SetupText(comboNumber, resolvedFont);
            SetupText(comboNumberGlow, resolvedFont);
            SetupText(comboCaption, resolvedFont);
            if (milestoneLabels != null)
                for (int i = 0; i < milestoneLabels.Length; i++)
                {
                    var label = milestoneLabels[i];
                    if (!label) continue;
                    SetupText(label, resolvedFont);
                    label.gameObject.SetActive(false);
                }
            orbBaseScale = Vector3.one * (config ? config.orbBaseSize : 1f);
            gameObject.SetActive(false);
        }

        public void Summon(Vector3 position, int value)
        {
            Debug.Log("[Combo Orb] Summon " + value + " at " + position, this);
            BeginCombo(position, value);
            FormOrb(position, value);
        }

        public void BeginCombo(Vector3 position, int value)
        {
            if (despawnRoutine != null) { StopCoroutine(despawnRoutine); despawnRoutine = null; }
            anchor = position;
            transform.position = anchor;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            if (visualRoot) visualRoot.localScale = Vector3.zero;
            orbFormed = false;
            if (comboDisplayRoot)
            {
                comboDisplayRoot.localScale = Vector3.one;
                comboDisplayRoot.gameObject.SetActive(true);
            }
            gameObject.SetActive(true);
            SetDisplayAlpha(1f);
            SetCombo(value);
        }

        public void FormOrb(Vector3 position, int value)
        {
            if (!IsSummoned) BeginCombo(position, value);
            if (orbFormed || forming) return;
            orbFormed = true;
            SetCombo(value, true);
            if (summonRoutine != null) StopCoroutine(summonRoutine);
            summonRoutine = StartCoroutine(SummonRoutine());
        }

        public void SetCombo(int value, bool immediate = false)
        {
            if (!IsSummoned || !config) return;
            combo = Mathf.Max(1, value);
            ComboTierSettings tier = config.TierFor(combo);
            string number = combo.ToString();
            if (comboNumber) comboNumber.text = number;
            if (comboNumberGlow) comboNumberGlow.text = number;
            if (comboCaption) comboCaption.text = "COMBO";
            targetNumberColor = tier.primaryColor;
            if (immediate)
            {
                displayedNumberColor = targetNumberColor;
                ApplyTextColors(displayedNumberColor, tier);
            }
            ApplyEvolution(tier);
            if (!immediate)
            {
                orbPulse = Mathf.Max(orbPulse, 1.1f);
                if (pulseRoutine != null) StopCoroutine(pulseRoutine);
                pulseRoutine = StartCoroutine(PulseRoutine(tier));
            }
        }

        public void ShowMilestone(string message, Color color)
        {
            if (!IsSummoned || string.IsNullOrWhiteSpace(message) || milestoneLabels == null || milestoneLabels.Length == 0) return;
            TextMesh label = milestoneLabels[labelCursor++ % milestoneLabels.Length];
            if (!label) return;
            label.text = message;
            label.color = color;
            label.gameObject.SetActive(true);
            StartCoroutine(MilestoneRoutine(label));
        }

        public void AbsorbPulse(Color color)
        {
            if (!IsSummoned) return;
            if (glow) glow.color = new Color(color.r, color.g, color.b, Mathf.Max(glow.color.a, .62f));
        }

        public void Despawn()
        {
            if (!IsSummoned) return;
            if (pulseRoutine != null) { StopCoroutine(pulseRoutine); pulseRoutine = null; }
            if (despawnRoutine != null) StopCoroutine(despawnRoutine);
            despawnRoutine = StartCoroutine(DespawnRoutine());
        }

        void Update()
        {
            if (!IsSummoned || !config) return;
            float time = Time.unscaledTime;
            float hover = Mathf.Sin(time * config.orbHoverSpeed) * config.orbHoverAmount;
            float drift = Mathf.Sin(time * config.orbHoverSpeed * .63f) * config.orbHorizontalDrift;
            transform.position = anchor + new Vector3(drift, hover, 0f);
            if (visualRoot && orbFormed)
            {
                visualRoot.Rotate(0f, 0f, config.orbRotationSpeed * Time.unscaledDeltaTime);
                float breath = 1f + Mathf.Sin(time * config.orbPulseSpeed) * config.orbBreathAmount;
                orbPulse = Mathf.MoveTowards(orbPulse, 1f, Time.unscaledDeltaTime * 1.9f);
                if (!forming) visualRoot.localScale = orbBaseScale * (breath * orbPulse);
            }
            ComboTierSettings tier = config.TierFor(combo);
            Color desiredColor = evolutionStage >= 7
                ? Color.HSVToRGB(Mathf.Repeat(time * .16f, 1f), .72f, 1f)
                : targetNumberColor;
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 8f);
            displayedNumberColor = Color.Lerp(displayedNumberColor, desiredColor, blend);
            ApplyTextColors(displayedNumberColor, tier);
            if (lightning && lightning.enabled)
            {
                var tint = lightning.color;
                tint.a = Mathf.Lerp(.16f, .72f, Mathf.PerlinNoise(time * 15f, combo * .01f));
                lightning.color = tint;
                lightning.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 21f) * 18f);
            }
            if (corona && corona.enabled)
                corona.transform.localRotation = Quaternion.Euler(0f, 0f, -time * config.orbRotationSpeed * .45f);
        }

        void ApplyEvolution(ComboTierSettings tier)
        {
            evolutionStage = EvolutionStage(combo);
            float size = 1f + evolutionStage * .075f;
            if (core)
            {
                core.enabled = true;
                core.color = tier.primaryColor;
                core.transform.localScale = Vector3.one * (.62f + evolutionStage * .035f);
            }
            SetLayer(glow, tier.secondaryColor, .25f + tier.glow * .16f, 1.18f + evolutionStage * .08f, true);
            SetLayer(flame, Color.Lerp(tier.primaryColor, new Color(1f, .24f, .04f), .35f), .15f + tier.fire * .19f, .82f + evolutionStage * .06f, evolutionStage >= 1);
            SetLayer(ring, tier.secondaryColor, .28f + evolutionStage * .035f, .95f + evolutionStage * .09f, evolutionStage >= 2);
            SetLayer(lightning, Color.Lerp(tier.secondaryColor, new Color(.72f, .28f, 1f), .55f), .48f, 1.18f + evolutionStage * .09f, evolutionStage >= 4);
            SetLayer(corona, Color.Lerp(tier.primaryColor, Color.yellow, .48f), .30f + evolutionStage * .035f, 1.35f + evolutionStage * .10f, evolutionStage >= 6);
            orbBaseScale = Vector3.one * (config.orbBaseSize * size);
        }

        IEnumerator SummonRoutine()
        {
            forming = true;
            float duration = config ? config.orbFormationDuration : .32f;
            float gatherDuration = duration * .74f;
            float settleDuration = duration - gatherDuration;
            for (float time = 0f; time < gatherDuration; time += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(time / gatherDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                if (visualRoot) visualRoot.localScale = Vector3.LerpUnclamped(Vector3.zero, orbBaseScale * 1.12f, eased);
                yield return null;
            }
            for (float time = 0f; time < settleDuration; time += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(time / settleDuration);
                if (visualRoot) visualRoot.localScale = Vector3.Lerp(orbBaseScale * 1.12f, orbBaseScale, Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }
            if (visualRoot) visualRoot.localScale = orbBaseScale;
            forming = false;
            summonRoutine = null;
        }

        IEnumerator PulseRoutine(ComboTierSettings tier)
        {
            if (!comboDisplayRoot) yield break;
            Vector3 start = Vector3.one;
            float amount = Mathf.Clamp(config ? config.comboTextPopScale : tier.punchScale, 1.18f, 1.28f);
            float totalDuration = config ? config.comboTextAnimationDuration : .21f;
            float inDuration = totalDuration * .38f;
            float outDuration = totalDuration - inDuration;
            for (float time = 0f; time < inDuration; time += Time.unscaledDeltaTime)
            {
                float progress = time / inDuration;
                comboDisplayRoot.localScale = Vector3.LerpUnclamped(start, start * amount, 1f - Mathf.Pow(1f - progress, 3f));
                yield return null;
            }
            for (float time = 0f; time < outDuration; time += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(time / outDuration);
                float bounce = 1f + Mathf.Sin(progress * Mathf.PI * 2f) * .035f * (1f - progress);
                comboDisplayRoot.localScale = Vector3.one * Mathf.Lerp(amount, bounce, Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }
            comboDisplayRoot.localScale = start;
            pulseRoutine = null;
        }

        IEnumerator MilestoneRoutine(TextMesh label)
        {
            Transform item = label.transform;
            Vector3 startPosition = item.localPosition;
            for (float time = 0f; time < .12f; time += Time.unscaledDeltaTime)
            {
                float progress = time / .12f;
                item.localScale = Vector3.one * Mathf.Lerp(.2f, 1.08f, 1f - Mathf.Pow(1f - progress, 3f));
                var tint = label.color; tint.a = progress; label.color = tint;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(config ? config.announcementDuration : .55f);
            for (float time = 0f; time < .18f; time += Time.unscaledDeltaTime)
            {
                float progress = time / .18f;
                var tint = label.color; tint.a = 1f - progress; label.color = tint;
                item.localPosition = startPosition + Vector3.up * (progress * .35f);
                yield return null;
            }
            item.localPosition = startPosition;
            item.localScale = Vector3.one;
            label.gameObject.SetActive(false);
        }

        IEnumerator DespawnRoutine()
        {
            if (summonRoutine != null) { StopCoroutine(summonRoutine); summonRoutine = null; }
            forming = false;
            Vector3 visualStart = visualRoot ? visualRoot.localScale : Vector3.one;
            Vector3 displayStart = comboDisplayRoot ? comboDisplayRoot.localScale : Vector3.one;
            for (float time = 0f; time < .32f; time += Time.unscaledDeltaTime)
            {
                float progress = time / .32f;
                if (visualRoot)
                    visualRoot.localScale = Vector3.Lerp(
                        visualStart * (1f + Mathf.Sin(progress * Mathf.PI) * .25f),
                        Vector3.zero,
                        progress * progress);
                if (comboDisplayRoot) comboDisplayRoot.localScale = Vector3.Lerp(displayStart, Vector3.one * .72f, progress);
                SetDisplayAlpha(1f - progress);
                yield return null;
            }
            gameObject.SetActive(false);
            orbFormed = false;
            transform.localScale = Vector3.one;
            if (visualRoot) visualRoot.localScale = orbBaseScale;
            if (comboDisplayRoot) comboDisplayRoot.localScale = Vector3.one;
            despawnRoutine = null;
        }

        void ApplyTextColors(Color color, ComboTierSettings tier)
        {
            if (comboNumber)
            {
                color.a = displayAlpha;
                comboNumber.color = color;
            }
            if (comboCaption)
            {
                Color captionColor = Color.Lerp(Color.white, color, .3f);
                captionColor.a = displayAlpha;
                comboCaption.color = captionColor;
            }
            if (comboNumberGlow)
            {
                float alpha = displayAlpha * Mathf.Clamp(.13f + evolutionStage * .045f + tier.glow * .035f, .13f, .58f);
                comboNumberGlow.color = new Color(color.r, color.g, color.b, alpha);
            }
        }

        void SetDisplayAlpha(float alpha)
        {
            displayAlpha = Mathf.Clamp01(alpha);
            ApplyTextColors(targetNumberColor, config ? config.TierFor(combo) : default);
        }

        static int EvolutionStage(int value) =>
            value >= 1000 ? 7 : value >= 500 ? 6 : value >= 300 ? 5 : value >= 200 ? 4 : value >= 100 ? 3 : value >= 50 ? 2 : value >= 20 ? 1 : 0;

        static void Setup(SpriteRenderer renderer, Sprite sprite, Material material)
        {
            if (!renderer) return;
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
        }

        static void SetupText(TextMesh text, Font font)
        {
            if (!text || !font) return;
            text.font = font;
            MeshRenderer renderer = text.GetComponent<MeshRenderer>();
            if (renderer) renderer.sharedMaterial = font.material;
        }

        static void SetLayer(SpriteRenderer renderer, Color color, float alpha, float scale, bool enabled)
        {
            if (!renderer) return;
            renderer.enabled = enabled;
            color.a = Mathf.Clamp01(alpha);
            renderer.color = color;
            renderer.transform.localScale = Vector3.one * scale;
        }
    }

    public sealed class ComboEnergyParticleFx : MonoBehaviour
    {
        SpriteRenderer spriteRenderer;
        Vector3 start;
        Vector3 target;
        Color color;
        float age;
        float duration;
        float spiralRadius;
        float spiralPhase;

        public void Initialize(Sprite sprite, Material material)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sharedMaterial = material;
            spriteRenderer.sortingOrder = 525;
            gameObject.SetActive(false);
        }

        public void Play(Vector3 origin, Vector3 destination, Color tint, float travelDuration, float size)
        {
            start = origin;
            target = destination;
            color = tint;
            duration = Mathf.Max(.12f, travelDuration);
            spiralRadius = Random.Range(.18f, .48f);
            spiralPhase = Random.Range(0f, Mathf.PI * 2f);
            age = 0f;
            transform.position = origin;
            transform.localScale = Vector3.one * size;
            spriteRenderer.color = tint;
            gameObject.SetActive(true);
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(age / duration);
            float eased = progress * progress * (3f - 2f * progress);
            float spiral = Mathf.Sin(progress * Mathf.PI) * spiralRadius;
            float angle = spiralPhase + progress * Mathf.PI * 4f;
            Vector3 bend = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * spiral;
            transform.position = Vector3.Lerp(start, target, eased) + bend;
            transform.localScale *= 1f + Time.unscaledDeltaTime * 1.2f;
            var tint = color; tint.a = 1f - Mathf.SmoothStep(.72f, 1f, progress); spriteRenderer.color = tint;
            if (age >= duration) gameObject.SetActive(false);
        }
    }
}
