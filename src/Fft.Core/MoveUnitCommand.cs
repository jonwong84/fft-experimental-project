namespace Fft.Core;

/// <summary>Requests that one unit move to a destination.</summary>
public sealed record MoveUnitCommand(BattleUnitId UnitId, TilePosition Destination);
