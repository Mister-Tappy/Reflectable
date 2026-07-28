using UnityEngine;

namespace Reflectable
{
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Managers")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private ScoreManager scoreManager;

        public TurnManager Turns => turnManager;
        public ScoreManager Score => scoreManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
