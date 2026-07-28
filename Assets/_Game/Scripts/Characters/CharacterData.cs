using UnityEngine;

namespace Reflectable
{
    public enum CharacterRarity { Common = 1, Rare = 2, Epic = 3, Legendary = 4, Mythic = 5 }
    public enum CharacterRole { Ricochet, Beam, Splash, Burn, Critical, Lucky, ExtraBall, Support, Chaos, Ultimate }

    [CreateAssetMenu(menuName = "Reflectable/Character Data", fileName = "Character_Data")]
    public sealed class CharacterData : ScriptableObject
    {
        [Header("Identity")]
        public string characterId = "marina";
        public string displayName = "Marina";
        public CharacterRarity rarity = CharacterRarity.Rare;
        public CharacterRole role = CharacterRole.Splash;
        public string title = "Tide Whisper";
        [TextArea(2, 5)] public string description;
        [TextArea(3, 8)] public string passiveAbility;

        [Header("Presentation")]
        public Sprite portrait;
        public Sprite icon;
        public GameObject prefab;
        public Sprite frontSprite;
        public Sprite sideSprite;
        public Sprite backSprite;
        public Color themeColor = Color.white;

        [Header("Combo Presentation (Optional)")]
        public Sprite fullBodyCutIn;
        public Vector2 cutInOffset;
        [Min(.1f)] public float cutInScale = 1f;
        public string cutInAbilityName;
        public Color comboAuraColor = Color.white;
        public GameObject comboEffectPrefab;
        public AudioClip[] comboVoiceClips = new AudioClip[0];

        [Header("Progression")]
        public bool unlockedByDefault;
        [Min(1)] public int startingLevel = 1;
        [Min(0.01f)] public float gachaWeight = 1f;

        public int Stars => (int)rarity;
        public string RarityLabel => new string('★', Stars) + " " + rarity.ToString().ToUpperInvariant();
        public Sprite CutInSprite => fullBodyCutIn ? fullBodyCutIn : frontSprite ? frontSprite : portrait;
        public string CutInAbilityName => string.IsNullOrWhiteSpace(cutInAbilityName) ? title : cutInAbilityName;
    }
}
