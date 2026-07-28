using System.Collections.Generic;
using UnityEngine;

namespace Reflectable
{
    [CreateAssetMenu(menuName = "Reflectable/Character Database")]
    public sealed class CharacterDatabase : ScriptableObject
    {
        public List<CharacterData> characters = new List<CharacterData>();
        public CharacterData Find(string id) => string.IsNullOrWhiteSpace(id) ? null : characters.Find(x => x && x.characterId == id.ToLowerInvariant());
        public CharacterData Default => Find(CharacterProgression.StarterId) ?? (characters.Count > 0 ? characters[0] : null);

        public CharacterData RollGacha()
        {
            float total = 0f;
            foreach (var entry in characters) if (entry) total += Mathf.Max(.01f, entry.gachaWeight);
            float roll = Random.value * total;
            foreach (var entry in characters)
            {
                if (!entry) continue;
                roll -= Mathf.Max(.01f, entry.gachaWeight);
                if (roll <= 0f) return entry;
            }
            return Default;
        }
    }
}
