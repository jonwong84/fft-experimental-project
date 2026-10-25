using Zodiac.Framework;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class FlatGridMovementRangeTests
{
    [Fact]
    public void Calculate_returns_all_tiles_within_move_distance_from_center()
    {
        var map = CreateFlatMap(width: 7, depth: 7, layerCount: 2);
        var origin = new TilePosition(3, 3, 1);

        IReadOnlySet<TilePosition> destinations = FlatGridMovementRange.Calculate(map, origin, move: 3);

        var expected = new HashSet<TilePosition>();
        for (int y = 0; y < map.Depth; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                int distance = Math.Abs(x - origin.X) + Math.Abs(y - origin.Y);
                if (distance is > 0 and <= 3)
                {
                    expected.Add(new TilePosition(x, y, origin.Layer));
                }
            }
        }

        Assert.Equal(24, destinations.Count);
        Assert.True(expected.SetEquals(destinations));
        Assert.DoesNotContain(origin, destinations);
        Assert.All(destinations, position => Assert.Equal(origin.Layer, position.Layer));
    }

    [Fact]
    public void Calculate_returns_only_adjacent_in_bounds_tiles_from_edge()
    {
        var map = CreateFlatMap(width: 3, depth: 2, layerCount: 2);
        var origin = new TilePosition(0, 0, 0);

        IReadOnlySet<TilePosition> destinations = FlatGridMovementRange.Calculate(map, origin, move: 1);

        Assert.True(new HashSet<TilePosition>
        {
            new(1, 0, 0),
            new(0, 1, 0),
        }.SetEquals(destinations));
    }

    [Fact]
    public void Calculate_rejects_negative_move()
    {
        var map = CreateFlatMap(width: 3, depth: 2, layerCount: 1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => FlatGridMovementRange.Calculate(map, new TilePosition(1, 0, 0), move: -1));
    }

    [Fact]
    public void Calculate_rejects_origin_outside_map()
    {
        var map = CreateFlatMap(width: 3, depth: 2, layerCount: 1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => FlatGridMovementRange.Calculate(map, new TilePosition(3, 0, 0), move: 1));
    }

    [Fact]
    public void Adapter_preserves_validation_order_and_FFT_parameter_names()
    {
        Assert.Equal("map", Assert.Throws<ArgumentNullException>(() =>
            FlatGridMovementRange.Calculate(null!, new(-1, 0, 0), -1)).ParamName);
        var map = CreateFlatMap(2, 1, 1);
        Assert.Equal("move", Assert.Throws<ArgumentOutOfRangeException>(() =>
            FlatGridMovementRange.Calculate(map, new(-1, 0, 0), -1)).ParamName);
        Assert.Equal("origin", Assert.Throws<ArgumentOutOfRangeException>(() =>
            FlatGridMovementRange.Calculate(map, new(0, 0, 1), 0)).ParamName);
    }

    [Fact]
    public void Adapter_preserves_non_square_upper_layer_membership_and_immutable_output()
    {
        var map = CreateFlatMap(4, 2, 2);
        var origin = new TilePosition(3, 1, 1);
        Assert.Empty(FlatGridMovementRange.Calculate(map, origin, 0));
        var actual = FlatGridMovementRange.Calculate(map, origin, 1);
        Assert.True(actual.SetEquals(new TilePosition[] { new(2, 1, 1), new(3, 0, 1) }));
        var collection = Assert.IsAssignableFrom<ICollection<TilePosition>>(actual);
        Assert.Throws<NotSupportedException>(() => collection.Clear());
    }

    private static BattleMapDefinition CreateFlatMap(int width, int depth, int layerCount)
    {
        var tiles = Enumerable.Repeat(new BattleTile(0), width * depth * layerCount).ToArray();
        return new BattleMapDefinition(width, depth, layerCount, tiles);
    }
}
