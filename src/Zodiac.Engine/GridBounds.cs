namespace Zodiac.Engine;

/// <summary>Immutable positive rectangular dimensions, without tile data or allocation.</summary>
public sealed record GridBounds
{
    public GridBounds(int width, int depth, int layerCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(layerCount);
        Width = width;
        Depth = depth;
        LayerCount = layerCount;
    }

    public int Width { get; }
    public int Depth { get; }
    public int LayerCount { get; }

    public bool Contains(GridPosition position) =>
        position.X >= 0 && position.X < Width &&
        position.Y >= 0 && position.Y < Depth &&
        position.Layer >= 0 && position.Layer < LayerCount;
}
