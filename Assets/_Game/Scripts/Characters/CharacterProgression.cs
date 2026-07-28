using UnityEngine;

namespace Reflectable
{
    public static class CharacterProgression
    {
        public const string StarterId = "marina";
        const string ActiveCharacterKey = "Reflectable.ActiveCharacter";
        const string ActiveLevelKey = "Reflectable.ActiveCharacterLevel";
        const string LegacyEquippedKey = "Reflectable.EquippedCharacter";
        const string LegacyUnlockedKey = "Reflectable.UnlockedCharacters";

        public static void MigrateLegacyData()
        {
            if (!PlayerPrefs.HasKey(ActiveCharacterKey))
                PlayerPrefs.SetString(ActiveCharacterKey, PlayerPrefs.GetString(LegacyEquippedKey, StarterId).ToLowerInvariant());
            PlayerPrefs.DeleteKey(LegacyEquippedKey);
            PlayerPrefs.DeleteKey(LegacyUnlockedKey);
            PlayerPrefs.Save();
        }

        public static string ActiveCharacterId
        {
            get
            {
                MigrateLegacyData();
                string id = PlayerPrefs.GetString(ActiveCharacterKey, StarterId);
                return string.IsNullOrWhiteSpace(id) ? StarterId : id.ToLowerInvariant();
            }
        }

        public static int ActiveCharacterLevel
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt(ActiveLevelKey, 1));
        }

        public static void SetActiveCharacter(string id, int level)
        {
            string resolved = string.IsNullOrWhiteSpace(id) ? StarterId : id.ToLowerInvariant();
            PlayerPrefs.SetString(ActiveCharacterKey, resolved);
            PlayerPrefs.SetInt(ActiveLevelKey, Mathf.Max(1, level));
            PlayerPrefs.DeleteKey(LegacyEquippedKey);
            PlayerPrefs.DeleteKey(LegacyUnlockedKey);
            PlayerPrefs.Save();
        }
    }
}
