using UnityEngine;

namespace Reflectable
{
    [CreateAssetMenu(menuName = "Reflectable/Block Visual Data", fileName = "BlockVisualData")]
    public sealed class ReflectableBlockVisualData : ScriptableObject
    {
        public ReflectableBlockType blockType;
        public string displayName;
        public GameObject prefab;
        public Sprite sprite;
        public Material material;
        public Color baseTint = Color.white;
        public Color hpTextColor = Color.white;
        [Min(1)] public int hpTextSize = 24;
        public Vector3 visualScale = Vector3.one;
        public Vector2 colliderSize = Vector2.one;
        [Min(.01f)] public float durabilityMultiplier = 1f;
        [Min(0f)] public float spawnWeight = 1f;
        public GameObject hitEffect;
        public GameObject destroyEffect;
    }
}
