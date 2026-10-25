using Fft.Client.Presentation;
using Zodiac.Content.Samples;
using Zodiac.Framework;
using Xunit;

namespace Fft.Client.Tests;

public sealed class BattleUiLayoutTests
{
    [Theory]
    [InlineData(960, 640, 960, 640, 480, 320, 480, 320)]
    [InlineData(1920, 1080, 1920, 1080, 960, 540, 480, 320)]
    [InlineData(640, 960, 640, 960, 320, 480, 480, 320)]
    [InlineData(960, 540, 1920, 1080, 480, 270, 480, 320)]
    [InlineData(1280, 720, 1920, 1080, 640, 360, 480, 320)]
    [InlineData(960, 640, 1920, 1080, 480, 320, 480, 320)]
    [InlineData(1920, 1080, 1920, 1080, 150, 0, 0, 0)]
    [InlineData(1920, 1080, 1920, 1080, 1769, 1079, 959, 639)]
    [InlineData(640, 960, 640, 960, 0, 267, 0, 0)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, 1073741823, 1073741823, 479, 320)]
    public void Pointer_mapping_accounts_for_letterbox_DPI_and_large_integer_dimensions(int cw, int ch,
        int bw, int bh, int x, int y, int expectedX, int expectedY) =>
        Assert.Equal(new PixelPoint(expectedX, expectedY), BattleUiLayout.CanvasPoint(new(x, y), cw, ch, bw, bh));

    [Theory]
    [InlineData(1920, 1080, 1920, 1080, 149, 540)]
    [InlineData(1920, 1080, 1920, 1080, 1770, 540)]
    [InlineData(640, 960, 640, 960, 320, 266)]
    [InlineData(640, 960, 640, 960, 320, 693)]
    [InlineData(960, 640, 960, 640, -1, 320)]
    [InlineData(960, 640, 960, 640, 960, 320)]
    [InlineData(960, 640, 960, 640, 480, 640)]
    [InlineData(0, 640, 960, 640, 0, 0)]
    [InlineData(960, -1, 960, 640, 0, 0)]
    [InlineData(960, 640, 0, 640, 0, 0)]
    [InlineData(960, 640, 960, -1, 0, 0)]
    public void Bars_outside_client_and_minimized_dimensions_have_no_canvas_pointer(int cw, int ch,
        int bw, int bh, int x, int y) =>
        Assert.Null(BattleUiLayout.CanvasPoint(new(x, y), cw, ch, bw, bh));

    [Fact]
    public void Rectangle_hit_testing_is_half_open_and_avoids_addition_overflow()
    {
        var rect = new PixelRect(int.MaxValue - 2, int.MaxValue - 2, 10, 10);
        Assert.True(BattleUiLayout.Contains(rect, new(int.MaxValue, int.MaxValue)));
        Assert.False(BattleUiLayout.Contains(rect, new(int.MaxValue - 3, int.MaxValue)));
        Assert.False(BattleUiLayout.Contains(new(10, 20, 30, 40), new(40, 30)));
        Assert.False(BattleUiLayout.Contains(new(10, 20, 30, 40), new(20, 60)));
        Assert.True(BattleUiLayout.Contains(new(10, 20, 30, 40), new(10, 20)));
    }

    [Fact]
    public void Tile_centers_and_shared_edges_follow_frontmost_diamond_order()
    {
        var map = SyntheticTwoUnitBattle.Create().InitialBattle.TurnBattle.Battle.Map;
        for (int y = 0; y < 2; y++)
        for (int x = 0; x < 2; x++)
            Assert.Equal(new TilePosition(x, y, 0), BattleUiLayout.PickTile(SceneLayout.TileCenter(x, y, 2, 2), map));
        Assert.Equal(new TilePosition(1, 0, 0), BattleUiLayout.PickTile(new(520, 320), map));
        Assert.Equal(new TilePosition(1, 1, 0), BattleUiLayout.PickTile(new(440, 360), map));
        Assert.Null(BattleUiLayout.PickTile(new(int.MinValue, int.MaxValue), map));
        Assert.Null(BattleUiLayout.PickTile(new(20, 20), map));
    }

    [Fact]
    public void Sprite_rectangle_selection_precedes_the_floor_under_its_upper_half()
    {
        var interaction = new BattleInteraction(SyntheticTwoUnitBattle.Create());
        var router = new BattleUiRouter(interaction);
        var before = interaction.State;
        var point = new PixelPoint(480, 320);
        Assert.Equal(new TilePosition(0, 0, 0), BattleUiLayout.PickTile(point, before.Battle.TurnBattle.Battle.Map));
        var second = SceneLayout.Units(before.Battle).Single(unit => unit.Id == SyntheticTwoUnitBattle.SecondUnitId);
        Assert.True(BattleUiLayout.Contains(BattleUiLayout.UnitBounds(second), point));
        router.Click(point);
        Assert.Equal(SyntheticTwoUnitBattle.SecondUnitId, interaction.SelectedUnit);
        Assert.Equal(new TilePosition(1, 1, 0), router.Cursor);
        Assert.Same(before, interaction.State); Assert.Empty(interaction.Journal);
    }

    [Fact]
    public void Pointer_buttons_and_tiles_submit_real_move_attack_and_rejected_commands_atomically()
    {
        var interaction = new BattleInteraction(SyntheticTwoUnitBattle.Create());
        var router = new BattleUiRouter(interaction);
        ClickButton(router, BattleUiAction.NextTurn);
        Assert.Equal(SyntheticTwoUnitBattle.FirstUnitId, interaction.SelectedUnit);
        Assert.Equal(10ul, interaction.State.Battle.TurnBattle.Turns.Ticks);
        ClickButton(router, BattleUiAction.Move);
        var before = interaction.State;
        router.Click(new(480, 320)); // Sprite B wins over A's floor; occupied destination is submitted.
        Assert.Same(before, interaction.State); Assert.False(interaction.LastResult!.Accepted);
        Assert.Equal(BattleTransitionRejection.DestinationOccupied, interaction.LastResult.MovementRejection);
        Assert.Equal(BattleInteractionMode.Move, interaction.Mode);
        router.Click(new(400, 340));
        Assert.True(interaction.LastResult!.Accepted);
        Assert.Equal(new TilePosition(0, 1, 0), interaction.State.Battle.TurnBattle.Battle.Units
            .Single(unit => unit.Id == SyntheticTwoUnitBattle.FirstUnitId).Position);
        ClickButton(router, BattleUiAction.Attack);
        router.Click(new(480, 320));
        Assert.True(interaction.LastResult!.Accepted);
        Assert.Equal(25, interaction.State.Battle.Combatants.Single(unit => unit.UnitId == SyntheticTwoUnitBattle.SecondUnitId).Hp);
        Assert.Equal(new FftRandomState(3655513600, 7), interaction.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(BattleInteractionMode.Inspect, interaction.Mode);
        before = interaction.State;
        ClickButton(router, BattleUiAction.Move); router.Click(new(480, 300));
        Assert.Same(before, interaction.State); Assert.False(interaction.LastResult!.Accepted);
        Assert.Equal(TurnCycleRejection.InactiveUnit, interaction.LastResult.TurnRejection);
        Assert.Equal(5, interaction.Journal.Length);
    }

    [Fact]
    public void Selection_cycle_cursor_clamps_confirm_cancel_wait_and_restart_share_semantic_routes()
    {
        var interaction = new BattleInteraction(SyntheticTwoUnitBattle.Create());
        var router = new BattleUiRouter(interaction);
        router.Action(BattleUiAction.SelectNext);
        Assert.Equal(SyntheticTwoUnitBattle.FirstUnitId, interaction.SelectedUnit);
        router.Action(BattleUiAction.SelectNext);
        Assert.Equal(SyntheticTwoUnitBattle.SecondUnitId, interaction.SelectedUnit);
        Assert.Equal(new TilePosition(1, 1, 0), router.Cursor);
        router.MoveCursor(int.MinValue, int.MinValue); Assert.Equal(new TilePosition(0, 0, 0), router.Cursor);
        router.MoveCursor(int.MaxValue, int.MaxValue); Assert.Equal(new TilePosition(1, 1, 0), router.Cursor);
        router.Action(BattleUiAction.Confirm);
        Assert.Equal(SyntheticTwoUnitBattle.SecondUnitId, interaction.SelectedUnit);
        Assert.Empty(interaction.Journal);
        ClickButton(router, BattleUiAction.Move); router.Action(BattleUiAction.Cancel);
        Assert.Equal(BattleInteractionMode.Inspect, interaction.Mode);
        ClickButton(router, BattleUiAction.NextTurn);
        router.Action(BattleUiAction.SelectSecond);
        var before = interaction.State;
        ClickButton(router, BattleUiAction.Wait);
        Assert.Same(before, interaction.State); Assert.Equal(TurnCycleRejection.InactiveUnit, interaction.LastResult!.TurnRejection);
        router.Action(BattleUiAction.SelectFirst); ClickButton(router, BattleUiAction.Wait);
        Assert.True(interaction.LastResult!.Accepted);
        Assert.Null(interaction.State.Battle.TurnBattle.Turns.ActiveUnitId);
        ClickButton(router, BattleUiAction.Restart);
        Assert.Empty(interaction.Journal); Assert.Null(interaction.SelectedUnit); Assert.Null(interaction.LastResult);
        Assert.Equal(new TilePosition(0, 0, 0), router.Cursor);
        Assert.Equal(new FftRandomState(1, 0), interaction.State.Battle.TurnBattle.Turns.RngState);
    }

    private static void ClickButton(BattleUiRouter router, BattleUiAction action)
    {
        var button = BattleUiLayout.Buttons.Single(button => button.Action == action);
        router.Click(new(button.Bounds.X + button.Bounds.Width / 2, button.Bounds.Y + button.Bounds.Height / 2));
    }
}
