using System.Collections.Immutable;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiMoveFirstExecutionTests;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiPostActExecutionTests;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiTurnFlowTests
{
    [Fact]
    public void Move_first_retains_one_selection_through_actual_ACT_and_receipt_facing_for_both_reach_branches()
    {
        foreach (bool outside in new[] { false, true })
        foreach (bool second in new[] { false, true })
        {
            var state = Ready(move: outside ? 1 : 3, target: outside ? new(6, 1, 0) : null, second: second);
            string before = Snapshot(state.Battle);
            var first = MoveThenAct(state, outside, second);
            var replay = MoveThenAct(state, outside, second);
            var actor = second ? B : A;
            Assert.Equal(new RandomState(outside ? 3297241969u : 2102736524u, outside ? 23ul : 50ul), first.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(3ul, first.State.Revision - state.Revision); // Confirmed Move, actual ACT, receipt-facing.
            Assert.Null(first.State.PendingFacing);
            var turn = first.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == actor);
            Assert.False(turn.HasTurn); Assert.True(turn.MovementTaken); Assert.True(turn.ActionTaken); Assert.Equal(15, turn.Ct);
            int pathIndex = Array.FindIndex(first.Events.ToArray(), e => e is VoluntaryPathCompletedEvent);
            int actIndex = Array.FindIndex(first.Events.ToArray(), e => e is ThrowStoneResolvedEvent);
            int facingIndex = Array.FindIndex(first.Events.ToArray(), e => e is FacingSettledEvent);
            Assert.True(pathIndex >= 0 && pathIndex < actIndex && actIndex < facingIndex);
            Assert.Equal(Snapshot(first.State.Battle), Snapshot(replay.State.Battle));
            Assert.Equal(Serialize(first.Events), Serialize(replay.Events));
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    [Fact]
    public void ACT_first_reselects_from_actual_results_including_a_knockback_branch_change_before_settlement()
    {
        // Each case chooses its explicit entry from known source facts. No query/probe retry is used as a router.
        var cases = new[] {
            new { Name = "Attack / in reach", Move = 3, Seed = 1u, Power = 2, BeforeOutside = false, AfterOutside = false,
                Target = new TilePosition(2,1,0), Final = new RandomState(1223573862,72), Revisions = 3ul },
            new { Name = "Stone push changes reach branch", Move = 0, Seed = 20u, Power = 1, BeforeOutside = false, AfterOutside = true,
                Target = new TilePosition(2,1,0), Final = new RandomState(713082951,38), Revisions = 2ul },
            new { Name = "Out-of-reach Stone / END", Move = 0, Seed = 1u, Power = 2, BeforeOutside = true, AfterOutside = true,
                Target = new TilePosition(4,1,0), Final = new RandomState(1116175964,34), Revisions = 2ul } };
        foreach (var example in cases)
        {
            var state = Ready(move: example.Move, target: example.Target, entryWord: example.Seed);
            string before = Snapshot(state.Battle);
            var request = new AttackThrowStoneAiSelectionRequest(FreshRequest(), Stone, new(Sword, example.Power));
            var configuration = SelectionConfiguration(Selection);
            var actual = example.BeforeOutside
                ? AttackThrowStoneAiActFirstExecution.StartOutOfReach(state, request, configuration, Dispatch).Execution.Transition
                : AttackThrowStoneAiActFirstExecution.StartInReach(state, request, configuration, Dispatch).Execution.Transition;
            Assert.True(actual.Accepted); Assert.Null(actual.State.PendingFacing);
            int hpAfterAct = actual.State.Battle.Combatants.Single(u => u.UnitId == B).Hp;
            var settled = Execute(actual.State, example.AfterOutside, example.Power);
            Assert.True(settled.Accepted, Serialize(settled.Failure));
            Assert.Equal(example.Final, settled.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(example.Revisions, settled.State.Revision - state.Revision);
            Assert.Equal(hpAfterAct, settled.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.Empty(settled.Events.OfType<AttackResolvedEvent>()); Assert.Empty(settled.Events.OfType<ThrowStoneResolvedEvent>());
            Assert.Single(settled.Events.OfType<FacingSettledEvent>()); Assert.Null(settled.State.PendingFacing);
            Assert.False(settled.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).HasTurn);
            Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, settled.State.Battle.TurnBattle.Turns.Ticks);
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    [Fact]
    public void Later_failure_preserves_the_published_command_and_terminal_ACT_never_manufactures_living_facing()
    {
        // Attack-first consumes42 draws; only22 remain, one short of its post-ACT decision's23 draws.
        var started = AttackThrowStoneAiActFirstExecution.StartInReach(Ready(count: ulong.MaxValue - 64),
            new(FreshRequest(), Stone, new(Sword, 2)), SelectionConfiguration(Selection), Dispatch);
        Assert.True(started.Accepted);
        string acted = Snapshot(started.State.Battle);
        foreach (int attempt in new[] { 1, 2 })
        {
            var failed = Execute(started.State);
            Assert.False(failed.Accepted); Assert.Same(started.State, failed.State); Assert.Empty(failed.Events);
            Assert.Equal(40, failed.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.True(failed.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).ActionTaken);
            Assert.Equal(acted, Snapshot(failed.State.Battle));
        }
        // Move-first commits its path; an actual seven-draw Stone cannot fit in the six remaining draws.
        var moved = AttackThrowStoneAiMoveFirstExecution.StartInReach(Ready(count: ulong.MaxValue - 42),
            new(FreshRequest(), Stone, new(Sword, 1)), SelectionConfiguration(Selection), Dispatch);
        Assert.True(moved.Accepted);
        var failedAct = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation);
        Assert.False(failedAct.Accepted); Assert.Same(moved.State, failedAct.State); Assert.Empty(failedAct.Events);
        Assert.Equal(moved.Path!.Tiles[^1], failedAct.State.Battle.TurnBattle.Battle.Units.Single(u => u.Id == A).Position);
        Assert.Equal(50, failedAct.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);

        var lethal = AttackThrowStoneAiOutOfReachActFirstTests.Start(Ready(move: 0, target: new(4, 1, 0), hp: 10));
        Assert.True(lethal.Accepted);
        var terminal = Execute(lethal.State, outside: true);
        Assert.False(terminal.Accepted); Assert.Same(lethal.State, terminal.State); Assert.Empty(terminal.Events);
        Assert.Equal(0, terminal.State.Battle.Combatants.Single(u => u.UnitId == B).Hp); Assert.Null(terminal.State.PendingFacing);
    }

    private sealed record Trace(FacingTurnState State, ImmutableArray<AttackResultEvent> Events);
    private static Trace MoveThenAct(FacingTurnState state, bool outside, bool second)
    {
        var actor = second ? B : A; var target = second ? A : B;
        var request = new AttackThrowStoneAiSelectionRequest(FreshRequest(second), Stone, new(Sword, 1));
        var configuration = SelectionConfiguration(Selection);
        // The actual continuation belongs to the exact returned session, not to serialized diagnostic data.
        FacingTurnState movedState;
        FacingMoveContinuation? continuation;
        ImmutableArray<AttackResultEvent> moveEvents;
        if (outside)
        {
            var moved = AttackThrowStoneAiMoveFirstExecution.StartOutOfReach(state, request, configuration, Dispatch);
            Assert.True(moved.Accepted, Serialize(moved.Failure));
            movedState = moved.State; continuation = moved.Continuation; moveEvents = moved.Events;
        }
        else
        {
            var moved = AttackThrowStoneAiMoveFirstExecution.StartInReach(state, request, configuration, Dispatch);
            Assert.True(moved.Accepted, Serialize(moved.Failure));
            movedState = moved.State; continuation = moved.Continuation; moveEvents = moved.Events;
        }
        var actual = FacingMoveDecisionBattle.Continue(movedState, actor, continuation);
        Assert.True(actual.Accepted); Assert.NotNull(actual.State.PendingFacing);
        var faced = FacingTurnBattle.FinishFacing(actual.State, actor, actual.State.PendingFacing, new FacingChoice.ChooseClosedEnemy(target));
        Assert.True(faced.Accepted);
        return new(faced.State, moveEvents.AddRange(actual.Events).AddRange(faced.Events));
    }
}
