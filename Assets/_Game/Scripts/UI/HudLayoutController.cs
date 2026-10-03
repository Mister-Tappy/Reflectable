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

        void OnEnable()
        {
            if (Application.isPlaying) EnsureSafeAreaRoot();
            ApplySafeArea();
        }
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

        void EnsureSafeAreaRoot()
        {
            var canvas = GetComponent<Canvas>();
            var canvasRoot = canvas ? canvas.transform as RectTransform : null;
            if (!canvasRoot || (safeAreaRoot && safeAreaRoot != canvasRoot)) return;

            var root = canvasRoot.Find("SafeAreaRoot") as RectTransform;
            if (!root)
            {
                var rootObject = new GameObject("SafeAreaRoot", typeof(RectTransform));
                root = rootObject.GetComponent<RectTransform>();
                root.SetParent(canvasRoot, false);
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = root.offsetMax = Vector2.zero;
                root.pivot = new Vector2(.5f, .5f);

                var existingChildren = new Transform[canvasRoot.childCount - 1];
                int childIndex = 0;
                for (int i = 0; i < canvasRoot.childCount; i++)
                {
                    var child = canvasRoot.GetChild(i);
                    if (child != root) existingChildren[childIndex++] = child;
                }
                foreach (var child in existingChildren)
                    if (child) child.SetParent(root, false);
            }

            safeAreaRoot = root;
        }

        public bool FitsResolution(int width, int height)
        {
            if (width <= 0 || height <= 0) return false;
            float scale = Mathf.Min(width / referenceResolution.x, height / referenceResolution.y);
            return 118f * scale <= height * .13f + .5f;
        }
    }
}
