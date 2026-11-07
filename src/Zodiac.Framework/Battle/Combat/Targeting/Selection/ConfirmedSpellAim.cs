namespace Zodiac.Framework;

/// <summary>Opaque confirmed selection bound to its original immutable definition and caster.</summary>
public sealed class ConfirmedSpellAim
{
    public MagicCastDefinition Definition { get; }
    public BattleUnitId CasterId { get; }
    public StoredSpellAim Aim { get; }
    internal ConfirmedSpellAim(SpellTargetChoice choice, StoredSpellAim aim)
    { Definition = choice.Definition; CasterId = choice.CasterId; Aim = aim; }
}
