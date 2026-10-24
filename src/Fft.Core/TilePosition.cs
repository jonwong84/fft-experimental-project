namespace Fft.Core;

/// <summary>
/// Immutable integer coordinates of a battle-map tile.
/// </summary>
/// <remarks>
/// <see cref="Layer"/> is the terrain-layer index at (<see cref="X"/>, <see cref="Y"/>).
/// It is not terrain height, which belongs to the tile data rather than this position.
/// </remarks>
public readonly record struct TilePosition(int X, int Y, int Layer);
