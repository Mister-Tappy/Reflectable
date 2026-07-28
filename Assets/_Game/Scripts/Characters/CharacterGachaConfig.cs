using UnityEngine;

namespace Reflectable
{
    public enum DuplicateCompensationType { None, SkillPoints, Gems }

    [CreateAssetMenu(menuName = "Reflectable/Character Gacha Config", fileName = "CharacterGachaConfig")]
    public sealed class CharacterGachaConfig : ScriptableObject
    {
        [Header("Draw")]
        [Min(0)] public int drawCost = 2;
        public CharacterData featuredCharacter;
        [Range(0f, 1f)] public float featuredCharacterRate = .18f;
        public bool useCharacterWeights = true;

        [Header("Duplicate Result")]
        public DuplicateCompensationType duplicateCompensation = DuplicateCompensationType.SkillPoints;
        [Min(0)] public int duplicateCompensationAmount = 1;

        [Header("Summon Presentation")]
        [Range(1.5f, 3f)] public float summonAnimationDuration = 2.1f;
        public bool allowSkipAfterFirstViewing = true;
        public Color rareColor = new Color(.15f, .82f, 1f);
        public Color epicColor = new Color(.65f, .28f, 1f);
        public Color legendaryColor = new Color(1f, .68f, .12f);
        public Color mythicColor = new Color(1f, .22f, .58f);
        public AudioClip rareReveal;
        public AudioClip epicReveal;
        public AudioClip legendaryReveal;
        public AudioClip mythicReveal;
        public GameObject summonParticlePrefab;

        public CharacterData Roll(CharacterDatabase database)
        {
            if (!database) return null;
            if (featuredCharacter && Random.value < featuredCharacterRate) return featuredCharacter;
            return useCharacterWeights ? database.RollGacha() : database.RandomCharacter();
        }

        public Color RarityColor(CharacterRarity rarity)
        {
            if (rarity >= CharacterRarity.Mythic) return mythicColor;
            if (rarity >= CharacterRarity.Legendary) return legendaryColor;
            if (rarity >= CharacterRarity.Epic) return epicColor;
            return rareColor;
        }

        public AudioClip RaritySound(CharacterRarity rarity)
        {
            if (rarity >= CharacterRarity.Mythic) return mythicReveal;
            if (rarity >= CharacterRarity.Legendary) return legendaryReveal;
            if (rarity >= CharacterRarity.Epic) return epicReveal;
            return rareReveal;
        }
    }
}
