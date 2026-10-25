using Zodiac.Framework;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class TilePositionTests
{
    [Fact]
    public void Constructor_preserves_coordinates()
    {
        var position = new TilePosition(3, 7, 1);

        Assert.Equal(3, position.X);
        Assert.Equal(7, position.Y);
        Assert.Equal(1, position.Layer);
    }

    [Fact]
    public void Positions_with_equal_coordinates_are_equal_and_have_equal_hash_codes()
    {
        var left = new TilePosition(2, 4, 0);
        var right = new TilePosition(2, 4, 0);

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Theory]
    [InlineData(4, 7, 1)]
    [InlineData(3, 8, 1)]
    [InlineData(3, 7, 0)]
    public void Changing_any_coordinate_makes_positions_unequal(int x, int y, int layer)
    {
        var original = new TilePosition(3, 7, 1);
        var other = new TilePosition(x, y, layer);

        Assert.NotEqual(original, other);
        Assert.True(original != other);
    }
}
