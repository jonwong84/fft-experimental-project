using Zodiac.Framework;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class FlatGridMovementRangeTests
{
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
