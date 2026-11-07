namespace Zodiac.Framework.Ai;

/// <summary>Diagnostic coordinates and, for a type6 aim, its current unit anchor. Neither is retained authority.</summary>
public sealed record OrdinarySpellAiAim(TilePosition Position, BattleUnitId? UnitId);
