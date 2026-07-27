using UnityEngine;

namespace Reflectable
{
    [CreateAssetMenu(menuName = "Reflectable/Stage Catalog", fileName = "ReflectableStageCatalog")]
    public sealed class ReflectableStageCatalog : ScriptableObject
    {
        [SerializeField] ReflectableStageData[] stages = new ReflectableStageData[0];
        static ReflectableStageCatalog cached;

        public static ReflectableStageCatalog Current
        {
            get
            {
                if (!cached) cached = Resources.Load<ReflectableStageCatalog>("ReflectableStageCatalog");
                return cached;
            }
        }

        public static ReflectableStageData Get(int stageNumber)
        {
            var catalog = Current;
            if (!catalog || catalog.stages == null) return null;
            foreach (var stage in catalog.stages)
                if (stage && stage.stageNumber == stageNumber) return stage;
            return null;
        }

        public static int StageCount
        {
            get
            {
                var catalog = Current;
                return catalog != null && catalog.stages != null ? Mathf.Max(1, catalog.stages.Length) : 10;
            }
        }
    }
}
