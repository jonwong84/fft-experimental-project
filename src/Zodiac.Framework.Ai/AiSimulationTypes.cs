namespace Zodiac.Framework.Ai;

/// <summary>Original selected-state death counter, absent from the ordinary live clock model.</summary>
public sealed record AiUnitBaseline(BattleUnitId UnitId, byte DeathCounter);

/// <summary>Hypothetical data only; no active turn, command receipt or executable battle snapshot.</summary>
public sealed record AiProjectedUnit(BattleUnitId UnitId, TilePosition Position, BattleFacing Facing,
    int Hp, int MaxHp, AttackUnitStatus Status, byte DeathCounter, int Speed, int TieKey, int Ct,
    bool MovementTaken, bool ActionTaken);
public sealed record AiUpkeepPick(BattleUnitId UnitId, int CtBefore, int CtAfter, bool Dead, uint RawRoll);
