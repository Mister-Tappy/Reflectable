using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reflectable
{
    public static class CharacterProgression
    {
        public const string StarterId = "marina";
        const string EquippedKey = "Reflectable.EquippedCharacter";
        const string UnlockedKey = "Reflectable.UnlockedCharacters";
        const string LevelPrefix = "Reflectable.CharacterLevel.";

        static HashSet<string> ReadUnlocked()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { StarterId };
            foreach (var value in PlayerPrefs.GetString(UnlockedKey, StarterId).Split(';'))
                if (!string.IsNullOrWhiteSpace(value)) result.Add(value.Trim().ToLowerInvariant());
            return result;
        }

        public static IReadOnlyCollection<string> UnlockedCharacters => ReadUnlocked();
        public static bool IsUnlocked(string id) => !string.IsNullOrWhiteSpace(id) && ReadUnlocked().Contains(id);

        public static void Unlock(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            var unlocked = ReadUnlocked();
            unlocked.Add(id.ToLowerInvariant());
            PlayerPrefs.SetString(UnlockedKey, string.Join(";", unlocked));
            PlayerPrefs.Save();
        }

        public static string EquippedCharacter
        {
            get { var id = PlayerPrefs.GetString(EquippedKey, StarterId).ToLowerInvariant(); return IsUnlocked(id) ? id : StarterId; }
            set { var id = string.IsNullOrWhiteSpace(value) ? StarterId : value.ToLowerInvariant(); if (!IsUnlocked(id)) return; PlayerPrefs.SetString(EquippedKey, id); PlayerPrefs.Save(); }
        }

        public static int GetLevel(string id, int fallback = 1) => Mathf.Max(1, PlayerPrefs.GetInt(LevelPrefix + id.ToLowerInvariant(), fallback));
        public static void SetLevel(string id, int level) { PlayerPrefs.SetInt(LevelPrefix + id.ToLowerInvariant(), Mathf.Max(1, level)); PlayerPrefs.Save(); }
    }
}
