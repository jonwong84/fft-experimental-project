using System.Collections.Immutable;
using Xunit;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiMoveFirstExecutionTests;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiActFirstExecutionTests
{
    private static readonly AttackThrowStoneAiPreparedSelectionProfile Selection = new(true, true, true, true, true);
    private static readonly SwordAiCommandDispatchProfile Dispatch = new(true, true, true, true);

    [Fact]
    public void Fresh_Attack_executes_at_original_origin_and_diagnostic_D1_grants_no_future_Move()
    {
        var state = Ready(); string before = Snapshot(state.Battle); var result = Start(state, power: 2); var o = Success(result);
        Assert.Equal(new FftRandomState(3397090333, 43), o.DecisionRng);
        Assert.Equal(AttackThrowStoneAiPlanKind.Attack, o.Decision.Selection.Plan.Kind); Assert.True(o.Decision.Selection.Plan.ActBeforeMove);
        Assert.Equal(new TilePosition(1, 4, 0), o.Decision.Selection.Plan.Destination);
        Assert.Equal(new AttackResolvedEvent(A, B, true, false, 10), Assert.Single(result.Events.OfType<AttackResolvedEvent>()));
        Assert.Empty(result.Events.OfType<ThrowStoneResolvedEvent>());
        Assert.Equal(new FftRandomState(995405759, 49), result.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(40, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp); OpenMove(state, result.State);
        Assert.Equal(Origin, Position(result.State, A)); Assert.NotEqual(o.Decision.Selection.Plan.Destination, Position(result.State, A));
        var p = Profile(); var pure = AttackThrowStoneAiPreparedSelection.Evaluate(state.Battle, FreshRequest(), Stone, new(Sword, 2),
            p.Attack, p.ThrowStone, p.Score, new(true, true, true), new(true, true, true), Selection, SwordAiScenarioTeam.NonBlue,
            state.Battle.TurnBattle.Turns.RngState);
        Assert.True(pure.Accepted); Assert.Equal(Serialize(pure.Outcome), Serialize(o.Decision)); Assert.Equal(pure.RngState, o.DecisionRng);
        Assert.Equal(FacingTurnRejection.MissingCompletion,
            FacingTurnBattle.FinishFacing(result.State, A, null, new FacingChoice.KeepCurrent()).FacingRejection);
        Assert.Empty(result.Events.OfType<VoluntaryPathCompletedEvent>()); Assert.Empty(result.Events.OfType<FacingSettledEvent>());
        Assert.Equal(before, Snapshot(state.Battle));
    }

    [Fact]
    public void ACT_first_Stone_uses_real_damage_push_or_miss_and_keeps_zero_Move_unspent_without_auto_END()
    {
        foreach (var (seed, evade, word, count, hit, damage, push, finalWord, finalCount) in new[] {
            (1u, 0, 595337866u, 20ul, true, 10, false, 3823029677u, 27ul),
            (20u, 0, 2711922929u, 20ul, true, 10, true, 2928661168u, 27ul),
            (1u, 75, 3823029677u, 27ul, false, 0, false, 675780521u, 31ul) })
        {
            var state = Ready(move: 0, evade: evade, entryWord: seed); string before = Snapshot(state.Battle);
            var result = Start(state); var o = Success(result); Assert.Equal(AttackThrowStoneAiPlanKind.ThrowStone, o.Decision.Selection.Plan.Kind);
            Assert.Equal(new FftRandomState(word, count), o.DecisionRng);
            Assert.Equal(new ThrowStoneResolvedEvent(A, Stone, B, hit, damage, push), Assert.Single(result.Events.OfType<ThrowStoneResolvedEvent>()));
            Assert.Empty(result.Events.OfType<AttackResolvedEvent>());
            Assert.Equal(new FftRandomState(finalWord, finalCount), result.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(50 - damage, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.Equal(new TilePosition(push ? 3 : 2, 1, 0), Position(result.State, B)); OpenMove(state, result.State);
            Assert.Equal(Origin, Position(result.State, A)); Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    [Fact]
    public void Real_Stone_KO_publishes_settled_effects_but_no_facing_receipt_or_post_ACT_selection()
    {
        var state = Ready(move: 0, hp: 10); var result = Start(state); var o = Success(result);
        Assert.Equal(AttackThrowStoneAiPlanKind.ThrowStone, o.Decision.Selection.Plan.Kind);
        Assert.Equal(new FftRandomState(2937909499, 21), o.DecisionRng);
        Assert.Equal(new ThrowStoneResolvedEvent(A, Stone, B, true, 10, false), Assert.Single(result.Events.OfType<ThrowStoneResolvedEvent>()));
        Assert.Equal(AttackUnitStatus.Dead, result.State.Battle.Combatants.Single(u => u.UnitId == B).Status);
        Assert.Equal(1, result.State.Battle.Combatants.Single(u => u.UnitId == B).KoCount);
        Assert.Equal(new FftRandomState(3914758370, 28), result.State.Battle.TurnBattle.Turns.RngState);
        OpenMove(state, result.State); Assert.Equal(new TilePosition(2, 1, 0), Position(result.State, B));
        Atomic(result.State, Start(result.State), AttackThrowStoneAiActFirstRejection.UnavailableChoices);
    }

    [Fact]
    public void Exact_actual_budgets_fit_and_short_ACT_or_earlier_selection_failure_rolls_back_the_entire_transaction()
    {
        foreach (var (move, power, hp, evade, seed, total) in new[] {
            (3, 2, 50, 0, 1u, 42ul), (0, 1, 50, 0, 1u, 20ul), (0, 1, 50, 0, 20u, 20ul),
            (0, 1, 50, 75, 1u, 24ul), (0, 1, 10, 0, 1u, 21ul) })
        {
            var state = Ready(move: move, hp: hp, evade: evade, entryWord: seed, count: ulong.MaxValue - total);
            var exact = Start(state, power: power); Success(exact); Assert.Equal(ulong.MaxValue, exact.State.Battle.TurnBattle.Turns.RngState.DrawCount);
            var shortState = Ready(move: move, hp: hp, evade: evade, entryWord: seed, count: ulong.MaxValue - total + 1);
            string before = Snapshot(shortState.Battle); var failed = Start(shortState, power: power);
            Assert.Equal(FacingDecisionRejection.CommandRejected, Atomic(shortState, failed, AttackThrowStoneAiActFirstRejection.BoundaryRejected).BoundaryReason);
            Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, failed.Execution.Transition.AttackRejection);
            Assert.Equal(before, Snapshot(shortState.Battle));
        }
        foreach (ulong remaining in new[] { 0ul, 1ul, 35ul })
        {
            var state = Ready(count: ulong.MaxValue - remaining);
            Atomic(state, Start(state, power: 2), AttackThrowStoneAiActFirstRejection.SelectionRejected);
        }
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Move_first_empty_plans_wrong_branch_context_and_spent_choices_reject_without_partial_decision()
    {
        var state = Ready(); Atomic(state, Start(state), AttackThrowStoneAiActFirstRejection.ActFirstRequired);
        var empty = Ready(left: true, evade: 100); Atomic(empty, Start(empty), AttackThrowStoneAiActFirstRejection.ActFirstRequired);
        foreach (var dispatch in new[] { Dispatch with { InitializedActorContext = false }, Dispatch with { PresentCompactRecords = false },
            Dispatch with { CanonicalCoordinates = false }, Dispatch with { OrdinaryContinuation = false } })
            Atomic(state, Start(state, dispatch: dispatch), AttackThrowStoneAiActFirstRejection.UnsupportedDispatchContext);
        var far = Ready(target: new(6, 1, 0));
        Assert.Equal(AttackThrowStoneAiScenarioRejection.OutOfReach,
            Atomic(far, Start(far), AttackThrowStoneAiActFirstRejection.SelectionRejected).SelectionFailure!.ScenarioFailure!.Reason);
        Atomic(state, Start(state, selection: Selection with { NoDefendSupport = false }), AttackThrowStoneAiActFirstRejection.SelectionRejected);
        Atomic(state, Start(state, team: SwordAiScenarioTeam.Blue), AttackThrowStoneAiActFirstRejection.SelectionRejected);
        var moved = FacingTurnPathMovement.MoveAlongPath(state, new(A, new(0, 1, 0)), new(Ground, A, B)); Assert.True(moved.Accepted);
        Atomic(moved.State, Start(moved.State), AttackThrowStoneAiActFirstRejection.UnavailableChoices);
        var acted = Start(state, power: 2); Success(acted);
        Atomic(acted.State, Start(acted.State), AttackThrowStoneAiActFirstRejection.UnavailableChoices);
        var wrongActor = Start(state, request: FreshRequest(second: true));
        Assert.Equal(FacingDecisionRejection.AuthorityRejected, Atomic(state, wrongActor, AttackThrowStoneAiActFirstRejection.BoundaryRejected).BoundaryReason);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_families_actor_identities_and_collection_policy_orders_replay_without_live_input_changes()
    {
        foreach (bool second in new[] { false, true })
        foreach (var (move, power) in new[] { (3, 2), (0, 1) })
        {
            var state = Ready(move: move, second: second); string before = Snapshot(state.Battle); var request = FreshRequest(second);
            var first = Start(state, power: power, request: request); Success(first);
            var replay = Start(state, power: power, request: request); Success(replay);
            var reversed = Start(Ready(move: move, second: second, reverse: true), power: power,
                request: request with { Policies = request.Policies.Reverse().ToImmutableArray() }); Success(reversed);
            Assert.Equal(Serialize(first.Outcome), Serialize(reversed.Outcome)); Assert.Equal(Serialize(first.Outcome), Serialize(replay.Outcome));
            Assert.Equal(Snapshot(first.State.Battle), Snapshot(reversed.State.Battle)); Assert.Equal(Snapshot(first.State.Battle), Snapshot(replay.State.Battle));
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    private static AttackThrowStoneAiActFirstResult Start(FacingTurnState state, int power = 1, SwordAiRefreshRequest? request = null,
        SwordAiCommandDispatchProfile? dispatch = null, AttackThrowStoneAiPreparedSelectionProfile? selection = null,
        SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue)
    {
        var p = Profile(); return AttackThrowStoneAiActFirstExecution.StartInReach(state, request ?? FreshRequest(), Stone, new(Sword, power),
            p.Attack, p.ThrowStone, p.Score, new(true, true, true), new(true, true, true), selection ?? Selection, team, dispatch ?? Dispatch);
    }
    private static AttackThrowStoneAiActFirstOutcome Success(AttackThrowStoneAiActFirstResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); Assert.Null(result.Execution.Path);
        return Assert.IsType<AttackThrowStoneAiActFirstOutcome>(result.Outcome); }
    private static AttackThrowStoneAiActFirstFailure Atomic(FacingTurnState state, AttackThrowStoneAiActFirstResult result, AttackThrowStoneAiActFirstRejection reason)
    { Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events); Assert.Null(result.Execution.Path);
        Assert.Null(result.Outcome); Assert.Equal(reason, result.Failure!.Reason); return result.Failure; }
    private static TilePosition Position(FacingTurnState state, BattleUnitId id) => state.Battle.TurnBattle.Battle.Units.Single(u => u.Id == id).Position;
    private static void OpenMove(FacingTurnState before, FacingTurnState after)
    {
        var actor = before.Battle.TurnBattle.Turns.ActiveUnitId!.Value; var turn = after.Battle.TurnBattle.Turns.Units.Single(u => u.Id == actor);
        Assert.True(turn.ActionTaken); Assert.False(turn.MovementTaken); Assert.True(turn.HasTurn); Assert.Equal(15, turn.Ct);
        Assert.Equal(actor, after.Battle.TurnBattle.Turns.ActiveUnitId); Assert.Null(after.PendingFacing); Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.Battle.TurnBattle.Turns.Ticks, after.Battle.TurnBattle.Turns.Ticks);
        Assert.Equal(after.Battle.TurnBattle.Turns.RngState, after.Battle.TurnBattle.Battle.RngState);
        foreach (var body in before.Battle.TurnBattle.Battle.Units)
            if (body.Id == actor) Assert.Equal(body.Position, after.Battle.TurnBattle.Battle.Units.Single(u => u.Id == actor).Position);
    }
}
