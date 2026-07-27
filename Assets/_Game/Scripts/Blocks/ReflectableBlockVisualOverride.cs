using UnityEngine;
using UnityEngine.UI;

namespace Reflectable
{
    /// <summary>Applies asset-authored visual data after a gameplay block has initialized.</summary>
    public sealed class ReflectableBlockVisualOverride : MonoBehaviour
    {
        ReflectableBlockVisualData data;
        ReflectableStageBlockPalette palette;
        SpriteRenderer spriteRenderer;
        Text hpLabel;

        public void Configure(ReflectableBlockVisualData visualData, ReflectableStageBlockPalette stagePalette)
        {
            data = visualData;
            palette = stagePalette;
            var visualRoot = transform.Find("Visual") ?? transform;
            spriteRenderer = visualRoot.GetComponentInChildren<SpriteRenderer>();
            hpLabel = GetComponentInChildren<Text>();

            if (data != null)
            {
                if (data.sprite != null && spriteRenderer != null) spriteRenderer.sprite = data.sprite;
                if (data.material != null && spriteRenderer != null) spriteRenderer.sharedMaterial = data.material;
                visualRoot.localScale = Vector3.Scale(visualRoot.localScale, data.visualScale);
                var collider = GetComponent<Collider2D>();
                if (collider is BoxCollider2D box) box.size = data.colliderSize;
            }
            ApplyTheme();
        }

        void LateUpdate() => ApplyTheme();

        void ApplyTheme()
        {
            if (spriteRenderer != null)
            {
                var block = GetComponent<ReflectableBlockView>();
                var type = block != null ? block.Type : data != null ? data.blockType : ReflectableBlockType.Normal;
                spriteRenderer.color = palette != null ? palette.TintFor(type, data != null ? data.baseTint : Color.white) : data != null ? data.baseTint : spriteRenderer.color;
            }
            if (hpLabel != null && data != null)
            {
                hpLabel.color = palette != null ? palette.hpTextColor : data.hpTextColor;
                hpLabel.fontSize = data.hpTextSize;
            }
        }
    }
}
