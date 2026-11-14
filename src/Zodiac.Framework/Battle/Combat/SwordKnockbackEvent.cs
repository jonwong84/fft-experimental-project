namespace Zodiac.Framework;

/// <summary>Settled forced displacement; never consumes the target's voluntary movement choice.</summary>
public sealed record SwordKnockbackEvent(BattleUnitId UnitId, TilePosition Before, TilePosition After) : AttackResultEvent;
