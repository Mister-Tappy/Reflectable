using UnityEngine;

namespace Reflectable
{
    [CreateAssetMenu(menuName = "Reflectable/Block Visual Catalog", fileName = "ReflectableBlockVisualCatalog")]
    public sealed class ReflectableBlockVisualCatalog : ScriptableObject
    {
        public ReflectableBlockVisualData normalBlock;
        public ReflectableBlockVisualData gemBlock;
        public ReflectableBlockVisualData bombBlock;
        static ReflectableBlockVisualCatalog cached;

        public static ReflectableBlockVisualData For(ReflectableBlockType type)
        {
            if (!cached) cached = Resources.Load<ReflectableBlockVisualCatalog>("ReflectableBlockVisualCatalog");
            if (!cached) return null;
            return type == ReflectableBlockType.Gem ? cached.gemBlock : type == ReflectableBlockType.Bomb ? cached.bombBlock : cached.normalBlock;
        }
    }
}
