using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Prevalidated post-readiness data. Original table conversion belongs to offline preparation.</summary>
public sealed class DarkScreenDefinition
{
    public const int Rows = 12, Columns = 24, TileCount = Rows * Columns;
    public AssetId Id { get; }
    public DarkScreenParameters Parameters { get; }
    public ImmutableArray<DarkScreenTileDefinition> Tiles { get; }

    public DarkScreenDefinition(AssetId id, DarkScreenParameters parameters, IEnumerable<DarkScreenTileDefinition> tiles)
    {
        if (!SceneTaskState.ValidId(id)) throw new ArgumentException("A bounded definition ID is required.", nameof(id));
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(tiles);
        if (parameters != new DarkScreenParameters())
            throw new ArgumentException("Only reserved-0/origin/divisor-12/angle-64/radius-4/image-0 is supported.", nameof(parameters));
        var data = tiles.Take(TileCount + 1).ToImmutableArray();
        if (data.Length != TileCount) throw new ArgumentException("The compatibility layout requires 288 cells.", nameof(tiles));
        for (int i = 0; i < data.Length; i++)
        {
            int row = i / Columns, column = i % Columns;
            if (data[i].X != column * 12 - 12 || data[i].Y != row * 24 - (column % 2 == 0 ? 12 : 24) ||
                data[i].InitialDelay is < 0 or > 41)
                throw new ArgumentException("Expected the staggered row-major layout and prevalidated delays in 0..41.", nameof(tiles));
        }
        (Id, Parameters, Tiles) = (id, parameters, data);
    }
}
