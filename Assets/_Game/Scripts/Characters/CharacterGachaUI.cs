using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reflectable
{
    public sealed class CharacterGachaUI : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] CharacterGachaConfig config;
        [Header("Banner")]
        [SerializeField] Image bannerBackground;
        [SerializeField] Image featuredArtwork;
        [SerializeField] Image featuredGlow;
        [SerializeField] TMP_Text featuredLabel;
        [SerializeField] TMP_Text characterName;
        [SerializeField] TMP_Text rarityText;
        [SerializeField] TMP_Text passiveName;
        [SerializeField] TMP_Text passiveDescription;
        [SerializeField] TMP_Text gemAmount;
        [SerializeField] TMP_Text drawCost;
        [SerializeField] TMP_Text statusText;
        [SerializeField] Button drawButton;
        [SerializeField] Button closeButton;
        [Header("Reveal")]
        [SerializeField] CanvasGroup revealGroup;
        [SerializeField] Image revealArtwork;
        [SerializeField] Image revealGlow;
        [SerializeField] TMP_Text revealName;
        [SerializeField] TMP_Text revealRarity;
        [SerializeField] TMP_Text revealPassive;
        [SerializeField] TMP_Text duplicateMessage;
        [SerializeField] Button skipButton;
        [SerializeField] Button continueButton;

        bool skipRequested;
        bool hasViewedReveal;

        public CharacterGachaConfig Config => config;
        public Button DrawButton => drawButton;
        public Button CloseButton => closeButton;
        public Button ContinueButton => continueButton;
        public bool HasPersistentReferences =>
            config && bannerBackground && featuredArtwork && featuredGlow && featuredLabel &&
            characterName && rarityText && passiveName && passiveDescription && gemAmount &&
            drawCost && statusText && drawButton && closeButton && revealGroup && revealArtwork &&
            revealGlow && revealName && revealRarity && revealPassive && duplicateMessage &&
            skipButton && continueButton;

        void Awake()
        {
            if (skipButton) skipButton.onClick.AddListener(RequestSkip);
        }

        void OnDestroy()
        {
            if (skipButton) skipButton.onClick.RemoveListener(RequestSkip);
        }

        public void Open(CharacterData active, CharacterDatabase database, int gems)
        {
            gameObject.SetActive(true);
            CharacterData featured = config && config.featuredCharacter ? config.featuredCharacter :
                active ? active : database ? database.Default : null;
            BindBanner(featured, gems);
            SetRevealVisible(false);
            SetDrawInteractable(true);
            if (statusText) statusText.text = active ? "ACTIVE CHARACTER  ·  " + active.displayName.ToUpperInvariant() : "SUMMON A CHARACTER";
        }

        public void RefreshCurrency(int gems)
        {
            if (gemAmount) gemAmount.text = "GEMS  " + gems;
            int cost = config ? config.drawCost : 0;
            if (drawCost) drawCost.text = "COST  " + cost + " GEMS";
            if (drawButton)
            {
                TMP_Text label = drawButton.GetComponentInChildren<TMP_Text>(true);
                if (label) label.text = "SUMMON\nCOST: " + cost + " GEMS";
            }
        }

        public void ShowInsufficientCurrency(int gems)
        {
            RefreshCurrency(gems);
            if (statusText) statusText.text = "NOT ENOUGH GEMS";
            StartCoroutine(StatusPulse());
        }

        public IEnumerator PlayReveal(CharacterData character, bool duplicate, string compensationMessage)
        {
            if (!character) yield break;
            skipRequested = false;
            SetDrawInteractable(false);
            SetRevealVisible(true);
            BindReveal(character, duplicate, compensationMessage);
            float duration = config ? config.summonAnimationDuration : 2.1f;
            if (skipButton)
            {
                skipButton.gameObject.SetActive(config && config.allowSkipAfterFirstViewing && hasViewedReveal);
                skipButton.interactable = true;
            }
            if (continueButton) continueButton.gameObject.SetActive(false);

            Color rarityColor = config ? config.RarityColor(character.rarity) : character.themeColor;
            float revealPoint = duration * .48f;
            for (float time = 0f; time < duration && !skipRequested; time += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(time / duration);
                float pulse = 1f + Mathf.Sin(progress * Mathf.PI * 8f) * .055f * (1f - progress);
                if (revealGlow)
                {
                    Color glow = rarityColor;
                    glow.a = Mathf.Lerp(.12f, .72f, Mathf.SmoothStep(0f, 1f, progress));
                    revealGlow.color = glow;
                    revealGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(.5f, 2.25f, progress);
                    revealGlow.rectTransform.Rotate(0f, 0f, Time.unscaledDeltaTime * 70f);
                }
                if (revealArtwork)
                {
                    bool silhouette = time < revealPoint;
                    revealArtwork.color = silhouette ? new Color(.03f, .02f, .08f, Mathf.Clamp01(progress * 4f)) : Color.white;
                    revealArtwork.rectTransform.localScale = Vector3.one * (silhouette ? Mathf.Lerp(.82f, 1f, progress / .48f) : pulse);
                }
                if (revealGroup) revealGroup.alpha = Mathf.Clamp01(progress * 4f);
                yield return null;
            }

            hasViewedReveal = true;
            if (revealArtwork) { revealArtwork.color = Color.white; revealArtwork.rectTransform.localScale = Vector3.one; }
            if (revealGroup) revealGroup.alpha = 1f;
            if (skipButton) skipButton.gameObject.SetActive(false);
            if (continueButton) continueButton.gameObject.SetActive(true);
            AudioClip clip = config ? config.RaritySound(character.rarity) : null;
            if (clip) (GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>()).PlayOneShot(clip);
        }

        public void SetDrawInteractable(bool value)
        {
            if (drawButton) drawButton.interactable = value;
            if (closeButton) closeButton.interactable = value;
        }

        void BindBanner(CharacterData data, int gems)
        {
            RefreshCurrency(gems);
            if (!data) return;
            Sprite artwork = BannerArt(data);
            if (featuredArtwork) { featuredArtwork.sprite = artwork; featuredArtwork.preserveAspect = true; featuredArtwork.color = Color.white; }
            if (bannerBackground)
            {
                bannerBackground.sprite = data.bannerBackground;
                bannerBackground.color = data.bannerBackground ? Color.white : new Color(data.themeColor.r * .32f, data.themeColor.g * .32f, data.themeColor.b * .32f, 1f);
            }
            if (featuredGlow) featuredGlow.color = new Color(data.themeColor.r, data.themeColor.g, data.themeColor.b, .3f);
            if (featuredLabel) featuredLabel.text = config && config.featuredCharacter == data ? "FEATURED CHARACTER" : "CURRENT SPOTLIGHT";
            if (characterName) characterName.text = data.displayName.ToUpperInvariant();
            if (rarityText) { rarityText.text = data.RarityLabel; rarityText.color = config ? config.RarityColor(data.rarity) : data.themeColor; }
            if (passiveName) passiveName.text = data.CutInAbilityName.ToUpperInvariant();
            if (passiveDescription) passiveDescription.text = data.passiveAbility;
        }

        void BindReveal(CharacterData data, bool duplicate, string compensationMessage)
        {
            if (revealArtwork) { revealArtwork.sprite = BannerArt(data); revealArtwork.preserveAspect = true; }
            Color color = config ? config.RarityColor(data.rarity) : data.themeColor;
            if (revealGlow) revealGlow.color = color;
            if (revealName) revealName.text = data.displayName.ToUpperInvariant();
            if (revealRarity) { revealRarity.text = data.RarityLabel; revealRarity.color = color; }
            if (revealPassive) revealPassive.text = data.CutInAbilityName.ToUpperInvariant() + "\n" + data.passiveAbility;
            if (duplicateMessage)
            {
                duplicateMessage.gameObject.SetActive(duplicate);
                duplicateMessage.text = duplicate ? "DUPLICATE — ACTIVE CHARACTER RETAINED" +
                    (string.IsNullOrWhiteSpace(compensationMessage) ? "" : "\n" + compensationMessage) : "";
            }
            if (statusText) statusText.text = duplicate ? "DUPLICATE SUMMON" : "NEW CHARACTER ACTIVATED";
        }

        void SetRevealVisible(bool value)
        {
            if (revealGroup)
            {
                revealGroup.gameObject.SetActive(value);
                revealGroup.alpha = value ? 1f : 0f;
                revealGroup.blocksRaycasts = value;
                revealGroup.interactable = value;
            }
        }

        void RequestSkip()
        {
            skipRequested = true;
            if (skipButton) skipButton.interactable = false;
        }

        IEnumerator StatusPulse()
        {
            if (!statusText) yield break;
            RectTransform rect = statusText.rectTransform;
            Vector3 start = Vector3.one;
            for (float time = 0f; time < .25f; time += Time.unscaledDeltaTime)
            {
                rect.localScale = start * (1f + Mathf.Sin(time / .25f * Mathf.PI) * .12f);
                yield return null;
            }
            rect.localScale = start;
        }

        static Sprite BannerArt(CharacterData data) =>
            data.bannerArtwork ? data.bannerArtwork :
            data.fullBodyCutIn ? data.fullBodyCutIn :
            data.frontSprite ? data.frontSprite : data.portrait;
    }
}
