using System.Text.Json;
using Fft.Client.Presentation;
using Zodiac.Content.Samples;
using Zodiac.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace Fft.Client.Tests;

public sealed class BattleUiInputTests
{
    [Fact]
    public void Held_inputs_submit_once_and_activation_baselines_do_not_generate_commands()
    {
        var latch = new BattleInputLatch();
        Assert.True(latch.KeyDown(Keys.W)); Assert.False(latch.KeyDown(Keys.W));
        latch.KeyUp(Keys.W); Assert.True(latch.KeyDown(Keys.W));
        Assert.True(latch.LeftPress(true)); Assert.False(latch.LeftPress(true));
        Assert.False(latch.LeftPress(false)); Assert.True(latch.LeftPress(true));
        latch.Reset([Keys.N], true);
        Assert.False(latch.KeyDown(Keys.N)); Assert.False(latch.LeftPress(true));
        latch.KeyUp(Keys.N); Assert.True(latch.KeyDown(Keys.N));
        Assert.False(latch.LeftPress(false)); Assert.True(latch.LeftPress(true));
        latch.Reset([], false); Assert.True(latch.KeyDown(Keys.N));
    }

    [Fact]
    public void Programmed_pointer_and_keyboard_routes_complete_the_exact_headless_battle_with_rejections()
    {
        var ui = new BattleInteraction(SyntheticTwoUnitBattle.Create()); var router = new BattleUiRouter(ui);
        var accepted = new List<TwoUnitBattleCommand>(); var rejected = new List<TwoUnitBattleResult>();
        foreach (var step in BattleUiCheckPlan.Inputs)
        {
            int before = ui.Journal.Length; var state = ui.State;
            if (step.Key is { } key) router.Key(key);
            if (step.Click is { } click) router.Click(click);
            if (ui.Journal.Length == before) continue;
            if (ui.LastResult!.Accepted) accepted.Add(ui.Journal[^1]);
            else { rejected.Add(ui.LastResult); Assert.Same(state, ui.State); }
        }
        Assert.Equal(SyntheticTwoUnitBattle.Commands.ToArray(), accepted);
        Assert.Equal(14, ui.Journal.Length); Assert.Equal(2, rejected.Count);
        Assert.Equal(TurnCycleRejection.TurnAlreadyActive, rejected[0].TurnRejection);
        Assert.Equal(TwoUnitBattleRejection.AlreadyCompleted, rejected[1].ScenarioRejection);
        Assert.Equal(Summary(BattleUiCheckPlan.Expected()), Summary(ui.State));
        Assert.Equal(new FftRandomState(2473755763, 22), ui.State.Battle.TurnBattle.Turns.RngState);
        var terminal = ui.State;
        router.Key(Keys.R);
        Assert.Empty(ui.Journal); Assert.Null(ui.SelectedUnit);
        Assert.Equal(TwoUnitBattleOutcome.InProgress, ui.State.Outcome);
        Assert.Equal(new FftRandomState(1, 0), ui.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(TwoUnitBattleOutcome.FirstUnitWon, terminal.Outcome);
    }

    [Fact]
    public void Keyboard_selection_navigation_cancel_and_confirmation_share_the_pointer_actions()
    {
        var ui = new BattleInteraction(SyntheticTwoUnitBattle.Create()); var router = new BattleUiRouter(ui);
        router.Key(Keys.D2); Assert.Equal(SyntheticTwoUnitBattle.SecondUnitId, ui.SelectedUnit);
        router.Key(Keys.Tab); Assert.Equal(SyntheticTwoUnitBattle.FirstUnitId, ui.SelectedUnit);
        router.Key(Keys.D1); router.Key(Keys.Space); router.Key(Keys.M);
        router.Key(Keys.Down); router.Key(Keys.Enter);
        Assert.Equal(new TilePosition(0, 1, 0), ui.State.Battle.TurnBattle.Battle.Units[0].Position);
        router.Key(Keys.A); router.Key(Keys.Back);
        Assert.Equal(BattleInteractionMode.Inspect, ui.Mode);
        int attempts = ui.Journal.Length; router.Key(Keys.F12);
        Assert.Equal(attempts, ui.Journal.Length);
        router.Key(Keys.Right); Assert.Equal(new TilePosition(1, 1, 0), router.Cursor);
        router.Key(Keys.Left); router.Key(Keys.Up); Assert.Equal(new TilePosition(0, 0, 0), router.Cursor);
    }

    private static string Summary(TwoUnitBattleState state) => JsonSerializer.Serialize(new
    {
        state.Outcome, state.WinnerId, state.Battle.Combatants, state.Battle.TurnBattle.Turns,
        units = state.Battle.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved }),
        state.Battle.TurnBattle.Battle.RngState,
    });
}
