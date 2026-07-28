namespace Reflectable
{
    /// <summary>Stateless draw helper. It never stores ownership or a roster.</summary>
    public sealed class CharacterGacha
    {
        public CharacterData Summon(ref int gems, CharacterDatabase database, CharacterGachaConfig config, out bool duplicate)
        {
            duplicate = false;
            if (!config || gems < config.drawCost) return null;
            CharacterData drawn = config.Roll(database);
            if (!drawn) return null;
            gems -= config.drawCost;
            duplicate = drawn.characterId == CharacterProgression.ActiveCharacterId;
            if (!duplicate) CharacterProgression.SetActiveCharacter(drawn.characterId, drawn.startingLevel);
            return drawn;
        }
    }
}
