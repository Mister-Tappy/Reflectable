using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reflectable
{
    public sealed class CharacterCutInController : MonoBehaviour
    {
        [SerializeField] RectTransform panel;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Image backgroundStrip;
        [SerializeField] Image aura;
        [SerializeField] Image characterImage;
        [SerializeField] Image[] afterimages = new Image[0];
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text abilityLabel;
        PlayerCharacterPresenter presenter;
        Coroutine cutInRoutine;

        public void Bind(PlayerCharacterPresenter value) => presenter = value;

        public void Show(int milestone, float duration)
        {
            CharacterData data = presenter ? presenter.CurrentData : null;
            if (!data || !data.CutInSprite || !panel) return;
            if (cutInRoutine != null) StopCoroutine(cutInRoutine);
            cutInRoutine = StartCoroutine(ShowRoutine(data, milestone, duration));
        }

        public void HideImmediate()
        {
            if (cutInRoutine != null) StopCoroutine(cutInRoutine);
            cutInRoutine = null;
            if (canvasGroup) { canvasGroup.alpha = 0f; canvasGroup.blocksRaycasts = false; }
            if (panel) panel.anchoredPosition = new Vector2(-720f, 0f);
            if (characterImage) characterImage.enabled = false;
            if (afterimages != null) foreach (var image in afterimages) if (image) image.enabled = false;
        }

        IEnumerator ShowRoutine(CharacterData data, int milestone, float duration)
        {
            gameObject.SetActive(true);
            Color theme = data.comboAuraColor == default ? data.themeColor : data.comboAuraColor;
            if (characterImage)
            {
                characterImage.enabled = true;
                characterImage.sprite = data.CutInSprite;
                characterImage.preserveAspect = true;
                characterImage.color =
                    characterImage.sprite.name == "UISprite" || characterImage.sprite.name == "Background"
                        ? Color.Lerp(theme, Color.white, .12f)
                        : Color.white;
                characterImage.rectTransform.anchoredPosition = data.cutInOffset;
                float milestoneScale = milestone >= 1000 ? 1.28f : milestone >= 500 ? 1.12f : 1f;
                characterImage.rectTransform.localScale = Vector3.one * data.cutInScale * milestoneScale;
            }
            if (backgroundStrip) backgroundStrip.color = new Color(theme.r * .35f, theme.g * .35f, theme.b * .45f, milestone >= 500 ? .86f : .68f);
            if (aura) aura.color = new Color(theme.r, theme.g, theme.b, milestone >= 500 ? .55f : .34f);
            if (nameLabel) nameLabel.text = data.displayName.ToUpperInvariant();
            if (titleLabel) titleLabel.text = data.title.ToUpperInvariant();
            if (abilityLabel) abilityLabel.text = data.CutInAbilityName.ToUpperInvariant();
            if (canvasGroup) { canvasGroup.alpha = 0f; canvasGroup.blocksRaycasts = false; }

            Vector2 hidden = new Vector2(-720f, 0f);
            Vector2 shown = milestone >= 1000 ? new Vector2(125f, 0f) : new Vector2(70f, 0f);
            panel.anchoredPosition = hidden;
            SetupAfterimages(data.CutInSprite, theme, hidden);
            for (float time = 0f; time < .18f; time += Time.unscaledDeltaTime)
            {
                float progress = 1f - Mathf.Pow(1f - time / .18f, 3f);
                panel.anchoredPosition = Vector2.LerpUnclamped(hidden, shown + Vector2.right * 24f, progress);
                if (canvasGroup) canvasGroup.alpha = progress;
                AnimateEnergy(milestone);
                UpdateAfterimages(progress, hidden, shown);
                yield return null;
            }
            for (float time = 0f; time < .08f; time += Time.unscaledDeltaTime)
            {
                panel.anchoredPosition = Vector2.Lerp(shown + Vector2.right * 24f, shown, time / .08f);
                AnimateEnergy(milestone);
                yield return null;
            }
            float hold = Mathf.Max(.25f, duration - .5f);
            for (float time = 0f; time < hold; time += Time.unscaledDeltaTime)
            {
                AnimateEnergy(milestone);
                yield return null;
            }
            Vector2 exit = hidden + Vector2.left * 160f;
            for (float time = 0f; time < .24f; time += Time.unscaledDeltaTime)
            {
                float progress = time / .24f;
                panel.anchoredPosition = Vector2.Lerp(shown, exit, progress * progress);
                if (canvasGroup) canvasGroup.alpha = 1f - progress;
                AnimateEnergy(milestone);
                yield return null;
            }
            if (canvasGroup) canvasGroup.alpha = 0f;
            panel.anchoredPosition = hidden;
            if (characterImage) characterImage.enabled = false;
            if (afterimages != null) foreach (var image in afterimages) if (image) image.enabled = false;
            cutInRoutine = null;
        }

        void SetupAfterimages(Sprite sprite, Color color, Vector2 hidden)
        {
            if (afterimages == null) return;
            for (int i = 0; i < afterimages.Length; i++)
            {
                var image = afterimages[i];
                if (!image) continue;
                image.enabled = true;
                image.sprite = sprite;
                image.preserveAspect = true;
                image.color = new Color(color.r, color.g, color.b, .18f - i * .035f);
                image.rectTransform.anchoredPosition = hidden + Vector2.left * (i + 1) * 35f;
                image.rectTransform.localScale = characterImage ? characterImage.rectTransform.localScale : Vector3.one;
            }
        }

        void UpdateAfterimages(float progress, Vector2 hidden, Vector2 shown)
        {
            if (afterimages == null) return;
            for (int i = 0; i < afterimages.Length; i++)
            {
                var image = afterimages[i];
                if (!image) continue;
                float delayed = Mathf.Clamp01(progress - (i + 1) * .08f);
                image.rectTransform.anchoredPosition = Vector2.Lerp(hidden - Vector2.right * (i + 1) * 45f, shown - Vector2.right * (i + 1) * 28f, delayed);
                var color = image.color;
                color.a = (1f - progress) * (.2f - i * .04f);
                image.color = color;
            }
        }

        void AnimateEnergy(int milestone)
        {
            float time = Time.unscaledTime;
            if (aura)
            {
                float pulse = 1f + Mathf.Sin(time * (milestone >= 500 ? 10f : 6f)) * .08f;
                aura.rectTransform.localScale = Vector3.one * pulse;
                aura.rectTransform.Rotate(0f, 0f, Time.unscaledDeltaTime * (milestone >= 500 ? 55f : 28f));
            }
            if (characterImage)
            {
                float sway = milestone >= 300 ? Mathf.Sin(time * 7f) * 2.2f : Mathf.Sin(time * 4f) * .8f;
                characterImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, sway);
            }
        }
    }
}
