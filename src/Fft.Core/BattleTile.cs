namespace Fft.Core;

/// <summary>
/// Immutable battle-map tile data.
/// </summary>
/// <param name="Height">Terrain height in whole units. Distinct from <see cref="TilePosition.Layer"/>.</param>
/// <remarks>
/// The managed model uses <see cref="int"/> without imposing the PS1 file field's byte range.
/// An importer for the original format should validate that format's range at its boundary.
/// </remarks>
public readonly record struct BattleTile(int Height);
