using Zodiac.Framework;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class TurnBattleSimulatorTests
{
    private static readonly BattleUnitId A = new("sample:unit/a");
    private static readonly BattleUnitId B = new("sample:unit/b");

    [Fact]
    public void Inactive_move_act_and_wait_preserve_composed_state_and_rng()
    {
        TurnBattleState initial = Initial();
        foreach (TurnBattleResult result in new[] {
            TurnBattleSimulator.Move(initial, new(A, new(1, 0, 0))),
            TurnBattleSimulator.ConfirmAct(initial, A), TurnBattleSimulator.Wait(initial, A) })
            AssertRejected(initial, result, TurnCycleRejection.InactiveUnit);

        TurnBattleState active = TurnBattleSimulator.Advance(initial).State;
        foreach (TurnBattleResult result in new[] {
            TurnBattleSimulator.Move(active, new(B, new(3, 0, 0))),
            TurnBattleSimulator.ConfirmAct(active, B), TurnBattleSimulator.Wait(active, B) })
            AssertRejected(active, result, TurnCycleRejection.InactiveUnit);
    }

    [Fact]
    public void Move_then_act_completes_with_ordered_events_without_extra_draws()
    {
        TurnBattleState active = TurnBattleSimulator.Advance(Initial()).State;
        var moved = TurnBattleSimulator.Move(active, new(A, new(1, 0, 0)));
        Assert.True(moved.Accepted);
        Assert.Collection(moved.Events,
            ev => Assert.Equal(new BattleMovementEvent(A, new(0, 0, 0), new(1, 0, 0)), Assert.IsType<TurnBattleMovementEvent>(ev).Movement),
            ev => Assert.Equal(new TurnChoiceConsumedEvent(A, TurnCommandKind.ConfirmMove), Assert.IsType<TurnBattleClockEvent>(ev).Event));
        Assert.True(moved.State.Battle.Units[0].HasMoved);
        Assert.True(moved.State.Turns.Units[0].MovementTaken);
        var acted = TurnBattleSimulator.ConfirmAct(moved.State, A);
        Assert.True(acted.Accepted);
        Assert.Collection(acted.Events,
            ev => Assert.Equal(new TurnChoiceConsumedEvent(A, TurnCommandKind.ConfirmAct), Assert.IsType<TurnBattleClockEvent>(ev).Event),
            ev => Assert.Equal(new TurnEndedEvent(A, 0, 0), Assert.IsType<TurnBattleClockEvent>(ev).Event));
        Assert.Null(acted.State.Turns.ActiveUnitId);
        Assert.Equal(active.Turns.RngState, acted.State.Turns.RngState);
        Assert.Equal(active.Turns.Ticks, acted.State.Turns.Ticks);
        AssertRejected(acted.State, TurnBattleSimulator.Wait(acted.State, A), TurnCycleRejection.InactiveUnit);
        Assert.Equal(new TilePosition(0, 0, 0), active.Battle.Units[0].Position);
        Assert.False(active.Battle.Units[0].HasMoved);
    }

    [Fact]
    public void Act_then_move_publishes_movement_before_choice_and_automatic_completion()
    {
        var active = TurnBattleSimulator.Advance(Initial()).State;
        var acted = TurnBattleSimulator.ConfirmAct(active, A);
        var moved = TurnBattleSimulator.Move(acted.State, new(A, new(1, 0, 0)));
        Assert.True(moved.Accepted);
        Assert.Collection(moved.Events,
            ev => Assert.IsType<TurnBattleMovementEvent>(ev),
            ev => Assert.IsType<TurnChoiceConsumedEvent>(Assert.IsType<TurnBattleClockEvent>(ev).Event),
            ev => Assert.IsType<TurnEndedEvent>(Assert.IsType<TurnBattleClockEvent>(ev).Event));
        Assert.Null(moved.State.Turns.ActiveUnitId);
        Assert.Equal(active.Turns.RngState, moved.State.Battle.RngState);
    }

    [Theory]
    [InlineData(4, BattleTransitionRejection.DestinationOccupied)]
    [InlineData(3, BattleTransitionRejection.DestinationNotReachable)]
    public void Invalid_movement_does_not_consume_choice_or_change_clock_rng_or_positions(int x, BattleTransitionRejection reason)
    {
        var active = TurnBattleSimulator.Advance(Initial()).State;
        var failed = TurnBattleSimulator.Move(active, new(A, new(x, 0, 0)));
        Assert.False(failed.Accepted);
        Assert.Same(active, failed.State);
        Assert.Empty(failed.Events);
        Assert.Equal(reason, failed.MovementRejection);
        Assert.Equal(TurnCycleRejection.None, failed.TurnRejection);
        Assert.False(failed.State.Turns.Units[0].MovementTaken);
        Assert.False(failed.State.Battle.Units[0].HasMoved);
        Assert.Equal(active.Turns.RngState, failed.State.Battle.RngState);
        Assert.Equal(active.Turns.Units[0].Ct, failed.State.Turns.Units[0].Ct);
        Assert.True(TurnBattleSimulator.Move(failed.State, new(A, new(1, 0, 0))).Accepted);
    }

    [Fact]
    public void Ready_peer_precedes_a_tick_and_next_readiness_restores_movement_only_for_selected_unit()
    {
        var aReady = TurnBattleSimulator.Advance(Initial()).State;
        var aMoved = TurnBattleSimulator.Move(aReady, new(A, new(1, 0, 0))).State;
        var aEnded = TurnBattleSimulator.ConfirmAct(aMoved, A).State;
        var bReadyResult = TurnBattleSimulator.Advance(aEnded);
        Assert.Equal(B, bReadyResult.State.Turns.ActiveUnitId);
        Assert.Equal(10ul, bReadyResult.State.Turns.Ticks);
        Assert.Equal(2ul, bReadyResult.State.Turns.RngState.DrawCount);
        Assert.IsType<TurnReadyEvent>(Assert.IsType<TurnBattleClockEvent>(Assert.Single(bReadyResult.Events)).Event);
        Assert.True(bReadyResult.State.Battle.Units[0].HasMoved);
        var bMoved = TurnBattleSimulator.Move(bReadyResult.State, new(B, new(3, 0, 0))).State;
        var bEnded = TurnBattleSimulator.ConfirmAct(bMoved, B).State;
        var next = TurnBattleSimulator.Advance(bEnded).State;
        Assert.Equal(A, next.Turns.ActiveUnitId);
        Assert.Equal(20ul, next.Turns.Ticks);
        Assert.Equal(3ul, next.Turns.RngState.DrawCount);
        Assert.False(next.Battle.Units[0].HasMoved);
        Assert.True(next.Battle.Units[1].HasMoved);
        Assert.Equal(new TilePosition(1, 0, 0), next.Battle.Units[0].Position);
        Assert.Equal(new TilePosition(3, 0, 0), next.Battle.Units[1].Position);
        var secondMove = TurnBattleSimulator.Move(next, new(A, new(2, 0, 0)));
        Assert.True(secondMove.Accepted);
        Assert.Equal(new TilePosition(3, 0, 0), secondMove.State.Battle.Units[1].Position);
    }

    [Theory]
    [InlineData("rng")]
    [InlineData("missing-rng")]
    [InlineData("ids")]
    [InlineData("count")]
    [InlineData("flags")]
    public void Malformed_composed_snapshots_fail_before_commands(string invalid)
    {
        var original = Initial();
        BattleState battle = original.Battle;
        if (invalid == "rng") battle = new(battle.Map, battle.Units, new FftRandomState(2, 0));
        if (invalid == "missing-rng") battle = new(battle.Map, battle.Units);
        if (invalid == "count") battle = new(battle.Map, battle.Units.Take(1), battle.RngState);
        if (invalid == "ids") battle = new(battle.Map, new[] { battle.Units[0],
            new BattleUnitState(new("sample:unit/other"), new(4, 0, 0), 2) }, battle.RngState);
        if (invalid == "flags") battle = new(battle.Map, new[] {
            new BattleUnitState(A, new(0, 0, 0), 2, hasMoved: true), battle.Units[1] }, battle.RngState);
        Assert.Throws<ArgumentException>(() => new TurnBattleState(battle, original.Turns));
    }

    [Fact]
    public void Replaying_combined_commands_reproduces_events_all_state_fields_and_rng()
    {
        var first = Replay();
        var second = Replay();
        Assert.Equal(first, second);
        Assert.Contains("reject:InactiveUnit", first);
        Assert.Contains("reject:None/DestinationNotReachable", first);
    }

    private static string Replay()
    {
        var state = Initial();
        var trace = new List<string>();
        Add(TurnBattleSimulator.Move(state, new(A, new(1, 0, 0))));
        Add(TurnBattleSimulator.Advance(state));
        Add(TurnBattleSimulator.Move(state, new(A, new(3, 0, 0))));
        Add(TurnBattleSimulator.Move(state, new(A, new(1, 0, 0))));
        Add(TurnBattleSimulator.ConfirmAct(state, A));
        Add(TurnBattleSimulator.Advance(state));
        Add(TurnBattleSimulator.Wait(state, B));
        Add(TurnBattleSimulator.Advance(state));
        Add(TurnBattleSimulator.ConfirmAct(state, B));
        Add(TurnBattleSimulator.Wait(state, B));
        Add(TurnBattleSimulator.Advance(state));
        Assert.Equal(A, state.Turns.ActiveUnitId);
        Assert.Equal(20ul, state.Turns.Ticks);
        Assert.Equal(4ul, state.Turns.RngState.DrawCount);
        return string.Join("\n", trace);

        void Add(TurnBattleResult result)
        {
            trace.Add(result.Accepted ? "accepted" : $"reject:{result.TurnRejection}/{result.MovementRejection}");
            trace.AddRange(result.Events.Select(ev => ev.ToString()));
            state = result.State;
            trace.Add($"{state.Turns.Phase}/{state.Turns.ActiveUnitId}/{state.Turns.Ticks}/{state.Turns.RngState}/{state.Battle.RngState}");
            foreach (TurnUnitState unit in state.Turns.Units) trace.Add(unit.ToString());
            foreach (BattleUnitState unit in state.Battle.Units)
                trace.Add($"{unit.Id}/{unit.Position}/{unit.HasMoved}/{unit.Move}/{unit.MovementProfile.Jump}/{unit.MovementProfile.BodyHeight}/{unit.MovementProfile.Mode}/{unit.MovementProfile.Traits}");
        }
    }

    private static TurnBattleState Initial()
    {
        var map = new BattleMapDefinition(5, 1, 1, Enumerable.Repeat(new BattleTile(10), 5).ToArray());
        var rng = FftBiosRandom.Seed(1);
        var battle = new BattleState(map, new[] { new BattleUnitState(A, new(0, 0, 0), 2), new(B, new(4, 0, 0), 2) }, rng);
        var turns = new TurnCycleState(new[] { new TurnUnitState(A, 10, 0, 0), new(B, 10, 0, 1) }, rng);
        return new(battle, turns);
    }

    private static void AssertRejected(TurnBattleState before, TurnBattleResult result, TurnCycleRejection reason)
    {
        Assert.False(result.Accepted);
        Assert.Same(before, result.State);
        Assert.Empty(result.Events);
        Assert.Equal(reason, result.TurnRejection);
        Assert.Equal(before.Turns.RngState, result.State.Battle.RngState);
    }
}
