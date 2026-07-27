using UnityEngine;

namespace Reflectable
{
    [CreateAssetMenu(menuName = "Reflectable/Stage Block Palette", fileName = "StageBlockPalette")]
    public sealed class ReflectableStageBlockPalette : ScriptableObject
    {
        public bool overrideBlockTints;
        public Color normalTint = Color.white;
        public Color gemTint = Color.white;
        public Color bombTint = Color.white;
        public Color hpTextColor = Color.white;

        public Color TintFor(ReflectableBlockType type, Color fallback)
        {
            if (!overrideBlockTints) return fallback;
            return type == ReflectableBlockType.Gem ? gemTint : type == ReflectableBlockType.Bomb ? bombTint : normalTint;
        }
    }
}
