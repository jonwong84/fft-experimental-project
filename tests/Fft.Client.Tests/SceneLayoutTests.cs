using Fft.Client.Presentation;
using Zodiac.Framework;
using Xunit;

namespace Fft.Client.Tests;

public sealed class SceneLayoutTests
{
    [Theory]
    [InlineData(960, 640, 0, 0, 960, 640)]
    [InlineData(1920, 1080, 150, 0, 1620, 1080)]
    [InlineData(640, 960, 0, 267, 640, 426)]
    [InlineData(2000, 640, 520, 0, 960, 640)]
    [InlineData(1, 100, 0, 49, 1, 1)]
    [InlineData(100, 1, 49, 0, 1, 1)]
    [InlineData(0, 640, 0, 0, 0, 0)]
    [InlineData(960, 0, 0, 0, 0, 0)]
    [InlineData(-1, 640, 0, 0, 0, 0)]
    [InlineData(960, -1, 0, 0, 0, 0)]
    public void Fit_centers_the_letterbox_and_handles_minimized_outputs(int width, int height,
        int x, int y, int expectedWidth, int expectedHeight) =>
        Assert.Equal(new PixelRect(x, y, expectedWidth, expectedHeight), SceneLayout.Fit(width, height));

    [Fact]
    public void Fit_uses_wide_arithmetic_for_large_outputs()
    {
        Assert.Equal(new PixelRect(0, 357913941, int.MaxValue, 1431655764), SceneLayout.Fit(int.MaxValue, int.MaxValue));
        Assert.Equal(new PixelRect(0, 1073741823, 1, 1), SceneLayout.Fit(1, int.MaxValue));
    }

    [Theory]
    [InlineData(0, 0, 480, 300)]
    [InlineData(1, 0, 560, 340)]
    [InlineData(0, 1, 400, 340)]
    [InlineData(1, 1, 480, 380)]
    public void Two_by_two_diamonds_have_explicit_synthetic_axis_conventions(int x, int y, int px, int py) =>
        Assert.Equal(new PixelPoint(px, py), SceneLayout.TileCenter(x, y, 2, 2));

    [Fact]
    public void Rectangular_and_single_tile_maps_are_centered_on_both_axes()
    {
        Assert.Equal(new PixelPoint(480, 340), SceneLayout.TileCenter(0, 0, 1, 1));
        var first = SceneLayout.TileCenter(0, 0, 4, 2);
        var last = SceneLayout.TileCenter(3, 1, 4, 2);
        Assert.Equal(new PixelPoint(400, 260), first);
        Assert.Equal(new PixelPoint(560, 420), last);
        Assert.Equal(960, first.X + last.X);
        Assert.Equal(680, first.Y + last.Y);
    }

    [Theory]
    [InlineData(0, 0, 0, 1)] [InlineData(0, 0, 1, -1)]
    [InlineData(-1, 0, 2, 2)] [InlineData(0, -1, 2, 2)]
    [InlineData(2, 0, 2, 2)] [InlineData(0, 2, 2, 2)]
    public void Invalid_tile_coordinates_are_rejected(int x, int y, int width, int depth) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneLayout.TileCenter(x, y, width, depth));

    [Fact]
    public void Unrepresentable_pixel_coordinates_fail_instead_of_wrapping() =>
        Assert.Throws<OverflowException>(() => SceneLayout.TileCenter(0, 0, int.MaxValue, 1));

    [Fact]
    public void Unit_projection_orders_by_depth_then_x_and_is_independent_of_viewport_frames()
    {
        var surface = new TerrainSurfaceId("sample:terrain/ground");
        var map = new BattleMapDefinition(2, 2, 1, Enumerable.Repeat(new BattleTile(surface, 10), 4).ToArray());
        BattleUnitId back = new("sample:unit/back"), left = new("sample:unit/left"), right = new("sample:unit/right");
        var rng = new FftRandomState(0xfedcba98, 42);
        var bodies = new[] { new BattleUnitState(right, new(1, 0, 0), 1),
            new BattleUnitState(left, new(0, 1, 0), 1), new BattleUnitState(back, new(0, 0, 0), 1) };
        var battle = new BattleState(map, bodies, rng);
        var turns = new TurnCycleState(bodies.Select((unit, index) => new TurnUnitState(unit.Id, 10, 0, index)), rng);
        var snapshot = new AttackBattleSnapshot(new(battle, turns), bodies.Select(unit =>
            new AttackUnitState(unit.Id, 30, 50, 0, 0, 5, 10, 0, 0, 0, 70, 0,
                BattleFacing.East, AttackUnitStatus.None, 0, null)));
        var projected = SceneLayout.Units(snapshot);
        Assert.Equal(new[] { back, left, right }, projected.Select(unit => unit.Id));
        Assert.Equal(new[] { new PixelPoint(480, 300), new PixelPoint(400, 340), new PixelPoint(560, 340) }, projected.Select(unit => unit.Feet));
        Assert.All(projected, unit => { Assert.Equal(30, unit.Hp); Assert.Equal(50, unit.MaxHp); });
        _ = SceneLayout.Fit(1920, 1080);
        _ = SceneLayout.Fit(640, 960);
        Assert.Equal(projected.ToArray(), SceneLayout.Units(snapshot).ToArray());
        Assert.Same(battle, snapshot.TurnBattle.Battle);
        Assert.Equal(rng, snapshot.TurnBattle.Turns.RngState);
        Assert.Equal(bodies, snapshot.TurnBattle.Battle.Units);
        Assert.All(snapshot.Combatants, unit => Assert.Equal(30, unit.Hp));
    }
}
