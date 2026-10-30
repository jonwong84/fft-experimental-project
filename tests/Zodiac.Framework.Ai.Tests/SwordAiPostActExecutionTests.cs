using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiPostActExecutionTests
{
    [Fact]
    public void Real_hit_and_miss_commit_one_fresh_decision_path_and_destination_facing_without_another_Attack()
    {
        foreach (var (evade, word, count, hp) in new[] { (0, 3834121963u, 37ul, 25), (89, 3776134388u, 42ul, 50) })
        {
            var state = AfterAct(evade: evade); var before = Snapshot(state.Battle); var result = Execute(state); var o = Success(result);
            Assert.Equal(new FftRandomState(word, count), o.DecisionRng);
            Assert.Equal(o.DecisionRng, result.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(o.DecisionRng, result.State.Battle.TurnBattle.Battle.RngState);
            Assert.Equal(SwordAiDispatchKind.Move, o.Dispatch.Kind); Assert.Equal(new SwordAiSavedChoices(A, false, true), o.Dispatch.RecordedChoices);
            var path = Assert.IsType<FlatMovementPath>(result.Path);
            Assert.Equal(new[] { new TilePosition(2, 1, 0), new(2, 2, 0) }, path.Tiles); Assert.Equal(BattleFacing.North, path.FinalHeading);
            Assert.Equal(BattleFacing.South, result.State.Battle.Combatants.Single(u => u.UnitId == A).Facing);
            Assert.Equal(state.Revision + 2, result.State.Revision); Assert.Null(result.State.PendingFacing);
            var turns = result.State.Battle.TurnBattle.Turns;
            var actor = turns.Units.Single(u => u.Id == A);
            Assert.False(actor.HasTurn); Assert.True(actor.MovementTaken); Assert.True(actor.ActionTaken); Assert.Equal(15, actor.Ct);
            Assert.Equal(95, turns.Units.Single(u => u.Id == B).Ct); // Never adopt the hypothetical opponent's clock.
            Assert.Equal(hp, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, turns.Ticks);
            Assert.Single(result.Events.OfType<VoluntaryPathCompletedEvent>()); Assert.Single(result.Events.OfType<FacingSettledEvent>());
            Assert.Empty(result.Events.OfType<AttackResolvedEvent>()); Assert.Equal(8, o.Selection.Lottery.Length);
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    [Fact]
    public void Same_tile_END_refunds_only_Move_and_out_of_reach_after_real_knockback_uses_the_matching_branch()
    {
        foreach (bool push in new[] { false, true })
        {
            // Historical push seed601690112 advances to word57 in the real upkeep before this Attack.
            var ready = Ready(evade: push ? 0 : 89, entryWord: push ? 57u : 1u,
                move: false, origin: new(2, 1, 0), movePoints: 0);
            var acted = FacingTurnBattle.Attack(ready, new(A, B), new(Weapon, 1)); Assert.True(acted.Accepted);
            var state = acted.State; var before = Snapshot(state.Battle);
            Assert.Equal(new TilePosition(push ? 0 : 1, 1, 0), state.Battle.TurnBattle.Battle.Units.Single(u => u.Id == B).Position);
            var result = Execute(state, outOfReach: push); var o = Success(result);
            Assert.Equal(SwordAiDispatchKind.EndTurn, o.Dispatch.Kind); Assert.Null(result.Path);
            Assert.Equal(new TilePosition(2, 1, 0), o.Selection.Plan.Destination);
            Assert.Equal(push, o.Selection.Scenarios.OutOfReach is not null);
            Assert.Equal(state.Battle.TurnBattle.Turns.RngState.DrawCount + (push ? 11ul : 10ul), o.DecisionRng.DrawCount);
            Assert.Equal(35, result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
            Assert.Equal(BattleFacing.West, result.State.Battle.Combatants.Single(u => u.UnitId == A).Facing);
            Assert.Equal(state.Revision + 1, result.State.Revision); Assert.Null(result.State.PendingFacing);
            Assert.Empty(result.Events.OfType<VoluntaryPathCompletedEvent>()); Assert.Empty(result.Events.OfType<AttackResolvedEvent>());
            Assert.Single(result.Events.OfType<FacingSettledEvent>()); Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    [Fact]
    public void Exact_post_ACT_draw_budget_succeeds_and_last_roll_failure_cannot_rewind_the_published_Attack()
    {
        foreach (var (evade, priorCost) in new[] { (0, 17ul), (89, 22ul) })
        {
            var state = AfterAct(evade: evade, count: ulong.MaxValue - priorCost - 13);
            var fits = Execute(state); Success(fits); Assert.Equal(ulong.MaxValue, fits.State.Battle.TurnBattle.Turns.RngState.DrawCount);
            state = AfterAct(evade: evade, count: ulong.MaxValue - priorCost - 12); var before = Snapshot(state.Battle);
            var failed = Execute(state); Atomic(state, failed, SwordAiPostActExecutionRejection.SelectionRejected);
            Assert.Equal(7, failed.Failure!.SelectionFailure!.LotteryFailure!.LotteryIndex);
            Assert.Equal(evade == 0 ? 25 : 50, state.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.True(state.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).ActionTaken);
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Choice_source_branch_and_terminal_guards_remain_explicit_without_automatic_routing()
    {
        var state = AfterAct();
        Atomic(state, Execute(state, outOfReach: true), SwordAiPostActExecutionRejection.SelectionRejected);
        Atomic(state, Execute(state, selection: new()), SwordAiPostActExecutionRejection.SelectionRejected);
        Atomic(state, Execute(state, refresh: Refresh with { FreshRefreshEntry = false }), SwordAiPostActExecutionRejection.SelectionRejected);
        Atomic(state, Execute(state, dispatch: new()), SwordAiPostActExecutionRejection.DispatchRejected);
        Atomic(state, Execute(state, second: true), SwordAiPostActExecutionRejection.BoundaryRejected);
        var ready = Ready(move: false); Atomic(ready, Execute(ready), SwordAiPostActExecutionRejection.UnavailableChoices);
        var ko = AfterAct(power: 10); Atomic(ko, Execute(ko), SwordAiPostActExecutionRejection.SelectionRejected);
        Assert.Equal(0, ko.Battle.Combatants.Single(u => u.UnitId == B).Hp); Assert.Null(ko.PendingFacing);
        var ended = Execute(state); Success(ended);
        Atomic(ended.State, Execute(ended.State), SwordAiPostActExecutionRejection.BoundaryRejected);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actors_orders_and_actual_Critical_or_knockback_replay_without_changing_the_input_session()
    {
        foreach (bool second in new[] { false, true })
        foreach (var (power, seed) in new[] { (8, 1u), (1, 3u) })
        {
            var state = AfterAct(power: power, entryWord: seed, second: second); var before = Snapshot(state.Battle);
            var result = Execute(state, second: second); Success(result);
            var replay = Execute(state, second: second); Success(replay);
            var reordered = Execute(AfterAct(power: power, entryWord: seed, second: second, reverse: true), second: second); Success(reordered);
            Assert.Equal(Snapshot(result.State.Battle), Snapshot(replay.State.Battle));
            Assert.Equal(Snapshot(result.State.Battle), Snapshot(reordered.State.Battle));
            Assert.Equal(Serialize(result.Outcome), Serialize(reordered.Outcome)); Assert.Equal(Serialize(result.Events), Serialize(reordered.Events));
            var target = second ? A : B;
            Assert.Equal(state.Battle.Combatants.Single(u => u.UnitId == target).Hp, result.State.Battle.Combatants.Single(u => u.UnitId == target).Hp);
            Assert.Equal(before, Snapshot(state.Battle)); Assert.Null(result.State.PendingFacing);
        }
    }

    private static SwordAiPostActExecutionResult Execute(FacingTurnState state, bool outOfReach = false,
        bool second = false, SwordAiRefreshProfile? refresh = null, SwordAiPostActSelectionProfile? selection = null,
        SwordAiCommandDispatchProfile? dispatch = null) => outOfReach
        ? SwordAiPostActExecution.ExecuteOutOfReach(state, Request(second), Candidate, Score, refresh ?? Refresh, Preparation,
            SwordAiScenarioTeam.NonBlue, new(true, true, true), selection ?? new(true), dispatch ?? new(true, true, true, true))
        : SwordAiPostActExecution.ExecuteInReach(state, Request(second), Candidate, Score, refresh ?? Refresh, Preparation,
            SwordAiScenarioTeam.NonBlue, new(true, true, true), selection ?? new(true), dispatch ?? new(true, true, true, true));
    private static SwordAiPostActExecutionOutcome Success(SwordAiPostActExecutionResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); return Assert.IsType<SwordAiPostActExecutionOutcome>(result.Outcome); }
    private static void Atomic(FacingTurnState state, SwordAiPostActExecutionResult result, SwordAiPostActExecutionRejection reason)
    { Assert.False(result.Accepted); Assert.Equal(reason, result.Failure!.Reason); Assert.Same(state, result.State);
        Assert.Empty(result.Events); Assert.Null(result.Outcome); Assert.Null(result.Path); }
}
