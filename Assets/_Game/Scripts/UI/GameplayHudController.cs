using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reflectable
{
    public sealed class GameplayHudController : MonoBehaviour
    {
        [Header("Top status")]
        [SerializeField] Image hpFill;
        [SerializeField] TMP_Text hpText;
        [SerializeField] TMP_Text stageText;
        [SerializeField] TMP_Text progressText;
        [SerializeField] TMP_Text turnText;
        [SerializeField] Image stageProgressFill;
        [SerializeField] Image stageProgressSparkle;
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text gemsText;
        [Header("Character")]
        [SerializeField] Image portrait;
        [SerializeField] Image portraitGlow;
        [SerializeField] TMP_Text characterName;
        [SerializeField] TMP_Text characterLevel;
        [SerializeField] TMP_Text skillPointsText;
        [Header("Actions")]
        [SerializeField] HudUpgradeCard powerCard;
        [SerializeField] HudUpgradeCard ricochetCard;
        [SerializeField] HudUpgradeCard extraBallCard;
        [SerializeField] Button collectionButton;
        [SerializeField] Button skipButton;
        [SerializeField] Button pauseButton;
        [SerializeField] GameObject collectionBadge;
        [SerializeField] Image highComboEdgeGlow;
        float targetProgress;
        float displayedProgress;
        int lastDestroyed = -1;
        int cachedStage = -1;
        string cachedStageName;
        string cachedCharacterId;
        Coroutine progressPulse;
        Coroutine portraitPulse;

        public Button PowerButton => powerCard ? powerCard.Button : null;
        public Button RicochetButton => ricochetCard ? ricochetCard.Button : null;
        public Button ExtraBallButton => extraBallCard ? extraBallCard.Button : null;
        public Button CollectionButton => collectionButton;
        public Button SkipButton => skipButton;
        public Button PauseButton => pauseButton;

        void Update()
        {
            displayedProgress = Mathf.Lerp(displayedProgress, targetProgress, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 9f));
            if (stageProgressFill) stageProgressFill.fillAmount = displayedProgress;
        }

        public void Refresh(
            int hp, int maxHp, int stage, string stageNameValue, int destroyed, int target, int turn,
            int score, int gems, int skillPoints, CharacterData character, int level,
            int power, int powerCost, int ricochet, int ricochetCost, int extraBall, int extraBallCost,
            bool canUpgrade)
        {
            if (hpText) hpText.SetText("HP {0} / {1}", hp, maxHp);
            if (hpFill) hpFill.fillAmount = maxHp > 0 ? Mathf.Clamp01(hp / (float)maxHp) : 0f;
            if (stageText && (cachedStage != stage || cachedStageName != stageNameValue))
            {
                stageText.text = "STAGE " + stage + "  ·  " + stageNameValue;
                cachedStage = stage;
                cachedStageName = stageNameValue;
            }
            if (progressText) progressText.SetText("{0} / {1} BLOCKS", destroyed, target);
            if (turnText) turnText.SetText("TURN {0}", turn);
            if (scoreText) scoreText.SetText("SCORE  {0}", score);
            if (gemsText) gemsText.SetText("GEM  {0}", gems);
            if (skillPointsText) skillPointsText.SetText("SP  {0}", skillPoints);
            targetProgress = target > 0 ? Mathf.Clamp01(destroyed / (float)target) : 0f;
            if (lastDestroyed >= 0 && destroyed > lastDestroyed) PulseProgress();
            lastDestroyed = destroyed;

            if (character && cachedCharacterId != character.characterId)
            {
                cachedCharacterId = character.characterId;
                if (portrait)
                {
                    portrait.sprite = character.portrait ? character.portrait : character.frontSprite;
                    portrait.color = UsesBuiltinPlaceholder(portrait.sprite) ? character.themeColor : Color.white;
                }
                if (portraitGlow) portraitGlow.color = new Color(character.themeColor.r, character.themeColor.g, character.themeColor.b, .32f);
                if (characterName) characterName.text = character.displayName.ToUpperInvariant();
            }
            if (characterLevel) characterLevel.SetText("Lv.{0}", level);
            powerCard?.Refresh("POWER", power, power * 15, true, powerCost, canUpgrade && skillPoints >= powerCost);
            ricochetCard?.Refresh("RICOCHET", ricochet, ricochet * 4, true, ricochetCost, canUpgrade && skillPoints >= ricochetCost);
            extraBallCard?.Refresh("EXTRA BALL", extraBall, extraBall, false, extraBallCost, canUpgrade && skillPoints >= extraBallCost);
        }

        static bool UsesBuiltinPlaceholder(Sprite sprite) =>
            sprite && (sprite.name == "UISprite" || sprite.name == "Background");

        public void SetComboTier(int tier, Color color)
        {
            if (!highComboEdgeGlow) return;
            var tint = color;
            tint.a = tier >= 3 ? Mathf.Lerp(.08f, .42f, (tier - 2) / 5f) : 0f;
            highComboEdgeGlow.color = tint;
        }

        public void UpgradePurchased(string id)
        {
            if (id == "Power") powerCard?.PurchasePulse();
            else if (id == "Ricochet") ricochetCard?.PurchasePulse();
            else if (id == "Extra Ball") extraBallCard?.PurchasePulse();
        }

        public void NotifyCharacterUnlocked()
        {
            if (collectionBadge) collectionBadge.SetActive(true);
            var juice = collectionButton ? collectionButton.GetComponent<ButtonJuice>() : null;
            if (juice) juice.Punch();
        }

        public void ClearCharacterBadge()
        {
            if (collectionBadge) collectionBadge.SetActive(false);
        }

        public void PulsePortrait()
        {
            if (!portrait || !isActiveAndEnabled) return;
            if (portraitPulse != null) StopCoroutine(portraitPulse);
            portraitPulse = StartCoroutine(PortraitRoutine());
        }

        void PulseProgress()
        {
            if (!isActiveAndEnabled) return;
            if (progressPulse != null) StopCoroutine(progressPulse);
            progressPulse = StartCoroutine(ProgressRoutine());
        }

        IEnumerator ProgressRoutine()
        {
            if (stageProgressSparkle) stageProgressSparkle.gameObject.SetActive(true);
            var rect = progressText ? progressText.rectTransform : null;
            for (float time = 0f; time < .18f; time += Time.unscaledDeltaTime)
            {
                float pulse = 1f + Mathf.Sin(time / .18f * Mathf.PI) * .12f;
                if (rect) rect.localScale = Vector3.one * pulse;
                if (stageProgressSparkle) stageProgressSparkle.rectTransform.anchorMin = stageProgressSparkle.rectTransform.anchorMax = new Vector2(displayedProgress, .5f);
                yield return null;
            }
            if (rect) rect.localScale = Vector3.one;
            if (stageProgressSparkle) stageProgressSparkle.gameObject.SetActive(false);
            progressPulse = null;
        }

        IEnumerator PortraitRoutine()
        {
            var rect = portrait.rectTransform;
            for (float time = 0f; time < .28f; time += Time.unscaledDeltaTime)
            {
                float pulse = 1f + Mathf.Sin(time / .28f * Mathf.PI) * .16f;
                rect.localScale = Vector3.one * pulse;
                yield return null;
            }
            rect.localScale = Vector3.one;
            portraitPulse = null;
        }
    }
}
