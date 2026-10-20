using Fft.Core;
using Xunit;

namespace Fft.Core.Tests;

public sealed class BattleMapDefinitionTests
{
    [Fact]
    public void TryGetTile_returns_distinct_tiles_on_a_non_square_multi_layer_map()
    {
        var tiles = new BattleTile[3 * 2 * 2];
        for (int i = 0; i < tiles.Length; i++)
        {
            tiles[i] = new BattleTile(i + 1);
        }

        var map = new BattleMapDefinition(width: 3, depth: 2, layerCount: 2, tiles);

        Assert.True(map.TryGetTile(new TilePosition(0, 0, 0), out BattleTile origin));
        Assert.Equal(1, origin.Height);

        Assert.True(map.TryGetTile(new TilePosition(2, 0, 0), out BattleTile endOfFirstRow));
        Assert.Equal(3, endOfFirstRow.Height);

        Assert.True(map.TryGetTile(new TilePosition(0, 1, 0), out BattleTile secondRow));
        Assert.Equal(4, secondRow.Height);

        Assert.True(map.TryGetTile(new TilePosition(1, 1, 1), out BattleTile upperLayer));
        Assert.Equal(11, upperLayer.Height);

        Assert.True(map.TryGetTile(new TilePosition(1, 0, 0), out BattleTile lower));
        Assert.True(map.TryGetTile(new TilePosition(1, 0, 1), out BattleTile upper));
        Assert.Equal(2, lower.Height);
        Assert.Equal(8, upper.Height);
        Assert.NotEqual(lower, upper);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(3, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 2, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(0, 0, 2)]
    public void TryGetTile_rejects_negative_and_upper_bound_coordinates(int x, int y, int layer)
    {
        var map = CreateMap(width: 3, depth: 2, layerCount: 2);

        Assert.False(map.TryGetTile(new TilePosition(x, y, layer), out BattleTile tile));
        Assert.Equal(default, tile);
    }

    [Theory]
    [InlineData(0, 2, 2)]
    [InlineData(-1, 2, 2)]
    [InlineData(2, 0, 2)]
    [InlineData(2, -4, 2)]
    [InlineData(2, 2, 0)]
    [InlineData(2, 2, -1)]
    public void Constructor_rejects_non_positive_dimensions(int width, int depth, int layerCount)
    {
        var tiles = Array.Empty<BattleTile>();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BattleMapDefinition(width, depth, layerCount, tiles));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(13)]
    public void Constructor_rejects_mismatched_tile_counts(int tileCount)
    {
        var tiles = new BattleTile[tileCount];

        var exception = Assert.Throws<ArgumentException>(
            () => new BattleMapDefinition(width: 3, depth: 2, layerCount: 2, tiles));

        Assert.Equal("tiles", exception.ParamName);
    }

    [Fact]
    public void Constructor_rejects_dimensions_whose_tile_count_overflows()
    {
        Assert.Throws<OverflowException>(
            () => new BattleMapDefinition(
                width: int.MaxValue,
                depth: 2,
                layerCount: 1,
                Array.Empty<BattleTile>()));
    }

    [Fact]
    public void Constructor_copies_tile_data()
    {
        var tiles = new[]
        {
            new BattleTile(4),
            new BattleTile(8),
        };

        var map = new BattleMapDefinition(width: 1, depth: 1, layerCount: 2, tiles);
        tiles[0] = new BattleTile(99);
        tiles[1] = new BattleTile(100);

        Assert.True(map.TryGetTile(new TilePosition(0, 0, 0), out BattleTile lower));
        Assert.True(map.TryGetTile(new TilePosition(0, 0, 1), out BattleTile upper));
        Assert.Equal(4, lower.Height);
        Assert.Equal(8, upper.Height);
    }

    private static BattleMapDefinition CreateMap(int width, int depth, int layerCount)
    {
        var tiles = new BattleTile[width * depth * layerCount];
        for (int i = 0; i < tiles.Length; i++)
        {
            tiles[i] = new BattleTile(i + 1);
        }

        return new BattleMapDefinition(width, depth, layerCount, tiles);
    }
}
