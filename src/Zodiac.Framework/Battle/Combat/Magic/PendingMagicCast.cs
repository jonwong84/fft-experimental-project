namespace Zodiac.Framework;

/// <summary>Immutable proposed charge data, not a scheduled action or authority to adopt state.</summary>
public sealed class PendingMagicCast
{
    public MagicCastDefinition Definition { get; }
    public BattleUnitId CasterId { get; }
    public StoredSpellAim Aim { get; }
    public byte Counter { get; }
    internal PendingMagicCast(MagicCastDefinition definition, BattleUnitId casterId, StoredSpellAim aim, byte counter)
    { Definition = definition; CasterId = casterId; Aim = aim; Counter = counter; }
}
