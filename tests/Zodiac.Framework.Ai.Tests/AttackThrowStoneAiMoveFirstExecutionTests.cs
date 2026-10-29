using System.Collections.Immutable;
using Xunit;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiMoveFirstExecutionTests
{
    private static readonly AttackThrowStoneAiPreparedSelectionProfile Selection = new(true, true, true, true, true);
    private static readonly SwordAiCommandDispatchProfile Dispatch = new(true, true, true, true);

    [Fact]
    public void Fresh_Stone_selection_adopts_one_full_pipeline_then_real_Move_and_ACT_use_actual_damage_without_replanning()
    {
        var state = Ready(); string before = Snapshot(state.Battle); var moved = Start(state); var o = Success(moved);
        Assert.Equal(new FftRandomState(3397090333, 43), o.DecisionRng); Assert.Equal(o.DecisionRng, moved.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(o.DecisionRng, moved.State.Battle.TurnBattle.Battle.RngState);
        Assert.Equal(AttackThrowStoneAiPlanKind.ThrowStone, o.Decision.Selection.Plan.Kind);
        Assert.False(o.Decision.Selection.Plan.ActBeforeMove); Assert.Equal(new TilePosition(1, 4, 0), o.Move.Destination);
        Assert.Equal(new[] { new TilePosition(1, 1, 0), new(1, 2, 0), new(1, 3, 0), new(1, 4, 0) }, moved.Path!.Tiles.ToArray());
        Assert.Equal(BattleFacing.North, moved.Path.FinalHeading); Assert.Equal(state.Revision + 1, moved.State.Revision);
        Assert.Equal(50, moved.State.Battle.Combatants.Single(u => u.UnitId == B).Hp); Assert.Empty(moved.Events.OfType<ThrowStoneResolvedEvent>());
        var p = Profile(); var pure = AttackThrowStoneAiPreparedSelection.Evaluate(state.Battle, FreshRequest(), Stone, new(Sword, 1),
            p.Attack, p.ThrowStone, p.Score, new(true, true, true), new(true, true, true), Selection, SwordAiScenarioTeam.NonBlue,
            state.Battle.TurnBattle.Turns.RngState);
        Assert.True(pure.Accepted); Assert.Equal(Serialize(pure.Outcome), Serialize(o.Decision)); Assert.Equal(pure.RngState, o.DecisionRng);
        var act = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation); Assert.True(act.Accepted);
        Assert.Equal(new ThrowStoneResolvedEvent(A, Stone, B, true, 5, false), Assert.Single(act.Events.OfType<ThrowStoneResolvedEvent>()));
        Assert.Equal(new FftRandomState(2102736524, 50), act.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(45, act.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.Same(act.State.Battle, act.State.PendingFacing!.ExpectedBattle); Assert.Equal(state.Revision + 2, act.State.Revision);
        var faced = FacingTurnBattle.FinishFacing(act.State, A, act.State.PendingFacing, new FacingChoice.ChooseClosedEnemy(B));
        Assert.True(faced.Accepted); Assert.Equal(act.State.Battle.TurnBattle, faced.State.Battle.TurnBattle);
        Assert.Equal(before, Snapshot(state.Battle));
    }

    [Fact]
    public void Typed_Attack_and_empty_MoveOnly_plans_retain_commands_and_deferred_Wait_facing()
    {
        var attack = Start(Ready(left: true), power: 5); var a = Success(attack);
        Assert.Equal(AttackThrowStoneAiPlanKind.Attack, a.Decision.Selection.Plan.Kind);
        Assert.Equal(new TilePosition(2, 1, 0), a.Move.Destination); Assert.Equal(new FftRandomState(3791773014, 24), a.DecisionRng);
        var actual = FacingMoveDecisionBattle.Continue(attack.State, A, attack.Continuation); Assert.True(actual.Accepted);
        Assert.Single(actual.Events.OfType<AttackResolvedEvent>()); Assert.Empty(actual.Events.OfType<ThrowStoneResolvedEvent>());
        Assert.Equal(25, actual.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.Equal(new FftRandomState(1343201072, 30), actual.State.Battle.TurnBattle.Turns.RngState);
        var wait = Start(Ready(left: true, evade: 100)); var w = Success(wait);
        Assert.Equal(AttackThrowStoneAiPlanKind.MoveOnly, w.Decision.Selection.Plan.Kind); Assert.Equal(8, w.Decision.Selection.Lottery.Length);
        Assert.Equal(new TilePosition(3, 2, 0), w.Move.Destination); Assert.Equal(new FftRandomState(2473755763, 29), w.DecisionRng);
        var ended = FacingMoveDecisionBattle.Continue(wait.State, A, wait.Continuation); Assert.True(ended.Accepted);
        Assert.Equal(w.DecisionRng, ended.State.Battle.TurnBattle.Turns.RngState); Assert.Null(ended.State.PendingFacing);
        Assert.Equal(35, ended.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
        Assert.Empty(ended.Events.OfType<ThrowStoneResolvedEvent>()); Assert.Empty(ended.Events.OfType<AttackResolvedEvent>());
        Assert.Equal(FacingTurnBattle.DirectionToward(w.Move.Destination, new(1, 1, 0)), ended.State.Battle.Combatants.Single(u => u.UnitId == A).Facing);
    }

    [Fact]
    public void Predicted_KO_is_not_copied_to_real_Stone_damage_push_or_completion_receipt()
    {
        var moved = Start(Ready(hp: 10)); var o = Success(moved);
        Assert.Equal(AttackThrowStoneAiPlanKind.ThrowStone, o.Decision.Selection.Plan.Kind);
        var predicted = o.Decision.Selection.Composition.ThrowStone.Candidate!.Evaluation.Projection!.Outcome!;
        Assert.Equal(AttackUnitStatus.Dead, predicted.Units.Single(u => u.UnitId == B).Status);
        Assert.Equal(new FftRandomState(835903122, 44), o.DecisionRng);
        var real = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation); Assert.True(real.Accepted);
        Assert.Equal(new ThrowStoneResolvedEvent(A, Stone, B, true, 5, true), Assert.Single(real.Events.OfType<ThrowStoneResolvedEvent>()));
        Assert.Equal(5, real.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.NotEqual(AttackUnitStatus.Dead, real.State.Battle.Combatants.Single(u => u.UnitId == B).Status);
        Assert.Equal(new TilePosition(2, 0, 0), real.State.Battle.TurnBattle.Battle.Units.Single(u => u.Id == B).Position);
        Assert.Equal(new FftRandomState(271520213, 51), real.State.Battle.TurnBattle.Turns.RngState);
        Assert.Same(real.State.Battle, real.State.PendingFacing!.ExpectedBattle);
    }

    [Fact]
    public void Exact_budgets_fit_and_late_ACT_failure_preserves_Move_while_selection_failure_adopts_nothing()
    {
        foreach (var (left, evade, power, total) in new[] { (false, 0, 1, 43ul), (true, 0, 5, 23ul), (true, 100, 1, 22ul) })
        {
            var moved = Start(Ready(left: left, evade: evade, count: ulong.MaxValue - total), power: power); Success(moved);
            var complete = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation); Assert.True(complete.Accepted);
            Assert.Equal(ulong.MaxValue, complete.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        }
        foreach (ulong remaining in new[] { 0ul, 1ul, 35ul })
        {
            var state = Ready(count: ulong.MaxValue - remaining);
            Atomic(state, Start(state), AttackThrowStoneAiMoveFirstRejection.SelectionRejected);
        }
        var late = Start(Ready(count: ulong.MaxValue - 42)); var o = Success(late); string before = Snapshot(late.State.Battle);
        Assert.Equal(ulong.MaxValue - 6, o.DecisionRng.DrawCount);
        foreach (int attempt in new[] { 1, 2 })
        {
            var failure = FacingMoveDecisionBattle.Continue(late.State, A, late.Continuation);
            Assert.False(failure.Accepted); Assert.Same(late.State, failure.State); Assert.Empty(failure.Events);
            Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, failure.Transition.AttackRejection);
        }
        Assert.Equal(before, Snapshot(late.State.Battle)); Assert.Equal(o.Move.Destination, late.State.Battle.TurnBattle.Battle.Units.Single(u => u.Id == A).Position);
        var cancelled = FacingTurnBattle.WaitWithFacing(late.State, A, new FacingChoice.ChooseClosedEnemy(B)); Assert.True(cancelled.Accepted);
        Assert.Equal(FacingMoveRejection.StaleContinuation, FacingMoveDecisionBattle.Continue(cancelled.State, A, late.Continuation).Rejection);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void ACT_first_same_tile_wrong_branch_and_remaining_context_reject_without_partial_selection()
    {
        var state = Ready(); Atomic(state, Start(state, power: 2), AttackThrowStoneAiMoveFirstRejection.MoveFirstRequired);
        var immobile = Ready(move: 0); Atomic(immobile, Start(immobile), AttackThrowStoneAiMoveFirstRejection.MoveFirstRequired);
        foreach (var dispatch in new[] { Dispatch with { InitializedActorContext = false }, Dispatch with { PresentCompactRecords = false },
            Dispatch with { CanonicalCoordinates = false }, Dispatch with { OrdinaryContinuation = false } })
            Atomic(state, Start(state, dispatch: dispatch), AttackThrowStoneAiMoveFirstRejection.UnsupportedDispatchContext);
        var far = Ready(target: new(6, 1, 0));
        Assert.Equal(AttackThrowStoneAiScenarioRejection.OutOfReach,
            Atomic(far, Start(far), AttackThrowStoneAiMoveFirstRejection.SelectionRejected).SelectionFailure!.ScenarioFailure!.Reason);
        Assert.Equal(AttackThrowStoneAiPreparedSelectionRejection.UnsupportedProfile,
            Atomic(state, Start(state, selection: Selection with { ClearDirectTrajectories = false }),
                AttackThrowStoneAiMoveFirstRejection.SelectionRejected).SelectionFailure!.Reason);
        Atomic(state, Start(state, team: SwordAiScenarioTeam.Blue), AttackThrowStoneAiMoveFirstRejection.SelectionRejected);
        var moved = Start(state); Success(moved);
        Assert.Equal(FacingMoveRejection.UnavailableChoices,
            Atomic(moved.State, Start(moved.State), AttackThrowStoneAiMoveFirstRejection.BoundaryRejected).BoundaryReason);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_and_collection_policy_orders_replay_with_exact_continuation_binding()
    {
        foreach (bool second in new[] { false, true })
        {
            var actor = second ? B : A; var state = Ready(second: second); string before = Snapshot(state.Battle);
            var request = FreshRequest(second); var first = Start(state, request: request); Success(first);
            var replay = Start(state, request: request); Success(replay);
            var reversed = Start(Ready(second: second, reverse: true), request: request with { Policies = request.Policies.Reverse().ToImmutableArray() }); Success(reversed);
            Assert.Equal(Serialize(first.Outcome), Serialize(reversed.Outcome)); Assert.Equal(Snapshot(first.State.Battle), Snapshot(reversed.State.Battle));
            Assert.Equal(FacingMoveRejection.StaleContinuation, FacingMoveDecisionBattle.Continue(replay.State, actor, first.Continuation).Rejection);
            var actual = FacingMoveDecisionBattle.Continue(first.State, actor, first.Continuation); Assert.True(actual.Accepted);
            Assert.Equal(Snapshot(actual.State.Battle), Snapshot(FacingMoveDecisionBattle.Continue(reversed.State, actor, reversed.Continuation).State.Battle));
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    private static AttackThrowStoneAiMoveFirstResult Start(FacingTurnState state, int power = 1, SwordAiRefreshRequest? request = null,
        SwordAiCommandDispatchProfile? dispatch = null, AttackThrowStoneAiPreparedSelectionProfile? selection = null,
        SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue)
    {
        var p = Profile(); return AttackThrowStoneAiMoveFirstExecution.StartInReach(state, request ?? FreshRequest(), Stone, new(Sword, power),
            p.Attack, p.ThrowStone, p.Score, new(true, true, true), new(true, true, true), selection ?? Selection, team, dispatch ?? Dispatch);
    }
    private static AttackThrowStoneAiMoveFirstOutcome Success(AttackThrowStoneAiMoveFirstResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); Assert.NotNull(result.Continuation);
        return Assert.IsType<AttackThrowStoneAiMoveFirstOutcome>(result.Outcome); }
    private static AttackThrowStoneAiMoveFirstFailure Atomic(FacingTurnState state, AttackThrowStoneAiMoveFirstResult result, AttackThrowStoneAiMoveFirstRejection reason)
    { Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events); Assert.Null(result.Path);
        Assert.Null(result.Continuation); Assert.Null(result.Outcome); Assert.Equal(reason, result.Failure!.Reason); return result.Failure; }
    private static FacingTurnState Ready(ulong count = 7, int evade = 0, int hp = 50, int move = 3, bool left = false,
        TilePosition? target = null, bool second = false, bool reverse = false)
    {
        var actor = second ? B : A; var peer = second ? A : B;
        var rng = new FftRandomState(unchecked((1u - 12345u) * 4005161829u), count - 1);
        var movement = new UnitMovementProfile(left ? 1 : move, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        var map = new BattleMapDefinition(8, 8, 1, Enumerable.Repeat(new BattleTile(Ground, 10), 64).ToArray());
        BattleUnitState[] bodies = [new(actor, left ? new(3, 1, 0) : Origin, movement), new(peer, target ?? (left ? new(1, 1, 0) : new(2, 1, 0)), movement)];
        // Begin starts at AddCt: the first tick yields 115/95, then upkeep leaves the actor at CT15.
        TurnUnitState[] turns = [new(actor, 5, 110, 2), new(peer, 5, 90, 17)];
        AttackUnitState[] units = [new(actor, 50, 50, 0, 0, 5, 0, 0, 0, 0, 70, 0, BattleFacing.East, AttackUnitStatus.None, 0, Sword),
            new(peer, hp, hp, 0, 0, 5, evade, 0, 0, 0, 70, 0, left ? BattleFacing.East : BattleFacing.West, AttackUnitStatus.None, 0, Sword)];
        var fresh = FacingTurnBattle.Begin(new(new(new(map, reverse ? bodies.Reverse() : bodies, rng),
            new(reverse ? turns.Reverse() : turns, rng)), reverse ? units.Reverse() : units), new(Ground));
        var ready = FacingTurnBattle.Advance(fresh); Assert.True(ready.Accepted); return ready.State;
    }
}
