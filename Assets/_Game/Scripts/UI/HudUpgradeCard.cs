using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reflectable
{
    [RequireComponent(typeof(Button), typeof(ButtonJuice))]
    public sealed class HudUpgradeCard : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] Image outline;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text levelLabel;
        [SerializeField] TMP_Text effectLabel;
        [SerializeField] TMP_Text costLabel;
        Button button;
        Coroutine purchaseAnimation;
        int cachedLevel = -1;
        int cachedCost = -1;
        bool cachedInteractable;

        public Button Button => button ? button : button = GetComponent<Button>();

        public void Configure(Image cardIcon, Image cardOutline, TMP_Text title, TMP_Text level, TMP_Text effect, TMP_Text cost)
        {
            icon = cardIcon;
            outline = cardOutline;
            nameLabel = title;
            levelLabel = level;
            effectLabel = effect;
            costLabel = cost;
            button = GetComponent<Button>();
        }

        public void Refresh(string title, int level, int effect, bool percent, int cost, bool interactable)
        {
            if (nameLabel && nameLabel.text != title) nameLabel.text = title;
            if (cachedLevel != level)
            {
                if (levelLabel) levelLabel.SetText("Lv.{0}", level);
                if (effectLabel)
                {
                    if (percent) effectLabel.SetText("+{0}%", effect);
                    else effectLabel.SetText("+{0}", effect);
                }
            }
            if (cachedCost != cost && costLabel) costLabel.SetText("{0} SP", cost);
            if (Button && cachedInteractable != interactable) Button.interactable = interactable;
            cachedLevel = level;
            cachedCost = cost;
            cachedInteractable = interactable;
        }

        public void PurchasePulse()
        {
            if (!isActiveAndEnabled) return;
            if (purchaseAnimation != null) StopCoroutine(purchaseAnimation);
            purchaseAnimation = StartCoroutine(PurchaseRoutine());
        }

        IEnumerator PurchaseRoutine()
        {
            var start = transform.localScale;
            Color startOutline = outline ? outline.color : Color.white;
            for (float time = 0f; time < .09f; time += Time.unscaledDeltaTime)
            {
                float progress = time / .09f;
                transform.localScale = Vector3.Lerp(start, start * 1.10f, progress);
                if (outline) outline.color = Color.Lerp(startOutline, Color.white, progress);
                if (icon) icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 18f, progress));
                yield return null;
            }
            for (float time = 0f; time < .18f; time += Time.unscaledDeltaTime)
            {
                float progress = time / .18f;
                transform.localScale = Vector3.Lerp(start * 1.10f, start, progress);
                if (outline) outline.color = Color.Lerp(Color.white, startOutline, progress);
                if (icon) icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(18f, 0f, progress));
                yield return null;
            }
            transform.localScale = start;
            if (icon) icon.rectTransform.localRotation = Quaternion.identity;
            purchaseAnimation = null;
        }
    }
}
