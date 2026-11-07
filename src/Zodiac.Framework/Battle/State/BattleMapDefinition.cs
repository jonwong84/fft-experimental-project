namespace Zodiac.Framework;

/// <summary>
/// Immutable battle-map dimensions and tile records.
/// </summary>
/// <remarks>
/// Tiles are stored in a managed layer-major array: layer varies slowest, then Y (depth),
/// then X (width) fastest. Index is <c>((layer * Depth) + y) * Width + x</c>.
/// That layout is a new representation; it is not the original disc terrain packing,
/// which used two fixed 256-tile layers.
/// </remarks>
public sealed class BattleMapDefinition
{
    private readonly BattleTile[] _tiles;

    public BattleMapDefinition(int width, int depth, int layerCount, IReadOnlyList<BattleTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(layerCount);

        int expectedCount = checked(width * depth * layerCount);
        if (tiles.Count != expectedCount)
        {
            throw new ArgumentException(
                $"Tile count must be Width * Depth * LayerCount ({expectedCount}), but was {tiles.Count}.",
                nameof(tiles));
        }

        Width = width;
        Depth = depth;
        LayerCount = layerCount;
        _tiles = [.. tiles];
    }

    public int Width { get; }

    public int Depth { get; }

    public int LayerCount { get; }

    public bool TryGetTile(TilePosition position, out BattleTile tile)
    {
        if (position.X < 0 || position.X >= Width
            || position.Y < 0 || position.Y >= Depth
            || position.Layer < 0 || position.Layer >= LayerCount)
        {
            tile = default;
            return false;
        }

        tile = _tiles[((position.Layer * Depth) + position.Y) * Width + position.X];
        return true;
    }
}
