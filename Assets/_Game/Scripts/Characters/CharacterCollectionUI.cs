using System;
using UnityEngine;
using UnityEngine.UI;

namespace Reflectable
{
    public sealed class CharacterCollectionUI : MonoBehaviour
    {
        [SerializeField] RectTransform content;
        [SerializeField] CharacterCollectionRow rowPrefab;

        CharacterDatabase database;
        Action<string> equip;

        public void Configure(CharacterDatabase source, Action<string> equipAction)
        {
            database = source;
            equip = equipAction;
            Refresh();
        }

        public bool HasPersistentReferences => content && rowPrefab;

        public void Refresh()
        {
            if (!content || !rowPrefab || !database)
            {
                Debug.LogError(
                    "CharacterCollectionUI requires a serialized content transform, row prefab, and CharacterDatabase.",
                    this);
                return;
            }

            for (int i = content.childCount - 1; i >= 0; i--)
                if (content.GetChild(i).GetComponent<CharacterCollectionRow>())
                    Destroy(content.GetChild(i).gameObject);

            int index = 0;
            foreach (var data in database.characters)
            {
                if (!data) continue;
                var row = Instantiate(rowPrefab, content);
                row.name = data.displayName;
                var rect = (RectTransform)row.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(index % 2 == 0 ? -165f : 165f, 125f - (index / 2) * 54f);
                row.Bind(data, OnEquip);
                index++;
            }
        }

        void OnEquip(string characterId)
        {
            equip?.Invoke(characterId);
            Refresh();
        }
    }
}
