using System;
using UnityEngine;
using UnityEngine.UI;

namespace Reflectable
{
    public sealed class CharacterCollectionRow : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] Image portrait;
        [SerializeField] Text label;
        [SerializeField] Button button;

        CharacterData data;
        Action<string> selected;

        public bool HasPersistentReferences => background && portrait && label && button;

        public void Bind(CharacterData character, Action<string> onSelected)
        {
            data = character;
            selected = onSelected;
            bool unlocked = data.unlockedByDefault || CharacterProgression.IsUnlocked(data.characterId);
            background.color = unlocked ? new Color(.22f, .18f, .32f, 1f) : new Color(.12f, .11f, .16f, 1f);
            portrait.sprite = data.portrait;
            portrait.color = unlocked ? data.themeColor : Color.gray;
            button.interactable = unlocked;
            button.onClick.RemoveListener(Select);
            button.onClick.AddListener(Select);
            string state = CharacterProgression.EquippedCharacter == data.characterId ? "EQUIPPED" : unlocked ? "UNLOCKED" : "LOCKED";
            label.color = unlocked ? RarityColor(data.rarity) : Color.gray;
            label.text = data.displayName.ToUpperInvariant() + "  " + data.RarityLabel + "\n" + state;
        }

        void OnDestroy()
        {
            if (button) button.onClick.RemoveListener(Select);
        }

        void Select()
        {
            if (data) selected?.Invoke(data.characterId);
        }

        static Color RarityColor(CharacterRarity rarity) =>
            rarity == CharacterRarity.Mythic ? new Color(1f, .78f, .22f) :
            rarity == CharacterRarity.Legendary ? new Color(.72f, .40f, 1f) :
            rarity == CharacterRarity.Epic ? new Color(.32f, .58f, 1f) :
            rarity == CharacterRarity.Rare ? new Color(.34f, .84f, .42f) :
            Color.gray;
    }
}
