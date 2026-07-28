using UnityEngine;

namespace Reflectable
{
    [ExecuteAlways]
    public sealed class HudLayoutController : MonoBehaviour
    {
        [SerializeField] RectTransform safeAreaRoot;
        [SerializeField] Vector2 referenceResolution = new Vector2(1920f, 1080f);
        Rect lastSafeArea;
        Vector2Int lastScreen;

        public void Configure(RectTransform root) => safeAreaRoot = root;

        void OnEnable() => ApplySafeArea();
        void Update()
        {
            var resolution = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea != lastSafeArea || resolution != lastScreen) ApplySafeArea();
        }

        public void ApplySafeArea()
        {
            if (!safeAreaRoot || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = Screen.safeArea;
            safeAreaRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            safeAreaRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            safeAreaRoot.offsetMin = safeAreaRoot.offsetMax = Vector2.zero;
            lastSafeArea = safe;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
        }

        public bool FitsResolution(int width, int height)
        {
            if (width <= 0 || height <= 0) return false;
            float scale = Mathf.Min(width / referenceResolution.x, height / referenceResolution.y);
            return 118f * scale <= height * .13f + .5f;
        }
    }
}
