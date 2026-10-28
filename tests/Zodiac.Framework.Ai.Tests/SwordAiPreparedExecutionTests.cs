using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiPreparedExecutionTests
{
    private static readonly SwordAiPreparedSelectionProfile Selection = new(true, true, true, true, true, true);
    private static readonly SwordAiWaitFacingProfile Wait = new(true, true);
    private static readonly SwordAiWaitSettlementProfile End = new(true);

    [Fact]
    public void Fresh_prefix_and_real_miss_preserve_projected_vs_actual_results_and_one_completion_receipt()
    {
        var state = Ready(); var before = Snapshot(state.Battle); var result = Execute(state); var o = Success(result);
        Assert.NotNull(o.Preparation); Assert.Equal(16838u, Assert.Single(o.Preparation.Refresh.UpkeepPicks).RawRoll);
        Assert.Equal(new FftRandomState(2111915288, 22), o.DecisionRng);
        Assert.Equal(8, o.Decision.Selection.Lottery.Length);
        Assert.Equal(new FftRandomState(3794901700, 26), result.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(new AttackResolvedEvent(A, B, false, false, 0), Assert.Single(result.Events.OfType<AttackResolvedEvent>()));
        Assert.Equal(50, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.Equal(25, o.Decision.Selection.Plan.Attack!.Evaluation.Projection!.Outcome!.Units.Single(u => u.UnitId == B).Hp);
        Receipt(state, result.State); Assert.Equal(before, Snapshot(state.Battle));
        var finished = SwordAiWaitSettlement.Finish(result.State, A, B, result.State.PendingFacing, Candidate.Movement, Wait, End);
        Assert.True(finished.Accepted); Assert.Null(finished.State.PendingFacing);
        Assert.Equal(result.State.Revision + 1, finished.State.Revision);
        Assert.Equal(result.State.Battle.TurnBattle.Turns.RngState, finished.State.Battle.TurnBattle.Turns.RngState);
    }

    [Fact]
    public void Prepared_attack_and_zero_damage_fallback_share_actual_command_authority_and_single_refund()
    {
        var state = Ready(evade: 0); var attack = Execute(state); var o = Success(attack);
        Assert.Equal(SwordAiExecutedCommand.Attack, o.Command); Receipt(state, attack.State);
        Assert.Equal(new FftRandomState(2633739833, 15), o.DecisionRng);
        Assert.True(Assert.Single(attack.Events.OfType<AttackResolvedEvent>()).Hit);
        Assert.Equal(15, attack.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
        var wait = Execute(state, power: 0); var fallback = Success(wait);
        Assert.Equal(SwordAiExecutedCommand.Wait, fallback.Command); Assert.Null(fallback.Attack);
        Assert.Null(wait.State.PendingFacing); Assert.Equal(state.Revision + 1, wait.State.Revision);
        Assert.Empty(wait.Events.OfType<AttackResolvedEvent>());
        Assert.Equal(35, wait.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
        Assert.Equal(fallback.DecisionRng, wait.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, wait.State.Battle.TurnBattle.Turns.Ticks);
    }

    [Fact]
    public void Actual_Attack_budget_exhaustion_rolls_back_successful_preparation_and_selection()
    {
        var fits = Ready(count: ulong.MaxValue - 19); var accepted = Execute(fits); Success(accepted);
        Assert.Equal(ulong.MaxValue, accepted.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        var state = Ready(count: ulong.MaxValue - 18); var before = Snapshot(state.Battle);
        var failure = Atomic(state, Execute(state), SwordAiExecutionRejection.CommandRejected);
        Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, failure.CommandFailure!.AttackRejection);
        Assert.Null(failure.PreparedSelectionFailure); Assert.Equal(before, Snapshot(state.Battle));
        // A zero-power scored candidate still consumes its formula/clock work, then all eight fallback rolls.
        var waitState = Ready(evade: 0, count: ulong.MaxValue - 15);
        var wait = Execute(waitState, power: 0); Success(wait);
        Assert.Equal(ulong.MaxValue, wait.State.Battle.TurnBattle.Turns.RngState.DrawCount);
    }

    [Theory] [InlineData(5, 45, 5)] [InlineData(6, 54, 0)]
    public void Fresh_prefix_critical_push_and_lethal_results_bind_the_actual_settled_receipt(int power, int damage, int hp)
    {
        var state = Ready(evade: 0, entryWord: 8); var result = Execute(state, power: power); Success(result);
        Assert.Equal(new AttackResolvedEvent(A, B, true, true, damage), Assert.Single(result.Events.OfType<AttackResolvedEvent>()));
        Assert.Equal(hp, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.Equal(22ul, result.State.Battle.TurnBattle.Turns.RngState.DrawCount); Receipt(state, result.State);
        if (hp > 0)
        {
            Assert.Single(result.Events.OfType<SwordKnockbackEvent>());
            Assert.Equal(new TilePosition(0, 1, 0), result.State.Battle.TurnBattle.Battle.Units.Single(u => u.Id == B).Position);
        }
        else
        {
            Assert.Single(result.Events.OfType<AttackKnockedOutEvent>());
            // Lethal Attack remains committed; the existing two-living-opponent facing query is still bounded.
            var finish = SwordAiWaitSettlement.Finish(result.State, A, B, result.State.PendingFacing, Candidate.Movement, Wait, End);
            Assert.False(finish.Accepted); Assert.Same(result.State, finish.State);
        }
    }

    [Fact]
    public void Fresh_prefix_failures_and_session_authority_reject_without_exposing_partial_diagnostics()
    {
        foreach (int available in new[] { 0, 1, 14 })
        {
            var state = Ready(count: ulong.MaxValue - (ulong)available);
            var failure = Atomic(state, Execute(state), SwordAiExecutionRejection.SelectionRejected);
            Assert.NotNull(failure.PreparedSelectionFailure); Assert.Null(failure.SelectionFailure);
        }
        var ready = Ready();
        Atomic(ready, Execute(ready, preparation: new()), SwordAiExecutionRejection.SelectionRejected);
        Atomic(ready, Execute(ready, wait: new()), SwordAiExecutionRejection.UnsupportedContext);
        Atomic(ready, Execute(ready, request: Request(second: true)), SwordAiExecutionRejection.AuthorityRejected);
        var pending = Execute(ready).State;
        Assert.Equal(FacingTurnRejection.FacingPending,
            Atomic(pending, Execute(pending), SwordAiExecutionRejection.AuthorityRejected).AuthorityRejection);
        var max = new FacingTurnState(ready.Battle, ready.Profile, ulong.MaxValue, null, ready.Lineage);
        Assert.Equal(FacingTurnRejection.RevisionExhausted,
            Atomic(max, Execute(max), SwordAiExecutionRejection.AuthorityRejected).AuthorityRejection);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_order_and_replay_preserve_receipt_lineage_and_input_state()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = Ready(second: second); var before = Snapshot(state.Battle); var request = Request(second);
            var first = Execute(state, request: request); var replay = Execute(state, request: request);
            var other = Execute(Ready(second: second, reverse: true), request: Request(second, reverse: true));
            Success(first); Success(replay); Success(other);
            Assert.Equal(Serialize(first.Outcome), Serialize(other.Outcome));
            Assert.Equal(Snapshot(first.State.Battle), Snapshot(replay.State.Battle));
            Assert.Equal(Snapshot(first.State.Battle), Snapshot(other.State.Battle));
            Assert.Equal(first.Events.ToArray(), other.Events.ToArray()); Assert.Equal(before, Snapshot(state.Battle));
            Assert.Equal(FacingTurnRejection.StaleCompletion,
                FacingTurnBattle.FinishFacing(replay.State, request.ActorId, first.State.PendingFacing, new FacingChoice.KeepCurrent()).FacingRejection);
        }
    }

    private static SwordAiExecutionResult Execute(FacingTurnState state, int power = 5, SwordAiRefreshRequest? request = null,
        SwordAiPreparationProfile? preparation = null, SwordAiWaitFacingProfile? wait = null) =>
        SwordAiExecution.ExecutePreparedAlreadyMoved(state, request ?? Request(), new(Weapon, power), Candidate, Score,
            Refresh, preparation ?? Preparation, SwordAiScenarioTeam.NonBlue, Selection, wait ?? Wait, End);
    private static SwordAiExecutionOutcome Success(SwordAiExecutionResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); return Assert.IsType<SwordAiExecutionOutcome>(result.Outcome); }
    private static SwordAiExecutionFailure Atomic(FacingTurnState state, SwordAiExecutionResult result, SwordAiExecutionRejection reason)
    {
        Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events); Assert.Null(result.Outcome);
        Assert.Equal(reason, result.Failure!.Reason); return result.Failure;
    }
    private static void Receipt(FacingTurnState before, FacingTurnState after)
    {
        Assert.Equal(before.Revision + 1, after.Revision); var receipt = Assert.IsType<FacingCompletionReceipt>(after.PendingFacing);
        Assert.Same(before.Lineage, receipt.Lineage); Assert.Same(after.Battle, receipt.ExpectedBattle); Assert.Equal(after.Revision, receipt.Revision);
        Assert.Equal(before.Battle.TurnBattle.Turns.Ticks, after.Battle.TurnBattle.Turns.Ticks);
        Assert.Equal(after.Battle.TurnBattle.Battle.RngState, after.Battle.TurnBattle.Turns.RngState);
    }
}
