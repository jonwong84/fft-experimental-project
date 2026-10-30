using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiPreparedOutOfReachTests
{
    private static readonly SwordAiPreparedSelectionProfile Selection = new(true, true, true, true, true, true);
    private static readonly SwordAiWaitFacingProfile Wait = new(true, true);
    private static readonly SwordAiWaitSettlementProfile End = new(true);

    [Theory] [InlineData(1u, 10113u, 2, 180171308u)] [InlineData(20u, 12502u, 1, 3368358523u)]
    public void Actual_Wait_retains_post_score_adjustment_and_eight_roll_fallback_without_attack_or_movement(
        uint seed, uint raw, int reach, uint finalWord)
    {
        var state = Ready(entryWord: seed, outOfReach: true); var before = Snapshot(state.Battle);
        var result = Execute(state); var o = Success(result);
        var detail = Assert.IsType<SwordAiOutOfReachSelectionOutcome>(o.OutOfReach);
        Assert.Equal(raw, detail.MovementScenario.ReachAdjustment!.Raw);
        Assert.Equal(reach, detail.MovementScenario.EffectiveReach);
        Assert.Equal(3, detail.MovementScenario.MovementScenario.TargetDistance);
        Assert.Single(o.Decision.MovementScenario.Movement); // S is just the already-moved actor origin.
        Assert.Empty(detail.Selection.Composition.Scan.Outcome!.Candidates);
        Assert.All(detail.Selection.Composition.RankedActions, entry => Assert.Null(entry));
        Assert.Equal(8, detail.Selection.Lottery.Length); Assert.All(detail.Selection.Lottery, roll => Assert.Equal(0, roll.HitPercent));
        Assert.Equal(10ul, detail.Selection.LotteryEntryRng.DrawCount);
        Assert.Equal(new FftRandomState(finalWord, 18), o.DecisionRng);
        Assert.Equal(o.DecisionRng, result.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(o.DecisionRng, result.State.Battle.TurnBattle.Battle.RngState);
        Assert.Equal(SwordAiExecutedCommand.Wait, o.Command); Assert.Null(o.Attack); Assert.Null(result.State.PendingFacing);
        Assert.Equal(new TilePosition(4, 1, 0), detail.Selection.Plan.Destination);
        Assert.Equal(35, result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
        Assert.Equal(BattleFacing.West, result.State.Battle.Combatants.Single(u => u.UnitId == A).Facing);
        Assert.Equal(state.Revision + 1, result.State.Revision);
        Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, result.State.Battle.TurnBattle.Turns.Ticks);
        Assert.Empty(result.Events.OfType<AttackResolvedEvent>()); Assert.Empty(result.Events.OfType<SwordKnockbackEvent>());
        foreach (var unit in state.Battle.TurnBattle.Battle.Units)
            Assert.Equal(unit.Position, result.State.Battle.TurnBattle.Battle.Units.Single(u => u.Id == unit.Id).Position);
        Assert.Equal(before, Snapshot(state.Battle));
    }

    [Fact]
    public void Pure_query_owns_one_fresh_prefix_and_matches_explicit_stages_without_mutating_inputs()
    {
        var state = Ready(outOfReach: true); var before = Snapshot(state.Battle);
        var rng = state.Battle.TurnBattle.Turns.RngState;
        var pure = Query(state.Battle, Request(), rng); Assert.True(pure.Accepted);
        var prep = SwordAiPreparation.Evaluate(state.Battle, Request(), Candidate, Score, Refresh, Preparation, SwordAiScenarioTeam.NonBlue, rng);
        var derived = SwordAiDerivedSelection.EvaluateOutOfReach(state.Battle, new(Weapon, 5), Candidate, Score,
            new(A, B, true), new(true, true, true), new(true, true, true, true, true, true, true, true), new(true, true, true),
            SwordAiScenarioTeam.NonBlue, prep.RngState);
        Assert.Equal(derived.RngState, pure.RngState); Assert.Equal(Serialize(derived.Outcome), Serialize(pure.Outcome!.OutOfReach));
        var actual = Execute(state); Success(actual);
        Assert.Equal(pure.RngState, actual.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(before, Snapshot(state.Battle));
    }

    [Fact]
    public void Exact_eleven_draw_budget_fits_and_refresh_adjustment_and_lottery_exhaustion_roll_back()
    {
        var fits = Ready(count: ulong.MaxValue - 11, outOfReach: true); var result = Execute(fits); Success(result);
        Assert.Equal(ulong.MaxValue, result.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        foreach (int available in new[] { 0, 1, 2, 3, 10 })
        {
            var state = Ready(count: ulong.MaxValue - (ulong)available, outOfReach: true);
            var failure = Atomic(state, Execute(state), SwordAiExecutionRejection.SelectionRejected);
            Assert.NotNull(failure.PreparedSelectionFailure); Assert.Null(failure.CommandFailure);
            var rng = state.Battle.TurnBattle.Turns.RngState; var pure = Query(state.Battle, Request(), rng);
            Assert.False(pure.Accepted); Assert.Null(pure.Outcome); Assert.Equal(rng, pure.RngState);
        }
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Explicit_wrong_branch_unknown_team_and_pending_facing_reject_atomically()
    {
        var near = Ready(); Atomic(near, Execute(near), SwordAiExecutionRejection.SelectionRejected);
        var far = Ready(outOfReach: true);
        var wrong = SwordAiExecution.ExecutePreparedAlreadyMoved(far, Request(), new(Weapon, 5), Candidate, Score,
            Refresh, Preparation, SwordAiScenarioTeam.NonBlue, Selection, Wait, End);
        Atomic(far, wrong, SwordAiExecutionRejection.SelectionRejected);
        Atomic(far, Execute(far, team: SwordAiScenarioTeam.Blue), SwordAiExecutionRejection.SelectionRejected);
        Atomic(far, Execute(far, profile: Selection with { NoDefendSupport = false }), SwordAiExecutionRejection.SelectionRejected);
        var pending = SwordAiExecution.ExecutePreparedAlreadyMoved(near, Request(), new(Weapon, 5), Candidate, Score,
            Refresh, Preparation, SwordAiScenarioTeam.NonBlue, Selection, Wait, End).State;
        Assert.Equal(FacingTurnRejection.FacingPending,
            Atomic(pending, Execute(pending), SwordAiExecutionRejection.AuthorityRejected).AuthorityRejection);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Either_actor_and_collection_order_replay_the_complete_Wait_with_no_live_projection()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = Ready(second: second, outOfReach: true); var before = Snapshot(state.Battle); var request = Request(second);
            var first = Execute(state, request: request); var replay = Execute(state, request: request);
            var other = Execute(Ready(second: second, reverse: true, outOfReach: true), request: Request(second, reverse: true));
            Success(first); Success(replay); Success(other);
            Assert.Equal(Serialize(first.Outcome), Serialize(other.Outcome));
            Assert.Equal(Snapshot(first.State.Battle), Snapshot(other.State.Battle));
            Assert.Equal(Snapshot(first.State.Battle), Snapshot(replay.State.Battle));
            Assert.Equal(first.Events.ToArray(), other.Events.ToArray()); Assert.Equal(before, Snapshot(state.Battle));
        }
    }
    private static SwordAiPreparedSelectionResult Query(AttackBattleSnapshot state, SwordAiRefreshRequest request, FftRandomState rng) =>
        SwordAiPreparedSelection.EvaluateOutOfReach(state, request, new(Weapon, 5), Candidate, Score, Refresh, Preparation,
            SwordAiScenarioTeam.NonBlue, Selection, rng);
    private static SwordAiExecutionResult Execute(FacingTurnState state, SwordAiRefreshRequest? request = null,
        SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, SwordAiPreparedSelectionProfile? profile = null) =>
        SwordAiExecution.ExecutePreparedOutOfReachAlreadyMoved(state, request ?? Request(), new(Weapon, 5), Candidate, Score,
            Refresh, Preparation, team, profile ?? Selection, Wait, End);
    private static SwordAiExecutionOutcome Success(SwordAiExecutionResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); return Assert.IsType<SwordAiExecutionOutcome>(result.Outcome); }
    private static SwordAiExecutionFailure Atomic(FacingTurnState state, SwordAiExecutionResult result, SwordAiExecutionRejection reason)
    {
        Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events); Assert.Null(result.Outcome);
        Assert.Equal(reason, result.Failure!.Reason); return result.Failure;
    }
}
