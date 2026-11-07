namespace Zodiac.Framework;

/// <summary>Explicit current source position; activity and target eligibility are separate facts.</summary>
public sealed record SpellAimAnchor(BattleUnitId UnitId, TilePosition Position);
