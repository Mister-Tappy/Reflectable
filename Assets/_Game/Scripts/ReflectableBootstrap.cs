using UnityEngine;

namespace Reflectable
{
    /// <summary>
    /// Migration guard for old scene files. Production scenes use ReflectableGameController.
    /// This component intentionally creates nothing and cannot start gameplay.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class ReflectableBootstrap : MonoBehaviour
    {
        void Awake()
        {
            enabled = false;
            Debug.LogError("Remove obsolete ReflectableBootstrap from this scene and use the serialized ReflectableGameController.", this);
        }
    }
}
