using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class FacingMoveDecisionTests
{
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly FlatOrdinaryMovementProfile Profile = new(Ground, A, B);
    private static readonly TilePosition Destination = new(2, 1, 0);

    [Fact]
    public void Planned_RNG_and_real_path_publish_once_then_frozen_Attack_uses_moved_origin_without_planning_again()
    {
        var state = Active(); var before = Snapshot(state); int calls = 0;
        var moved = FacingMoveDecisionBattle.ExecuteMove(state, A, c =>
        { calls++; return c.PrepareMoveThenAttack(new(A, Destination), Profile, new(A, B), new(Weapon, 5), Draw(c.RngState, 2), "fresh"); });
        var token = Success(moved); Assert.Equal(1, calls); Assert.Equal("fresh", moved.Diagnostic);
        Assert.Equal(Draw(state.Battle.TurnBattle.Turns.RngState, 2), moved.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(moved.State.Battle.TurnBattle.Battle.RngState, moved.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(BattleFacing.East, moved.Path!.FinalHeading);
        Assert.Equal(BattleFacing.East, moved.State.Battle.Combatants.Single(u => u.UnitId == A).Facing);
        Assert.Equal(state.Revision + 1, moved.State.Revision); Assert.Null(moved.State.PendingFacing);
        Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, moved.State.Battle.TurnBattle.Turns.Ticks);
        var next = FacingMoveDecisionBattle.Continue(moved.State, A, token);
        Assert.True(next.Accepted); Assert.Equal(1, calls); Assert.Single(next.Events.OfType<AttackResolvedEvent>());
        Assert.Equal(moved.State.Revision + 1, next.State.Revision);
        var receipt = Assert.IsType<FacingCompletionReceipt>(next.State.PendingFacing);
        Assert.Same(next.State.Battle, receipt.ExpectedBattle); Assert.Same(state.Lineage, receipt.Lineage);
        Assert.True(FacingTurnBattle.FinishFacing(next.State, A, receipt, new FacingChoice.ChooseClosedEnemy(B)).Accepted);
        Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Wait_facing_is_resolved_after_Move_and_preserves_MAX_RNG_with_a_single_ACT_refund()
    {
        var state = Active(count: ulong.MaxValue, enemy: new(3, 2, 0));
        var moved = FacingMoveDecisionBattle.ExecuteMove(state, A, c =>
            c.PrepareMoveThenWait(new(A, new(3, 1, 0)), Profile, new FacingChoice.ChooseClosedEnemy(B), c.RngState, "wait"));
        var token = Success(moved); Assert.Equal(BattleFacing.East, moved.Path!.FinalHeading);
        var next = FacingMoveDecisionBattle.Continue(moved.State, A, token); Assert.True(next.Accepted);
        Assert.Equal(BattleFacing.North, next.State.Battle.Combatants.Single(u => u.UnitId == A).Facing);
        Assert.Equal(35, next.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
        Assert.Equal(ulong.MaxValue, next.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        Assert.Null(next.State.PendingFacing); Assert.Empty(next.Events.OfType<AttackResolvedEvent>());
        Assert.Equal(moved.State.Revision + 1, next.State.Revision);
    }

    [Fact]
    public void Failed_path_rolls_back_planning_RNG_and_exposes_no_path_token_or_diagnostic()
    {
        var state = Active(); var before = Snapshot(state);
        var result = FacingMoveDecisionBattle.ExecuteMove(state, A, c =>
            c.PrepareMoveThenWait(new(A, new(3, 1, 0)), Profile, new FacingChoice.KeepCurrent(), Draw(c.RngState, 1), "hidden"));
        Atomic(state, result, FacingMoveRejection.MoveRejected); Assert.Equal(FlatMovementRejection.InvalidDestination, result.PathRejection);
        Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Followup_exhaustion_preserves_committed_Move_and_is_deterministically_retryable()
    {
        var state = Active(count: ulong.MaxValue - 1);
        var moved = FacingMoveDecisionBattle.ExecuteMove(state, A, c =>
            c.PrepareMoveThenAttack(new(A, Destination), Profile, new(A, B), new(Weapon, 5), Draw(c.RngState, 1), "attack"));
        var token = Success(moved); var before = Snapshot(moved.State);
        foreach (int attempt in new[] { 1, 2 })
        {
            var failed = FacingMoveDecisionBattle.Continue(moved.State, A, token);
            Assert.False(failed.Accepted); Assert.Equal(FacingMoveRejection.CommandRejected, failed.Rejection);
            Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, failed.Transition.AttackRejection);
            Assert.Same(moved.State, failed.State); Assert.Empty(failed.Events);
        }
        Assert.Equal(before, Snapshot(moved.State));
        Assert.Equal(Destination, moved.State.Battle.TurnBattle.Battle.Units.Single(u => u.Id == A).Position);
        var cancelled = FacingTurnBattle.WaitWithFacing(moved.State, A, new FacingChoice.ChooseClosedEnemy(B));
        Assert.True(cancelled.Accepted); Assert.Equal(ulong.MaxValue, cancelled.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        Assert.Equal(FacingMoveRejection.StaleContinuation, FacingMoveDecisionBattle.Continue(cancelled.State, A, token).Rejection);
    }

    [Fact]
    public void Exact_state_binding_rejects_equal_valued_replays_foreign_sessions_wrong_actor_and_newer_states()
    {
        var state = Active(); var first = MoveWait(state); var token = Success(first);
        var replay = MoveWait(state); Success(replay); Assert.Equal(Snapshot(first.State), Snapshot(replay.State));
        var foreign = MoveWait(Active()); Success(foreign);
        Assert.Equal(FacingMoveRejection.StaleContinuation, FacingMoveDecisionBattle.Continue(replay.State, A, token).Rejection);
        Assert.Equal(FacingMoveRejection.StaleContinuation, FacingMoveDecisionBattle.Continue(foreign.State, A, token).Rejection);
        Assert.Equal(FacingMoveRejection.WrongActor, FacingMoveDecisionBattle.Continue(first.State, B, token).Rejection);
        Assert.Equal(FacingMoveRejection.MissingContinuation, FacingMoveDecisionBattle.Continue(first.State, A, null).Rejection);
        var next = FacingMoveDecisionBattle.Continue(first.State, A, token); Assert.True(next.Accepted);
        Assert.Equal(FacingMoveRejection.StaleContinuation, FacingMoveDecisionBattle.Continue(next.State, A, token).Rejection);
        var again = FacingMoveDecisionBattle.Continue(first.State, A, token); Assert.True(again.Accepted);
        Assert.Equal(Snapshot(next.State), Snapshot(again.State)); Assert.Equal(next.Events.ToArray(), again.Events.ToArray());
    }

    [Fact]
    public void Closed_cached_rejected_and_throwing_planners_cannot_issue_or_reuse_a_proposal()
    {
        var state = Active(); FacingMoveDecisionContext? expired = null; FacingMoveDecision<string>? saved = null;
        var first = FacingMoveDecisionBattle.ExecuteMove(state, A, c =>
        { expired = c; return saved = c.PrepareMoveThenWait(new(A, Destination), Profile, new FacingChoice.KeepCurrent(), c.RngState, "first"); });
        Success(first); Assert.Throws<InvalidOperationException>(() => expired!.Reject<string>());
        Atomic(state, FacingMoveDecisionBattle.ExecuteMove(state, A, _ => saved!), FacingMoveRejection.InvalidDecision);
        Atomic(state, FacingMoveDecisionBattle.ExecuteMove<string>(state, A, _ => null!), FacingMoveRejection.InvalidDecision);
        Atomic(state, FacingMoveDecisionBattle.ExecuteMove<string>(state, A, c => c.Reject<string>()), FacingMoveRejection.DecisionRejected);
        Assert.Throws<InvalidOperationException>(() => FacingMoveDecisionBattle.ExecuteMove<string>(state, A, c =>
        { expired = c; throw new InvalidOperationException("failed planner"); }));
        Assert.Throws<InvalidOperationException>(() => expired!.Reject<string>());
    }

    [Fact]
    public void Invalid_intent_RNG_or_authority_never_publishes_the_staged_session()
    {
        var state = Active(); var before = Snapshot(state); var rng = state.Battle.TurnBattle.Turns.RngState;
        foreach (var bad in new[] { new FftRandomState(rng.Value ^ 1, rng.DrawCount), new FftRandomState(rng.Value, rng.DrawCount - 1) })
            Atomic(state, FacingMoveDecisionBattle.ExecuteMove(state, A, c =>
                c.PrepareMoveThenWait(new(A, Destination), Profile, new FacingChoice.KeepCurrent(), bad, "bad")), FacingMoveRejection.InvalidRandomProgress);
        Atomic(state, FacingMoveDecisionBattle.ExecuteMove(state, A, c =>
            c.PrepareMoveThenAttack(new(A, Destination), Profile, new(B, A), new(Weapon, 5), c.RngState, "bad")), FacingMoveRejection.InvalidIntent);
        bool called = false;
        Atomic(state, FacingMoveDecisionBattle.ExecuteMove<string>(state, B, c => { called = true; return c.Reject<string>(); }), FacingMoveRejection.AuthorityRejected);
        Assert.False(called); Assert.Equal(before, Snapshot(state));
        var moved = MoveWait(state); Success(moved);
        Atomic(moved.State, MoveWait(moved.State), FacingMoveRejection.UnavailableChoices);
    }

    [Fact]
    public void Revision_limits_apply_per_published_step_without_reserving_future_slots()
    {
        var original = Active();
        var state = new FacingTurnState(original.Battle, original.Profile, ulong.MaxValue - 1, null, original.Lineage);
        var moved = MoveWait(state); var token = Success(moved); Assert.Equal(ulong.MaxValue, moved.State.Revision);
        var failed = FacingMoveDecisionBattle.Continue(moved.State, A, token);
        Assert.Equal(FacingMoveRejection.AuthorityRejected, failed.Rejection);
        Assert.Equal(FacingTurnRejection.RevisionExhausted, failed.Transition.FacingRejection); Assert.Same(moved.State, failed.State);
        Atomic(moved.State, MoveWait(moved.State), FacingMoveRejection.AuthorityRejected);
    }

    private static FacingMoveResult<string> MoveWait(FacingTurnState state) => FacingMoveDecisionBattle.ExecuteMove(state, A,
        c => c.PrepareMoveThenWait(new(A, Destination), Profile, new FacingChoice.ChooseClosedEnemy(B), c.RngState, "wait"));
    private static FacingMoveContinuation Success(FacingMoveResult<string> result)
    { Assert.True(result.Accepted, result.Rejection.ToString()); Assert.NotNull(result.Path); return Assert.IsType<FacingMoveContinuation>(result.Continuation); }
    private static void Atomic(FacingTurnState state, FacingMoveResult<string> result, FacingMoveRejection reason)
    { Assert.False(result.Accepted); Assert.Equal(reason, result.Rejection); Assert.Same(state, result.State); Assert.Empty(result.Events);
        Assert.Null(result.Path); Assert.Null(result.Continuation); Assert.Null(result.Diagnostic); }
    private static FftRandomState Draw(FftRandomState state, int count)
    { for (int i = 0; i < count; i++) state = new FftBiosRandom().Next(state).State; return state; }
    private static string Snapshot(FacingTurnState state) => JsonSerializer.Serialize(new { state.Revision, state.Battle.Combatants,
        state.Battle.TurnBattle.Turns, state.Battle.TurnBattle.Battle.RngState,
        Bodies = state.Battle.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved }) });
    private static FacingTurnState Active(ulong count = 7, TilePosition? enemy = null)
    {
        var rng = new FftRandomState(1, count - 1);
        var movement = new UnitMovementProfile(3, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        var map = new BattleMapDefinition(5, 4, 1, Enumerable.Repeat(new BattleTile(Ground, 10), 20).ToArray());
        var bodies = new BattleUnitState[] { new(A, new(1, 1, 0), movement), new(B, enemy ?? new(3, 1, 0), movement) };
        var turns = new TurnUnitState[] { new(A, 5, 110, 2), new(B, 5, 90, 17) };
        AttackUnitState Unit(BattleUnitId id) => new(id, 50, 50, 0, 0, 5, 0, 0, 0, 0, 70, 0, BattleFacing.West, AttackUnitStatus.None, 0, Weapon);
        var fresh = FacingTurnBattle.Begin(new(new(new(map, bodies, rng), new(turns, rng)), [Unit(A), Unit(B)]), new(Ground));
        var result = FacingTurnBattle.Advance(fresh); Assert.True(result.Accepted); return result.State;
    }
}
