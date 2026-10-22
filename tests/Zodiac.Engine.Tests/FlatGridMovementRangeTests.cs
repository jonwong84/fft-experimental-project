using Xunit;

namespace Zodiac.Engine.Tests;

public sealed class FlatGridMovementRangeTests
{
    [Fact]
    public void One_step_from_corner_is_clipped_and_does_not_include_diagonals()
    {
        var actual = FlatGridMovementRange.Calculate(new GridBounds(5, 2, 3), new(0, 0, 2), 1);
        Assert.True(actual.SetEquals(new GridPosition[] { new(1, 0, 2), new(0, 1, 2) }));
    }

    [Theory]
    [InlineData(1, 1, 1, 0, 0, 0)]
    [InlineData(5, 3, 2, 2, 1, 1)]
    [InlineData(2, 7, 3, 1, 6, 2)]
    public void Membership_matches_independent_Manhattan_distance_for_each_budget(
        int width, int depth, int layers, int x, int y, int layer)
    {
        var bounds = new GridBounds(width, depth, layers);
        var origin = new GridPosition(x, y, layer);
        for (int budget = 0; budget <= width + depth; budget++)
        {
            var actual = FlatGridMovementRange.Calculate(bounds, origin, budget);
            var expected = new HashSet<GridPosition>();
            for (int cy = 0; cy < depth; cy++)
            for (int cx = 0; cx < width; cx++)
            {
                int distance = Math.Abs(cx - x) + Math.Abs(cy - y);
                if (distance > 0 && distance <= budget)
                    expected.Add(new(cx, cy, layer));
            }

            Assert.True(actual.SetEquals(expected));
        }
    }

    [Fact]
    public void Maximum_budget_fills_only_origin_layer_without_overflow()
    {
        var actual = FlatGridMovementRange.Calculate(new GridBounds(2, 2, 2), new(1, 1, 1), int.MaxValue);
        Assert.True(actual.SetEquals(new GridPosition[] { new(0, 0, 1), new(1, 0, 1), new(0, 1, 1) }));
    }

    [Fact]
    public void Large_dimensions_need_no_tile_allocation_and_coordinate_steps_do_not_wrap()
    {
        var bounds = new GridBounds(int.MaxValue, int.MaxValue, int.MaxValue);
        var origin = new GridPosition(int.MaxValue - 1, int.MaxValue - 1, int.MaxValue - 1);
        Assert.Empty(FlatGridMovementRange.Calculate(bounds, origin, 0));
        Assert.True(FlatGridMovementRange.Calculate(bounds, origin, 1).SetEquals(new GridPosition[]
        {
            new(int.MaxValue - 2, int.MaxValue - 1, int.MaxValue - 1),
            new(int.MaxValue - 1, int.MaxValue - 2, int.MaxValue - 1),
        }));
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(3, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 2, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(0, 0, 2)]
    public void Out_of_bounds_origin_is_rejected_even_with_zero_budget(int x, int y, int layer)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            FlatGridMovementRange.Calculate(new GridBounds(3, 2, 2), new(x, y, layer), 0));
        Assert.Equal("origin", exception.ParamName);
    }

    [Fact]
    public void Validation_checks_bounds_then_budget_then_origin()
    {
        Assert.Equal("bounds", Assert.Throws<ArgumentNullException>(() =>
            FlatGridMovementRange.Calculate(null!, new(-1, 0, 0), -1)).ParamName);
        Assert.Equal("move", Assert.Throws<ArgumentOutOfRangeException>(() =>
            FlatGridMovementRange.Calculate(new GridBounds(1, 1, 1), new(-1, 0, 0), -1)).ParamName);
    }

    [Fact]
    public void Result_cannot_be_mutated_and_later_calls_leave_it_unchanged()
    {
        var bounds = new GridBounds(3, 3, 1);
        var origin = new GridPosition(1, 1, 0);
        var actual = FlatGridMovementRange.Calculate(bounds, origin, 1);
        var snapshot = actual.ToHashSet();
        // FrozenSet exposes the mutable interfaces for compatibility, but rejects writes.
        var collection = Assert.IsAssignableFrom<ICollection<GridPosition>>(actual);
        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Add(new(0, 0, 0)));
        Assert.Throws<NotSupportedException>(() => collection.Clear());
        FlatGridMovementRange.Calculate(bounds, origin, 2);
        Assert.True(actual.SetEquals(snapshot));
        Assert.Equal(new GridBounds(3, 3, 1), bounds);
        Assert.Equal(new GridPosition(1, 1, 0), origin);
    }
}
