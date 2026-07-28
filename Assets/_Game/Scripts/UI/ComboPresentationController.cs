using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reflectable
{
    public sealed class ComboPresentationController : MonoBehaviour
    {
        [SerializeField] ComboPresentationConfig config;
        [SerializeField] RectTransform comboRoot;
        [SerializeField] TMP_Text comboNumber;
        [SerializeField] TMP_Text comboCaption;
        [SerializeField] TMP_Text hypeLabel;
        [SerializeField] Image glow;
        [SerializeField] Image flame;
        [SerializeField] Image impactSplash;
        [SerializeField] Image lightning;
        [SerializeField] Image cosmicOverlay;
        [SerializeField] TMP_Text[] announcementPool = new TMP_Text[0];
        [SerializeField] CharacterCutInController cutIn;
        [SerializeField] GameplayHudController hud;
        [SerializeField] bool worldOrbPrimary = true;
        PlayerCharacterPresenter presenter;
        Coroutine punch;
        Coroutine reset;
        int announcementCursor;
        int currentCombo;
        int currentTier;

        public ComboPresentationConfig Config => config;
        public ArcadeEffectQuality Quality => config ? config.quality : ArcadeEffectQuality.Medium;

        public void BindCharacter(PlayerCharacterPresenter value)
        {
            presenter = value;
            if (cutIn) cutIn.Bind(value);
        }

        public void SetCombo(int combo)
        {
            if (!config || !comboRoot || !comboNumber) return;
            if (reset != null) { StopCoroutine(reset); reset = null; }
            currentCombo = Mathf.Max(0, combo);
            var tier = config.TierFor(currentCombo);
            currentTier = TierIndex(currentCombo);
            if (worldOrbPrimary)
            {
                if (comboRoot) comboRoot.gameObject.SetActive(false);
                hud?.SetComboTier(0, Color.white);
                if (config.TryGetMilestone(currentCombo, out var worldMilestone))
                {
                    if (currentCombo == 100) hud?.PulsePortrait();
                    if (worldMilestone.characterCutIn && cutIn) cutIn.Show(currentCombo, worldMilestone.cutInDuration);
                }
                return;
            }
            comboNumber.SetText("{0}", currentCombo);
            comboCaption.text = "COMBO";
            hypeLabel.text = tier.hypeWord;
            ApplyTier(tier);
            if (punch != null) StopCoroutine(punch);
            punch = StartCoroutine(PunchRoutine(tier));
            hud?.SetComboTier(currentTier, tier.primaryColor);

            if (config.TryGetMilestone(currentCombo, out var milestone))
            {
                Announce(milestone.announcement, tier);
                if (currentCombo == 100) hud?.PulsePortrait();
                if (milestone.characterCutIn && cutIn) cutIn.Show(currentCombo, milestone.cutInDuration);
            }
        }

        public void ResetCombo()
        {
            currentCombo = 0;
            currentTier = 0;
            if (worldOrbPrimary)
            {
                if (comboRoot) comboRoot.gameObject.SetActive(false);
                cutIn?.HideImmediate();
                hud?.SetComboTier(0,Color.white);
                return;
            }
            if (punch != null) { StopCoroutine(punch); punch = null; }
            if (reset != null) StopCoroutine(reset);
            reset = StartCoroutine(ResetRoutine());
            cutIn?.HideImmediate();
            hud?.SetComboTier(0, Color.white);
        }

        public void PreviewCutIn(int milestone)
        {
            if (!cutIn) return;
            float duration = config && config.TryGetMilestone(milestone, out var settings) ? settings.cutInDuration : 1.2f;
            cutIn.Show(milestone, duration);
        }

        void Update()
        {
            if (!config || currentCombo <= 0) return;
            if (currentTier >= 6 && comboNumber)
            {
                Color a = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * .18f, 1f), .75f, 1f);
                Color b = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * .18f + .28f, 1f), .75f, 1f);
                comboNumber.colorGradient = new VertexGradient(a, b, Color.white, a);
            }
            if (lightning && currentTier >= 3)
            {
                var color = lightning.color;
                color.a = (.05f + currentTier * .025f) * (.4f + Mathf.PerlinNoise(Time.unscaledTime * 13f, 0f));
                lightning.color = color;
                lightning.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 18f) * 3f);
            }
        }

        void ApplyTier(ComboTierSettings tier)
        {
            comboNumber.color = tier.primaryColor;
            comboNumber.colorGradient = new VertexGradient(tier.secondaryColor, tier.primaryColor, tier.primaryColor, tier.secondaryColor);
            comboNumber.enableVertexGradient = currentTier >= 2;
            if (comboCaption) comboCaption.color = Color.Lerp(Color.white, tier.primaryColor, .5f);
            if (hypeLabel) hypeLabel.color = tier.secondaryColor;
            SetAlpha(glow, Mathf.Clamp01(.08f + tier.glow * .22f));
            SetAlpha(flame, Mathf.Clamp01(tier.fire * .30f));
            SetAlpha(impactSplash, currentTier >= 3 ? Mathf.Clamp01(.12f + currentTier * .035f) : 0f);
            SetAlpha(lightning, Mathf.Clamp01(tier.lightning * .14f));
            SetAlpha(cosmicOverlay, currentTier >= 7 ? .32f : currentTier >= 5 ? .08f : 0f);
        }

        IEnumerator PunchRoutine(ComboTierSettings tier)
        {
            float angle = Random.Range(-tier.rotation, tier.rotation);
            Vector3 start = Vector3.one;
            for (float time = 0f; time < config.punchInDuration; time += Time.unscaledDeltaTime)
            {
                float progress = time / config.punchInDuration;
                comboRoot.localScale = Vector3.LerpUnclamped(start, start * tier.punchScale, 1f - Mathf.Pow(1f - progress, 3f));
                comboRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, angle, progress));
                yield return null;
            }
            for (float time = 0f; time < config.punchOutDuration; time += Time.unscaledDeltaTime)
            {
                float progress = time / config.punchOutDuration;
                comboRoot.localScale = Vector3.LerpUnclamped(start * tier.punchScale, start, Mathf.SmoothStep(0f, 1f, progress));
                comboRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(angle, 0f, progress));
                yield return null;
            }
            comboRoot.localScale = start;
            comboRoot.localRotation = Quaternion.identity;
            punch = null;
        }

        void Announce(string message, ComboTierSettings tier)
        {
            if (string.IsNullOrWhiteSpace(message) || announcementPool == null || announcementPool.Length == 0) return;
            TMP_Text label = announcementPool[announcementCursor++ % announcementPool.Length];
            if (!label) return;
            label.text = message;
            label.color = tier.secondaryColor;
            label.gameObject.SetActive(true);
            StartCoroutine(AnnouncementRoutine(label));
        }

        IEnumerator AnnouncementRoutine(TMP_Text label)
        {
            var rect = label.rectTransform;
            for (float time = 0f; time < .12f; time += Time.unscaledDeltaTime)
            {
                float progress = time / .12f;
                rect.localScale = Vector3.one * Mathf.Lerp(.2f, 1.18f, 1f - Mathf.Pow(1f - progress, 3f));
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-7f, 3f, progress));
                var color = label.color; color.a = progress; label.color = color;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(config.announcementDuration);
            for (float time = 0f; time < .18f; time += Time.unscaledDeltaTime)
            {
                float progress = time / .18f;
                var color = label.color; color.a = 1f - progress; label.color = color;
                rect.localScale = Vector3.one * Mathf.Lerp(1.18f, 1.45f, progress);
                yield return null;
            }
            label.gameObject.SetActive(false);
        }

        IEnumerator ResetRoutine()
        {
            float duration = config ? config.comboBreakFade : .48f;
            Color numberStart = comboNumber ? comboNumber.color : Color.white;
            float flameStart = flame ? flame.color.a : 0f;
            float lightningStart = lightning ? lightning.color.a : 0f;
            float cosmicStart = cosmicOverlay ? cosmicOverlay.color.a : 0f;
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                float progress = time / duration;
                if (comboRoot) comboRoot.localScale = Vector3.Lerp(Vector3.one, Vector3.one * .82f, progress);
                if (comboNumber) { var color = numberStart; color.a = 1f - progress; comboNumber.color = color; }
                SetAlpha(flame, (1f - progress) * flameStart);
                SetAlpha(lightning, (1f - progress) * lightningStart);
                SetAlpha(cosmicOverlay, (1f - progress) * cosmicStart);
                yield return null;
            }
            if (comboNumber) comboNumber.text = "";
            if (comboCaption) comboCaption.text = "";
            if (hypeLabel) hypeLabel.text = "";
            if (comboRoot) { comboRoot.localScale = Vector3.one; comboRoot.localRotation = Quaternion.identity; }
            SetAlpha(glow, 0f);
            SetAlpha(flame, 0f);
            SetAlpha(impactSplash, 0f);
            SetAlpha(lightning, 0f);
            SetAlpha(cosmicOverlay, 0f);
            reset = null;
        }

        static int TierIndex(int combo) => combo >= 1000 ? 7 : combo >= 500 ? 6 : combo >= 300 ? 5 : combo >= 200 ? 4 : combo >= 100 ? 3 : combo >= 50 ? 2 : combo >= 20 ? 1 : 0;
        static void SetAlpha(Graphic graphic, float alpha)
        {
            if (!graphic) return;
            var color = graphic.color;
            color.a = Mathf.Clamp01(alpha);
            graphic.color = color;
        }
    }
}
