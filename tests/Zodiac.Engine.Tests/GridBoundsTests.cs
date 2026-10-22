using Xunit;

namespace Zodiac.Engine.Tests;

public sealed class GridBoundsTests
{
    [Theory]
    [InlineData(0, 1, 1, "width")]
    [InlineData(-1, 1, 1, "width")]
    [InlineData(1, 0, 1, "depth")]
    [InlineData(1, -1, 1, "depth")]
    [InlineData(1, 1, 0, "layerCount")]
    [InlineData(1, 1, -1, "layerCount")]
    public void Dimensions_must_be_positive(int width, int depth, int layers, string parameter)
    {
        Assert.Equal(parameter, Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GridBounds(width, depth, layers)).ParamName);
    }

    [Fact]
    public void Position_equality_includes_layer_and_bounds_use_half_open_intervals()
    {
        var bounds = new GridBounds(3, 2, 2);
        Assert.True(bounds.Contains(default));
        Assert.True(bounds.Contains(new(2, 1, 1)));
        Assert.False(bounds.Contains(new(3, 1, 1)));
        Assert.Equal(new GridPosition(2, 1, 1), new GridPosition(2, 1, 1));
        Assert.NotEqual(new GridPosition(2, 1, 1), new GridPosition(2, 1, 0));
    }
}
