namespace Zodiac.Framework;

/// <summary>Source-order and map facts, separate from current formula input snapshots.</summary>
public sealed record SpellTargetOccupant(BattleUnitId UnitId, int SourceOrder, TilePosition Position,
    SpellTargetTeam InitialTeam, SpellTargetTeam CurrentTeam, bool Active = true, bool Crystal = false,
    bool Treasure = false, bool Jump = false, bool Mount = false);
