namespace Zodiac.Framework;

/// <summary>Derived choice mode tied to the same immutable caster, definition and blue panels.</summary>
public sealed class SpellTargetChoice
{
    public MagicCastDefinition Definition { get; }
    public BattleUnitId CasterId { get; }
    public TilePosition CasterPosition { get; }
    public SpellTargetChoiceMode Mode { get; }
    public SpellTargetPanels? Range { get; }
    internal SpellTargetChoice(MagicCastDefinition definition, SpellTargetOccupant caster, SpellTargetChoiceMode mode, SpellTargetPanels? range)
    { Definition = definition; CasterId = caster.UnitId; CasterPosition = caster.Position; Mode = mode; Range = range; }
}
