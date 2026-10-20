using Fft.Core;
using Xunit;

namespace Fft.Core.Tests;

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

    private static BattleMapDefinition CreateFlatMap(int width, int depth, int layerCount)
    {
        var tiles = Enumerable.Repeat(new BattleTile(0), width * depth * layerCount).ToArray();
        return new BattleMapDefinition(width, depth, layerCount, tiles);
    }
}