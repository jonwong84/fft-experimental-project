using Xunit;
using static Zodiac.Framework.Tests.TargetedAbilityDecisionTests;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class TargetedAbilityMoveDecisionTests
{
    private static readonly TilePosition Destination = new(3, 1, 0);
    private static readonly FlatOrdinaryMovementProfile Movement = new(Ground, A, B);

    [Fact]
    public void Confirmed_path_adopts_RNG_once_and_frozen_ACT_uses_real_moved_range_damage_and_final_receipt()
    {
        foreach (var (targetX, hp, evade, hit, pushed, word, count) in new[] {
            (6, 50, 0, true, true, 3297241969u, 23ul), (7, 50, 0, true, false, 3297241969u, 23ul),
            (6, 5, 0, true, false, 3297241969u, 23ul), (6, 50, 100, false, false, 595337866u, 20ul) })
        {
            var state = Ready(target: new(targetX, 1, 0), hp: hp, evade: evade); string before = Snapshot(state); int calls = 0;
            var moved = FacingMoveDecisionBattle.ExecuteMove(state, A, c => {
                calls++; return c.PrepareMoveThenTargetedAbility(new(A, Destination), Movement, new(A, Stone, B), Profile(), PlanRng(c.RngState), "frozen");
            });
            var token = Success(moved); Assert.Equal("frozen", moved.Diagnostic); Assert.Equal(1, calls);
            Assert.Equal(new FftRandomState(3210001534, 16), moved.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(moved.State.Battle.TurnBattle.Turns.RngState, moved.State.Battle.TurnBattle.Battle.RngState);
            Assert.Equal(new[] { new TilePosition(2, 1, 0), Destination }, moved.Path!.Tiles.ToArray());
            Assert.Equal(BattleFacing.East, moved.Path.FinalHeading); Assert.Equal(Destination, Position(moved.State, A));
            Assert.Equal(state.Revision + 1, moved.State.Revision); Assert.Null(moved.State.PendingFacing);
            Assert.Empty(moved.Events.OfType<ThrowStoneResolvedEvent>()); Assert.Equal(hp, moved.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            var act = FacingMoveDecisionBattle.Continue(moved.State, A, token); Assert.True(act.Accepted); Assert.Equal(1, calls);
            Assert.Equal(new ThrowStoneResolvedEvent(A, Stone, B, hit, hit ? 5 : 0, hit), Assert.Single(act.Events.OfType<ThrowStoneResolvedEvent>()));
            Assert.Equal(hit ? hp - 5 : hp, act.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.Equal(new TilePosition(pushed ? 7 : targetX, 1, 0), Position(act.State, B));
            Assert.Equal(new FftRandomState(word, count), act.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(act.State.Battle.TurnBattle.Turns.RngState, act.State.Battle.TurnBattle.Battle.RngState);
            Assert.Equal(moved.State.Revision + 1, act.State.Revision); Assert.Null(act.State.Battle.TurnBattle.Turns.ActiveUnitId);
            var receipt = Assert.IsType<FacingCompletionReceipt>(act.State.PendingFacing);
            Assert.Same(act.State.Battle, receipt.ExpectedBattle); Assert.Same(state.Lineage, receipt.Lineage);
            var faced = FacingTurnBattle.FinishFacing(act.State, A, receipt, new FacingChoice.Fixed(BattleFacing.North));
            Assert.True(faced.Accepted); Assert.Equal(state.Revision + 3, faced.State.Revision);
            Assert.Equal(act.State.Battle.TurnBattle, faced.State.Battle.TurnBattle);
            Assert.Equal(15, faced.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
            Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, faced.State.Battle.TurnBattle.Turns.Ticks);
            Assert.Equal(before, Snapshot(state));
        }
    }

    [Fact]
    public void Invalid_intents_RNG_or_paths_publish_no_Move_adoption_token_or_diagnostic()
    {
        var state = Ready(); string before = Snapshot(state);
        foreach (var (command, profile) in new[] {
            (new UseTargetedAbilityCommand(B, Stone, A), Profile()),
            (new UseTargetedAbilityCommand(A, new("sample:ability/missing"), B), Profile()),
            (new UseTargetedAbilityCommand(A, Stone, B), Profile(granted: false)),
            (new UseTargetedAbilityCommand(A, Stone, A), Profile()),
            (new UseTargetedAbilityCommand(A, Stone, new("sample:unit/missing")), Profile()),
            (new UseTargetedAbilityCommand(A, Stone, B), Profile(ground: new("sample:terrain/other"))) })
            Atomic(state, Move(state, command: command, profile: profile), FacingMoveRejection.InvalidIntent);
        Atomic(state, Move(state, moveActor: B), FacingMoveRejection.InvalidIntent);
        var invalidRng = FacingMoveDecisionBattle.ExecuteMove(state, A, c => c.PrepareMoveThenTargetedAbility(new(A, Destination), Movement,
            new(A, Stone, B), Profile(), c.RngState with { Value = c.RngState.Value ^ 1 }, "private"));
        Atomic(state, invalidRng, FacingMoveRejection.InvalidRandomProgress);
        var path = Move(state, destination: new(7, 7, 0)); Atomic(state, path, FacingMoveRejection.MoveRejected);
        Assert.Equal(FlatMovementRejection.InvalidDestination, path.PathRejection); Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Post_Move_range_or_late_draw_failure_preserves_committed_state_and_allows_identical_retry_or_Wait()
    {
        var tooFar = Move(Ready(target: new(7, 1, 0)), destination: new(1, 1, 0)); var farToken = Success(tooFar);
        var range = FacingMoveDecisionBattle.Continue(tooFar.State, A, farToken);
        AtomicAct(tooFar.State, range, AttackCommandRejection.OutOfRange); Assert.Equal(new TilePosition(1, 1, 0), Position(range.State, A));
        Assert.Equal(new FftRandomState(3210001534, 16), range.State.Battle.TurnBattle.Turns.RngState);
        // Planning and Move fit exactly; actual ACT fails on its final draw. The published Move must survive.
        var moved = Move(Ready(count: ulong.MaxValue - 15)); var token = Success(moved); string before = Snapshot(moved.State);
        foreach (int attempt in new[] { 1, 2 })
            AtomicAct(moved.State, FacingMoveDecisionBattle.Continue(moved.State, A, token), AttackCommandRejection.RandomDrawCountExhausted);
        Assert.Equal(before, Snapshot(moved.State)); Assert.Equal(Destination, Position(moved.State, A));
        var cancelled = FacingTurnBattle.WaitWithFacing(moved.State, A, new FacingChoice.KeepCurrent()); Assert.True(cancelled.Accepted);
        Assert.Equal(35, cancelled.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
        Assert.Equal(moved.State.Battle.TurnBattle.Turns.RngState, cancelled.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(FacingMoveRejection.StaleContinuation, FacingMoveDecisionBattle.Continue(cancelled.State, A, token).Rejection);
    }

    [Fact]
    public void Frozen_payload_exact_session_and_proposal_lifetime_reject_substitution_and_cross_replay()
    {
        var state = Ready(); var command = new UseTargetedAbilityCommand(A, Stone, B); var profile = Profile();
        FacingMoveDecisionContext? expired = null; FacingMoveDecision<string>? saved = null;
        var moved = FacingMoveDecisionBattle.ExecuteMove(state, A, c => {
            expired = c; saved = c.PrepareMoveThenTargetedAbility(new(A, Destination), Movement, command, profile, PlanRng(c.RngState), "fixed"); return saved;
        });
        var token = Success(moved); command = command with { AbilityId = new("sample:ability/replacement") }; profile = Profile(granted: false);
        var act = FacingMoveDecisionBattle.Continue(moved.State, A, token); Assert.True(act.Accepted);
        Assert.Equal(Stone, Assert.Single(act.Events.OfType<ThrowStoneResolvedEvent>()).AbilityId);
        Atomic(state, FacingMoveDecisionBattle.ExecuteMove(state, A, _ => saved!), FacingMoveRejection.InvalidDecision);
        Assert.Throws<InvalidOperationException>(() => expired!.PrepareMoveThenTargetedAbility(new(A, Destination), Movement, command, profile, expired.RngState, "closed"));
        var replay = Move(state); Success(replay); Assert.Equal(Snapshot(moved.State), Snapshot(replay.State));
        var foreign = Move(Ready()); Success(foreign);
        foreach (var other in new[] { replay.State, foreign.State, act.State })
            Assert.Equal(FacingMoveRejection.StaleContinuation, FacingMoveDecisionBattle.Continue(other, A, token).Rejection);
        Assert.Equal(FacingMoveRejection.WrongActor, FacingMoveDecisionBattle.Continue(moved.State, B, token).Rejection);
        Assert.Equal(FacingMoveRejection.MissingContinuation, FacingMoveDecisionBattle.Continue(moved.State, A, null).Rejection);
        Assert.True(FacingMoveDecisionBattle.Continue(moved.State, A, token).Accepted); // Replay on its exact immutable branch is allowed.
    }

    [Fact]
    public void Actual_draw_and_per_command_revision_budgets_cover_exact_ACT_and_later_facing_exhaustion()
    {
        foreach (var (evade, actualDraws) in new[] { (0, 7ul), (100, 4ul) })
        {
            var moved = Move(Ready(count: ulong.MaxValue - 1 - 8 - actualDraws, evade: evade)); var token = Success(moved);
            var act = FacingMoveDecisionBattle.Continue(moved.State, A, token); Assert.True(act.Accepted);
            Assert.Equal(ulong.MaxValue, act.State.Battle.TurnBattle.Turns.RngState.DrawCount);
            Assert.True(FacingTurnBattle.FinishFacing(act.State, A, act.State.PendingFacing, new FacingChoice.KeepCurrent()).Accepted);
        }
        var original = Ready();
        var fits = Move(Revision(original, ulong.MaxValue - 3)); var fitsToken = Success(fits);
        var completed = FacingMoveDecisionBattle.Continue(fits.State, A, fitsToken); Assert.True(completed.Accepted);
        Assert.Equal(ulong.MaxValue, FacingTurnBattle.FinishFacing(completed.State, A, completed.State.PendingFacing, new FacingChoice.KeepCurrent()).State.Revision);
        var noFacing = Move(Revision(original, ulong.MaxValue - 2)); var noFacingToken = Success(noFacing);
        var lastAct = FacingMoveDecisionBattle.Continue(noFacing.State, A, noFacingToken); Assert.True(lastAct.Accepted);
        Assert.Equal(FacingTurnRejection.RevisionExhausted,
            FacingTurnBattle.FinishFacing(lastAct.State, A, lastAct.State.PendingFacing, new FacingChoice.KeepCurrent()).FacingRejection);
        var lastMove = Move(Revision(original, ulong.MaxValue - 1)); var lastToken = Success(lastMove);
        var rejected = FacingMoveDecisionBattle.Continue(lastMove.State, A, lastToken);
        Assert.Equal(FacingMoveRejection.AuthorityRejected, rejected.Rejection); Assert.Same(lastMove.State, rejected.State); Assert.Empty(rejected.Events);
        Assert.Equal(FacingTurnRejection.RevisionExhausted, rejected.Transition.FacingRejection);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_and_collection_orders_replay_without_mutating_the_original_or_moved_state()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = Ready(second: second); string before = Snapshot(state); var actor = second ? B : A;
            var moved = Move(state); var token = Success(moved); string movedBefore = Snapshot(moved.State);
            var result = FacingMoveDecisionBattle.Continue(moved.State, actor, token); Assert.True(result.Accepted);
            var reordered = Move(Ready(second: second, reverse: true)); var otherToken = Success(reordered);
            var replay = FacingMoveDecisionBattle.Continue(reordered.State, actor, otherToken); Assert.True(replay.Accepted);
            Assert.Equal(Snapshot(result.State), Snapshot(replay.State)); Assert.Equal(before, Snapshot(state));
            Assert.Equal(movedBefore, Snapshot(moved.State));
        }
    }

    private static FacingMoveResult<string> Move(FacingTurnState state, TilePosition? destination = null, UseTargetedAbilityCommand? command = null,
        TargetedAbilityBattleProfile? profile = null, BattleUnitId? moveActor = null)
    {
        var actor = state.Battle.TurnBattle.Turns.ActiveUnitId!.Value; var target = state.Battle.Combatants.Single(u => u.UnitId != actor).UnitId;
        return FacingMoveDecisionBattle.ExecuteMove(state, actor, c => c.PrepareMoveThenTargetedAbility(new(moveActor ?? actor, destination ?? Destination),
            Movement, command ?? new(actor, Stone, target), profile ?? Profile(), PlanRng(c.RngState), "frozen"));
    }
    private static FacingMoveContinuation Success(FacingMoveResult<string> result)
    { Assert.True(result.Accepted, result.Rejection.ToString()); return Assert.IsType<FacingMoveContinuation>(result.Continuation); }
    private static void Atomic(FacingTurnState state, FacingMoveResult<string> result, FacingMoveRejection reason)
    { Assert.False(result.Accepted); Assert.Equal(reason, result.Rejection); Assert.Same(state, result.State);
        Assert.Empty(result.Events); Assert.Null(result.Path); Assert.Null(result.Continuation); Assert.Null(result.Diagnostic); }
    private static void AtomicAct(FacingTurnState state, FacingMoveContinueResult result, AttackCommandRejection reason)
    { Assert.False(result.Accepted); Assert.Equal(FacingMoveRejection.CommandRejected, result.Rejection); Assert.Same(state, result.State);
        Assert.Empty(result.Events); Assert.Equal(reason, result.Transition.AttackRejection); }
    private static FacingTurnState Revision(FacingTurnState state, ulong revision) => new(state.Battle, state.Profile, revision, null, state.Lineage);
}
