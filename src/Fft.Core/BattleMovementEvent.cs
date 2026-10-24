namespace Fft.Core;

/// <summary>Describes an accepted movement transition for presentation and diagnostics.</summary>
public sealed record BattleMovementEvent(BattleUnitId UnitId, TilePosition From, TilePosition To);
