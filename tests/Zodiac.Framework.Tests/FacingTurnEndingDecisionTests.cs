using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class FacingTurnEndingDecisionTests
{
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly FlatOrdinaryMovementProfile Movement = new(Ground, A, B);
    private static readonly MoveUnitCommand Move = new(A, new(2, 2, 0));
    private static readonly FacingChoice Facing = new FacingChoice.ChooseClosedEnemy(B);

    [Fact]
    public void Verified_RNG_path_and_actual_receipt_settle_once_at_the_destination()
    {
        var state = AfterAct(); var rng = new FftBiosRandom().Next(state.Battle.TurnBattle.Turns.RngState).State; int calls = 0;
        var result = FacingDecisionBattle.Execute(state, A, c =>
        { calls++; return c.PrepareTurnEndingMove(Move, Movement, Facing, rng, "complete"); });
        Assert.True(result.Accepted); Assert.Equal(1, calls); Assert.Equal("complete", result.Diagnostic);
        Assert.Equal(state.Revision + 2, result.State.Revision); Assert.Null(result.State.PendingFacing);
        var path = Assert.IsType<FlatMovementPath>(result.Path);
        Assert.Equal(new[] { new TilePosition(2, 1, 0), new(2, 2, 0) }, path.Tiles); Assert.Equal(BattleFacing.North, path.FinalHeading);
        Assert.Equal(BattleFacing.South, result.State.Battle.Combatants.Single(u => u.UnitId == A).Facing);
        Assert.Single(result.Events.OfType<VoluntaryPathCompletedEvent>()); Assert.Single(result.Events.OfType<FacingSettledEvent>());
        Assert.Empty(result.Events.OfType<AttackResolvedEvent>());
        Assert.Single(result.Events.OfType<AttackTurnBattleEvent>().Select(e => e.Event).OfType<TurnBattleClockEvent>()
            .Select(e => e.Event).OfType<TurnEndedEvent>());
        var actor = result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A);
        Assert.False(actor.HasTurn); Assert.True(actor.ActionTaken); Assert.True(actor.MovementTaken); Assert.Equal(15, actor.Ct);
        Assert.Equal(rng, result.State.Battle.TurnBattle.Turns.RngState); Assert.Equal(rng, result.State.Battle.TurnBattle.Battle.RngState);
        Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, result.State.Battle.TurnBattle.Turns.Ticks);
        Assert.Equal(FacingTurnRejection.MissingCompletion,
            FacingTurnBattle.FinishFacing(result.State, A, null, Facing).FacingRejection);
        Assert.Equal(new TilePosition(2, 1, 0), state.Battle.TurnBattle.Battle.Units.Single(u => u.Id == A).Position);
        Assert.True(state.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).HasTurn);
    }

    [Fact]
    public void Move_proposals_are_invocation_bound_and_factories_close_after_planning()
    {
        var state = AfterAct(); FacingDecision<string>? saved = null; FacingDecisionContext? expired = null;
        var first = FacingDecisionBattle.Execute(state, A, c =>
        { expired = c; return saved = c.PrepareTurnEndingMove(Move, Movement, Facing, c.RngState, "first"); });
        Assert.True(first.Accepted);
        Assert.Throws<InvalidOperationException>(() => expired!.PrepareTurnEndingMove(Move, Movement, Facing, expired.RngState, "late"));
        Atomic(state, FacingDecisionBattle.Execute(state, A, _ => saved!), FacingDecisionRejection.InvalidDecision);
    }

    [Fact]
    public void Wrong_actor_choice_mode_and_invalid_RNG_cannot_authorize_a_path()
    {
        var state = AfterAct();
        var wrong = FacingDecisionBattle.Execute(state, A, c => c.PrepareTurnEndingMove(new(B, new(2, 2, 0)), Movement, Facing, c.RngState, "wrong"));
        Atomic(state, wrong, FacingDecisionRejection.AuthorityRejected); Assert.Equal(FacingTurnRejection.WrongActor, wrong.Transition.FacingRejection);
        var ready = Ready();
        Atomic(ready, FacingDecisionBattle.Execute(ready, A, c => c.PrepareTurnEndingMove(Move, Movement, Facing, c.RngState, "pre-act")),
            FacingDecisionRejection.UnavailableChoices);
        var moved = FacingTurnPathMovement.MoveAlongPath(ready, Move, Movement); Assert.True(moved.Accepted);
        Atomic(moved.State, FacingDecisionBattle.Execute(moved.State, A, c => c.PrepareTurnEndingMove(new(A, new(2, 1, 0)), Movement, Facing, c.RngState, "already moved")),
            FacingDecisionRejection.UnavailableChoices);
        var rng = state.Battle.TurnBattle.Turns.RngState;
        Atomic(state, FacingDecisionBattle.Execute(state, A, c => c.PrepareTurnEndingMove(Move, Movement, Facing,
            new(rng.Value ^ 1, rng.DrawCount), "invalid")), FacingDecisionRejection.InvalidRandomProgress);
    }

    [Fact]
    public void Late_path_and_facing_failures_discard_adopted_RNG_events_path_and_diagnostics()
    {
        var state = AfterAct(); var original = state.Battle.TurnBattle.Turns.RngState; var rng = new FftBiosRandom().Next(original).State;
        var path = FacingDecisionBattle.Execute(state, A, c => c.PrepareTurnEndingMove(new(A, new(1, 1, 0)), Movement, Facing, rng, "private"));
        Atomic(state, path, FacingDecisionRejection.CommandRejected); Assert.Equal(FlatMovementRejection.InvalidDestination, path.PathRejection);
        var facing = FacingDecisionBattle.Execute(state, A, c => c.PrepareTurnEndingMove(Move, Movement,
            new FacingChoice.Fixed((BattleFacing)99), rng, "private"));
        Atomic(state, facing, FacingDecisionRejection.CommandRejected); Assert.Equal(FacingTurnRejection.UnsupportedFacing, facing.Transition.FacingRejection);
        Assert.Equal(original, state.Battle.TurnBattle.Turns.RngState); Assert.Equal(original, state.Battle.TurnBattle.Battle.RngState);
        Assert.Equal(50, state.Battle.Combatants.Single(u => u.UnitId == B).Hp); // Prior actual miss remains published.
    }

    [Fact]
    public void Move_needs_two_revisions_while_same_tile_Wait_needs_one_and_late_exhaustion_is_atomic()
    {
        var actual = AfterAct(); var fits = Revision(actual, ulong.MaxValue - 2);
        var moved = FacingDecisionBattle.Execute(fits, A, c => c.PrepareTurnEndingMove(Move, Movement, Facing, c.RngState, "fits"));
        Assert.True(moved.Accepted); Assert.Equal(ulong.MaxValue, moved.State.Revision);
        var shortBudget = Revision(actual, ulong.MaxValue - 1);
        var failed = FacingDecisionBattle.Execute(shortBudget, A, c => c.PrepareTurnEndingMove(Move, Movement, Facing,
            new FftBiosRandom().Next(c.RngState).State, "private"));
        Atomic(shortBudget, failed, FacingDecisionRejection.CommandRejected);
        Assert.Equal(FacingTurnRejection.RevisionExhausted, failed.Transition.FacingRejection);
        var wait = FacingDecisionBattle.Execute(shortBudget, A, c => c.PrepareWait(Facing, c.RngState, "wait"));
        Assert.True(wait.Accepted); Assert.Equal(ulong.MaxValue, wait.State.Revision); Assert.Null(wait.Path);
        Assert.Equal(35, wait.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
    }

    private static void Atomic(FacingTurnState state, FacingDecisionResult<string> result, FacingDecisionRejection reason)
    { Assert.False(result.Accepted); Assert.Equal(reason, result.Rejection); Assert.Same(state, result.State);
        Assert.Empty(result.Events); Assert.Null(result.Diagnostic); Assert.Null(result.Path); }
    private static FacingTurnState Revision(FacingTurnState state, ulong revision) =>
        new(state.Battle, state.Profile, revision, state.PendingFacing, state.Lineage); // Existing test-only friend access.
    private static FacingTurnState AfterAct()
    {
        var result = FacingTurnBattle.Attack(Ready(), new(A, B), new(Weapon, 5)); Assert.True(result.Accepted);
        Assert.Equal(50, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        return result.State;
    }
    private static FacingTurnState Ready()
    {
        var rng = new FftRandomState(unchecked((1u - 12345u) * 4005161829u), 6);
        var movement = new UnitMovementProfile(2, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        var map = new BattleMapDefinition(5, 3, 1, Enumerable.Repeat(new BattleTile(Ground, 10), 15).ToArray());
        var battle = new BattleState(map, [new(A, new(2, 1, 0), movement), new(B, new(1, 1, 0), movement)], rng);
        var turns = new TurnCycleState([new(A, 5, 110, 2), new(B, 5, 90, 17)], rng);
        AttackUnitState Unit(BattleUnitId id, int evade) => new(id, 50, 50, 0, 0, 5, evade, 0, 0, 0, 70, 0,
            BattleFacing.East, AttackUnitStatus.None, 0, Weapon);
        var ready = FacingTurnBattle.Advance(FacingTurnBattle.Begin(new(new(battle, turns), [Unit(A, 0), Unit(B, 89)]), new(Ground)));
        Assert.True(ready.Accepted); return ready.State;
    }
}
