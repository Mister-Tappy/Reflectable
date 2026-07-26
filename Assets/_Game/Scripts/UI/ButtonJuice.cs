using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Reflectable
{
    [RequireComponent(typeof(Button))]
    public sealed class ButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] float hoverScale = 1.04f;
        [SerializeField] float pressedScale = .96f;
        [SerializeField] float duration = .12f;
        Vector3 baseScale;
        Coroutine tween;

        void Awake() => baseScale = transform.localScale;
        void OnEnable() => baseScale = transform.localScale;
        public void OnPointerEnter(PointerEventData _) => Animate(hoverScale);
        public void OnPointerExit(PointerEventData _) => Animate(1f);
        public void OnPointerDown(PointerEventData _) => Animate(pressedScale);
        public void OnPointerUp(PointerEventData _) => Animate(hoverScale);
        public void Punch() { if (isActiveAndEnabled) Animate(1.08f); }

        void Animate(float multiplier)
        {
            if (!isActiveAndEnabled) return;
            if (tween != null) StopCoroutine(tween);
            tween = StartCoroutine(Tween(baseScale * multiplier));
        }

        IEnumerator Tween(Vector3 target)
        {
            var start = transform.localScale;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                transform.localScale = Vector3.LerpUnclamped(start, target, 1f - Mathf.Pow(1f - t / duration, 3f));
                yield return null;
            }
            transform.localScale = target;
            tween = null;
        }
    }
}
