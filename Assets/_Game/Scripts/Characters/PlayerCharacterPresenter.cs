using UnityEngine;

namespace Reflectable
{
    /// <summary>Spawns the equipped character prefab as the one gameplay player visual and hit target.</summary>
    public sealed class PlayerCharacterPresenter : MonoBehaviour
    {
        [SerializeField] CharacterDatabase database;
        [SerializeField] Transform characterSlot;
        GameObject current;
        Collider2D gameplayHitbox;

        public Transform CurrentVisual => current ? current.transform : null;
        public Collider2D GameplayHitbox => gameplayHitbox;
        public string CurrentCharacterId { get; private set; } = CharacterProgression.StarterId;
        public CharacterDatabase Database => database;
        public CharacterData CurrentData => database ? database.Find(CurrentCharacterId) : null;

        public void Configure(CharacterDatabase value, Transform slot)
        {
            database = value;
            characterSlot = slot;
        }

        public void Present(string id)
        {
            var data = database ? database.Find(id) : null;
            if (!data) data = database ? database.Default : null;
            if (!data || !data.prefab || !characterSlot)
            {
                Debug.LogError("[Character] ERROR: Cannot spawn the selected character. Database, prefab, or runtime visual slot is missing.");
                return;
            }

            if (current) Destroy(current);
            current = Instantiate(data.prefab, characterSlot);
            current.name = data.displayName + "(Clone)";
            current.transform.localPosition = Vector3.zero;
            current.transform.localRotation = Quaternion.identity;
            current.transform.localScale = Vector3.one;
            current.SetActive(true);
            CurrentCharacterId = data.characterId;
            gameplayHitbox = current.GetComponent<Collider2D>();
            if (!gameplayHitbox)
            {
                var box = current.AddComponent<BoxCollider2D>();
                box.size = new Vector2(.72f, .90f);
                box.offset = new Vector2(0f, -.03f);
                box.isTrigger = true;
                gameplayHitbox = box;
            }

            foreach (var renderer in current.GetComponentsInChildren<SpriteRenderer>(false))
            {
                renderer.enabled = true;
                var color = renderer.color; color.a = 1f; renderer.color = color;
                renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 50);
            }
            Debug.Log("[Character] Selected " + data.displayName);
            Debug.Log("[Character] Spawned " + data.displayName + " at " + current.transform.position);
            Debug.Log("[Player] Runtime player registered");
        }
    }
}
