using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiMoveFirstOutOfReachTests
{
    private static readonly SwordAiMovementScenarioProfile Scenario = new(true, true, true);
    private static readonly SwordAiDerivedSelectionProfile Derived = new(true, true, true, true, true, true, true, true);
    private static readonly SwordAiSelectionProfile Selection = new(true, true, true);
    private static readonly SwordAiExecutionProfile Execution = new(true);
    private static readonly SwordAiCommandDispatchProfile Dispatch = new(true, true, true, true);

    [Fact]
    public void Explicit_team_streams_move_then_Wait_with_no_reselection_or_actual_Attack()
    {
        foreach (var vector in new[] {
            (Team: SwordAiScenarioTeam.Blue, Seed: 1u, Word: 3210001534u, Count: 16ul, Raw: (uint?)null, Reach: 2),
            (Team: SwordAiScenarioTeam.NonBlue, Seed: 1u, Word: 267834847u, Count: 17ul, Raw: (uint?)5758, Reach: 2),
            (Team: SwordAiScenarioTeam.NonBlue, Seed: 6u, Word: 3146034828u, Count: 17ul, Raw: (uint?)25515, Reach: 1) })
        {
            var state = Ready(move: false, origin: new(4, 1, 0), entryWord: vector.Seed);
            var before = Snapshot(state.Battle); var moved = Start(state, vector.Team); var o = Success(moved);
            var detail = Assert.IsType<SwordAiOutOfReachSelectionOutcome>(o.OutOfReach);
            Assert.Equal(vector.Team, detail.MovementScenario.Team);
            Assert.Equal(vector.Raw, detail.MovementScenario.ReachAdjustment?.Raw);
            Assert.Equal(vector.Reach, detail.MovementScenario.EffectiveReach);
            Assert.Equal(vector.Reach == 1, detail.MovementScenario.AttackUsedDistanceFallback);
            Assert.Single(o.Decision.MovementScenario.Movement);
            Assert.Empty(o.Decision.Selection.Composition.Scan.Outcome!.Candidates);
            Assert.Equal(8, o.Decision.Selection.Lottery.Length);
            Assert.Equal(SwordAiSelectedPlanKind.MoveOnly, o.Decision.Selection.Plan.Kind);
            Assert.Equal(SwordAiWaitFacingHint.ChooseAtWait, o.Decision.Selection.Plan.WaitFacingHint);
            Assert.Equal(new FftRandomState(vector.Word, vector.Count), o.DecisionRng);
            Assert.Equal(new TilePosition(3, 1, 0), moved.Path!.Tiles[^1]);
            Assert.Equal(BattleFacing.West, moved.Path.FinalHeading);
            Assert.Equal(state.Revision + 1, moved.State.Revision);
            Assert.Equal(o.DecisionRng, moved.State.Battle.TurnBattle.Turns.RngState);
            var query = SwordAiDerivedSelection.EvaluateOutOfReach(state.Battle, new(Weapon, 5), Candidate, Score,
                new(A, B, true), Scenario, Derived, Selection, vector.Team, state.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(Serialize(query.Outcome), Serialize(detail)); Assert.Equal(query.RngState, o.DecisionRng);
            var next = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation);
            Assert.True(next.Accepted); Assert.Null(next.State.PendingFacing);
            Assert.Equal(moved.State.Revision + 1, next.State.Revision);
            Assert.Equal(o.DecisionRng, next.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(o.DecisionRng, next.State.Battle.TurnBattle.Battle.RngState);
            Assert.Equal(35, next.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
            Assert.Equal(BattleFacing.West, next.State.Battle.Combatants.Single(u => u.UnitId == A).Facing);
            Assert.All(next.State.Battle.Combatants, u => Assert.Equal(50, u.Hp));
            Assert.Empty(moved.Events.Concat(next.Events).OfType<AttackResolvedEvent>());
            Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, next.State.Battle.TurnBattle.Turns.Ticks);
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    [Fact]
    public void Exact_team_budgets_allow_Wait_at_MAX_and_short_prefixes_publish_nothing()
    {
        foreach (var (team, cost) in new[] { (SwordAiScenarioTeam.Blue, 9ul), (SwordAiScenarioTeam.NonBlue, 10ul) })
        {
            var moved = Start(Ready(move: false, origin: new(4, 1, 0), count: ulong.MaxValue - cost), team);
            Success(moved);
            var next = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation);
            Assert.True(next.Accepted); Assert.Equal(ulong.MaxValue, next.State.Battle.TurnBattle.Turns.RngState.DrawCount);
            foreach (var remaining in new[] { 0ul, 1ul, cost - 1 })
            {
                var state = Ready(move: false, origin: new(4, 1, 0), count: ulong.MaxValue - remaining);
                var rejected = Start(state, team); Atomic(state, rejected, SwordAiMoveFirstRejection.SelectionRejected);
                Assert.Equal(remaining >= (team == SwordAiScenarioTeam.Blue ? 1ul : 2ul)
                    ? SwordAiDerivedSelectionStage.Lottery : SwordAiDerivedSelectionStage.MovementScenario,
                    rejected.Failure!.SelectionStage);
            }
        }
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Explicit_branch_team_and_entry_certificates_reject_without_publishing_a_Move()
    {
        var state = Ready(move: false, origin: new(4, 1, 0));
        Atomic(state, Start(state, SwordAiScenarioTeam.Unknown), SwordAiMoveFirstRejection.SelectionRejected);
        Atomic(state, Start(state, (SwordAiScenarioTeam)99), SwordAiMoveFirstRejection.SelectionRejected);
        Atomic(state, Start(state, SwordAiScenarioTeam.NonBlue, new()), SwordAiMoveFirstRejection.UnsupportedContext);
        var wrongBranch = Ready(move: false);
        Atomic(wrongBranch, Start(wrongBranch, SwordAiScenarioTeam.NonBlue), SwordAiMoveFirstRejection.SelectionRejected);
        var inReach = SwordAiMoveFirstExecution.StartInReach(state, new(Weapon, 5), Candidate, Score, new(A, B, true),
            Scenario, Derived, Selection, Execution, Dispatch);
        Atomic(state, inReach, SwordAiMoveFirstRejection.SelectionRejected);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Identity_order_replay_and_failed_continuations_preserve_the_committed_Move()
    {
        foreach (bool second in new[] { false, true })
        {
            var actor = second ? B : A; var target = second ? A : B;
            var state = Ready(move: false, origin: new(4, 1, 0), second: second);
            var moved = Start(state, SwordAiScenarioTeam.NonBlue, second: second); Success(moved);
            var replay = Start(state, SwordAiScenarioTeam.NonBlue, second: second); Success(replay);
            var reversed = Start(Ready(move: false, origin: new(4, 1, 0), second: second, reverse: true),
                SwordAiScenarioTeam.NonBlue, second: second); Success(reversed);
            Assert.Equal(Serialize(moved.Outcome), Serialize(reversed.Outcome));
            Assert.Equal(Snapshot(moved.State.Battle), Snapshot(reversed.State.Battle));
            var failed = FacingMoveDecisionBattle.Continue(moved.State, target, moved.Continuation);
            Assert.Equal(FacingMoveRejection.WrongActor, failed.Rejection); Assert.Same(moved.State, failed.State); Assert.Empty(failed.Events);
            Assert.Equal(FacingMoveRejection.StaleContinuation, FacingMoveDecisionBattle.Continue(replay.State, actor, moved.Continuation).Rejection);
            var next = FacingMoveDecisionBattle.Continue(moved.State, actor, moved.Continuation); Assert.True(next.Accepted);
            Assert.Equal(Snapshot(next.State.Battle), Snapshot(FacingMoveDecisionBattle.Continue(moved.State, actor, moved.Continuation).State.Battle));
            var stale = FacingMoveDecisionBattle.Continue(next.State, actor, moved.Continuation);
            Assert.Equal(FacingMoveRejection.StaleContinuation, stale.Rejection); Assert.Same(next.State, stale.State); Assert.Empty(stale.Events);
        }
    }

    private static SwordAiMoveFirstResult Start(FacingTurnState state, SwordAiScenarioTeam team,
        SwordAiExecutionProfile? execution = null, bool second = false) => SwordAiMoveFirstExecution.StartOutOfReach(state,
            new(Weapon, 5), Candidate, Score, new(second ? B : A, second ? A : B, true), Scenario, Derived, Selection,
            execution ?? Execution, Dispatch, team);
    private static SwordAiMoveFirstOutcome Success(SwordAiMoveFirstResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); Assert.NotNull(result.Continuation); return Assert.IsType<SwordAiMoveFirstOutcome>(result.Outcome); }
    private static void Atomic(FacingTurnState state, SwordAiMoveFirstResult result, SwordAiMoveFirstRejection reason)
    { Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events); Assert.Null(result.Outcome);
        Assert.Null(result.Path); Assert.Null(result.Continuation); Assert.Equal(reason, result.Failure!.Reason); }
}
