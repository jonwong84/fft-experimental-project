using System.Text.Json;
using Fft.Client.Presentation;
using Zodiac.Content.Samples;
using Zodiac.Framework;
using Xunit;

namespace Fft.Client.Tests;

public sealed class BattleInteractionTests
{
    private static BattleUnitId A => SyntheticTwoUnitBattle.FirstUnitId;
    private static BattleUnitId B => SyntheticTwoUnitBattle.SecondUnitId;

    [Fact]
    public void Full_authored_script_through_interaction_matches_headless_replay_and_journal()
    {
        var definition = SyntheticTwoUnitBattle.Create();
        var ui = new BattleInteraction(definition);
        var expected = definition.Start();
        foreach (var command in SyntheticTwoUnitBattle.Commands)
        {
            expected = TwoUnitBattleScenario.Apply(expected, command).State;
            Route(ui, command);
            Assert.True(ui.LastResult!.Accepted);
            Assert.Equal(Snapshot(expected), Snapshot(ui.State));
            Assert.Equal(BattleInteractionMode.Inspect, ui.Mode);
        }
        Assert.Equal(TwoUnitBattleOutcome.FirstUnitWon, ui.State.Outcome);
        Assert.Equal(new FftRandomState(2473755763, 22), ui.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(SyntheticTwoUnitBattle.Commands.ToArray(), ui.Journal.ToArray());
        var replay = definition.Start();
        foreach (var command in ui.Journal) replay = TwoUnitBattleScenario.Apply(replay, command).State;
        Assert.Equal(Snapshot(ui.State), Snapshot(replay));
        Assert.Contains("A WINS", ui.Status);
    }

    [Fact]
    public void Rejected_attempts_are_journaled_and_preserve_the_current_state_and_mode()
    {
        var ui = new BattleInteraction(SyntheticTwoUnitBattle.Create());
        ui.Advance(); var state = ui.State;
        ui.Select(B); ui.Wait();
        Assert.Same(state, ui.State); Assert.False(ui.LastResult!.Accepted);
        Assert.Equal(TurnCycleRejection.InactiveUnit, ui.LastResult.TurnRejection);
        Assert.Equal("INACTIVE UNIT", ui.Status);
        Assert.True(ui.StatusIsError);
        ui.SetMode(BattleInteractionMode.Move); Assert.False(ui.StatusIsError);
        ui.Select(A); ui.SetMode(BattleInteractionMode.Attack); ui.ClickTile(new(1, 1, 0));
        Assert.Same(state, ui.State); Assert.False(ui.LastResult!.Accepted);
        Assert.Equal(AttackCommandRejection.OutOfRange, ui.LastResult.AttackRejection);
        Assert.Equal(BattleInteractionMode.Attack, ui.Mode);
        ui.SetMode(BattleInteractionMode.Move); ui.ClickTile(new(1, 1, 0));
        Assert.Same(state, ui.State); Assert.Equal(BattleTransitionRejection.DestinationOccupied, ui.LastResult!.MovementRejection);
        Assert.Equal(4, ui.Journal.Length);
        var replay = ui.State.Definition.Start();
        foreach (var command in ui.Journal) replay = TwoUnitBattleScenario.Apply(replay, command).State;
        Assert.Equal(Snapshot(state), Snapshot(replay));
    }

    [Fact]
    public void Highlight_probes_delegate_acceptance_and_leave_RNG_history_selection_status_and_state_unchanged()
    {
        var ui = new BattleInteraction(SyntheticTwoUnitBattle.Create());
        Assert.Empty(ui.Moves); Assert.Empty(ui.Targets);
        ui.Advance();
        Assert.Equal(A, ui.SelectedUnit);
        var before = ui.State; var rng = before.Battle.TurnBattle.Turns.RngState;
        var journal = ui.Journal; var last = ui.LastResult; string status = ui.Status;
        Assert.Equal(new[] { new TilePosition(1, 0, 0), new TilePosition(0, 1, 0) }, ui.Moves);
        Assert.Empty(ui.Targets);
        Assert.Equal(ui.Moves.ToArray(), ui.Moves.ToArray());
        Assert.Same(before, ui.State); Assert.Same(last, ui.LastResult); Assert.Equal(journal, ui.Journal);
        Assert.Equal(rng, ui.State.Battle.TurnBattle.Turns.RngState); Assert.Equal(status, ui.Status);
        ui.SetMode(BattleInteractionMode.Move); ui.ClickTile(new(0, 1, 0));
        before = ui.State; journal = ui.Journal; last = ui.LastResult;
        Assert.Empty(ui.Moves); Assert.Equal(new[] { B }, ui.Targets);
        Assert.Equal(new[] { B }, ui.Targets);
        Assert.Same(before, ui.State); Assert.Same(last, ui.LastResult); Assert.Equal(journal, ui.Journal);
        ui.Select(B); Assert.Empty(ui.Targets); Assert.Empty(ui.Moves);
        ui.Select(A); ui.SetMode(BattleInteractionMode.Attack); ui.ClickTile(new(1, 1, 0));
        Assert.Empty(ui.Targets); Assert.Empty(ui.Moves); // Both choices consumed; no active turn.
    }

    [Fact]
    public void Inspect_empty_clicks_and_missing_selection_never_submit_simulation_commands()
    {
        var ui = new BattleInteraction(SyntheticTwoUnitBattle.Create());
        var initial = ui.State;
        ui.ClickTile(new(0, 0, 0)); Assert.Equal(A, ui.SelectedUnit);
        ui.ClickTile(new(0, 1, 0)); Assert.Null(ui.SelectedUnit);
        ui.Wait(); ui.SetMode(BattleInteractionMode.Move); ui.ClickTile(new(0, 1, 0));
        ui.Select(A); ui.SetMode(BattleInteractionMode.Attack); ui.ClickTile(new(0, 1, 0));
        Assert.Equal("NO TARGET ON TILE", ui.Status);
        Assert.Empty(ui.Journal); Assert.Null(ui.LastResult); Assert.Same(initial, ui.State);
        ui.Select(new("sample:unit/unknown")); Assert.Null(ui.SelectedUnit);
        Assert.Throws<ArgumentOutOfRangeException>(() => ui.SetMode((BattleInteractionMode)100));
    }

    [Fact]
    public void Action_consumption_is_checked_by_core_and_advance_selects_each_new_active_unit()
    {
        var ui = new BattleInteraction(SyntheticTwoUnitBattle.Create());
        foreach (var command in SyntheticTwoUnitBattle.Commands.Take(5)) Route(ui, command);
        Assert.Equal(B, ui.SelectedUnit);
        Assert.Equal(BattleInteractionMode.Inspect, ui.Mode);
        Assert.Empty(ui.Targets); Assert.NotEmpty(ui.Moves); // B attacked but may still move.
        var before = ui.State;
        ui.SetMode(BattleInteractionMode.Attack); ui.ClickTile(new(0, 1, 0));
        Assert.Same(before, ui.State); Assert.Equal(AttackCommandRejection.ActionAlreadyTaken, ui.LastResult!.AttackRejection);
        ui.Wait(); Assert.True(ui.LastResult!.Accepted); Assert.Equal(BattleInteractionMode.Inspect, ui.Mode);
        ui.Advance(); Assert.Equal(B, ui.SelectedUnit); Assert.True(ui.LastResult!.Accepted);
    }

    [Fact]
    public void Terminal_attempts_preserve_result_and_restart_clears_all_presentation_history()
    {
        var ui = new BattleInteraction(SyntheticTwoUnitBattle.Create());
        string initial = Snapshot(ui.State);
        foreach (var command in SyntheticTwoUnitBattle.Commands) Route(ui, command);
        var terminal = ui.State; var previousJournal = ui.Journal;
        Assert.Empty(ui.Moves); Assert.Empty(ui.Targets);
        ui.Advance();
        Assert.Same(terminal, ui.State); Assert.Equal(TwoUnitBattleRejection.AlreadyCompleted, ui.LastResult!.ScenarioRejection);
        Assert.Equal(previousJournal.Length + 1, ui.Journal.Length);
        ui.Select(A); ui.SetMode(BattleInteractionMode.Move); ui.ClickTile(new(0, 1, 0));
        Assert.Same(terminal, ui.State); Assert.Equal(TwoUnitBattleRejection.AlreadyCompleted, ui.LastResult!.ScenarioRejection);
        ui.Restart();
        Assert.Equal(initial, Snapshot(ui.State)); Assert.NotSame(terminal, ui.State);
        Assert.Null(ui.SelectedUnit); Assert.Null(ui.LastResult); Assert.Empty(ui.Journal);
        Assert.Equal(BattleInteractionMode.Inspect, ui.Mode); Assert.Equal("ADVANCE TO START", ui.Status);
        foreach (var command in SyntheticTwoUnitBattle.Commands) Route(ui, command);
        Assert.Equal(Snapshot(terminal), Snapshot(ui.State));
    }

    private static void Route(BattleInteraction ui, TwoUnitBattleCommand command)
    {
        switch (command)
        {
            case TwoUnitBattleCommand.Advance: ui.Advance(); break;
            case TwoUnitBattleCommand.Wait wait: ui.Select(wait.UnitId); ui.Wait(); break;
            case TwoUnitBattleCommand.Move move:
                ui.Select(move.UnitId); ui.SetMode(BattleInteractionMode.Move); ui.ClickTile(move.Destination); break;
            case TwoUnitBattleCommand.Attack attack:
                ui.Select(attack.ActorId); ui.SetMode(BattleInteractionMode.Attack);
                ui.ClickTile(ui.State.Battle.TurnBattle.Battle.Units.Single(unit => unit.Id == attack.TargetId).Position); break;
            default: throw new InvalidOperationException();
        }
    }

    private static string Snapshot(TwoUnitBattleState state) => JsonSerializer.Serialize(new
    {
        state.Outcome, state.WinnerId, state.Battle.Combatants, state.Battle.TurnBattle.Turns,
        state.Battle.TurnBattle.Battle.RngState,
        units = state.Battle.TurnBattle.Battle.Units.Select(unit => new
        {
            unit.Id, unit.Position, unit.HasMoved, unit.MovementProfile.Move, unit.MovementProfile.Jump,
            unit.MovementProfile.BodyHeight, unit.MovementProfile.Mode, unit.MovementProfile.Traits,
            costs = unit.MovementProfile.TerrainCosts.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                .Select(pair => new { id = pair.Key.Value, pair.Value }),
        }),
    });
}
