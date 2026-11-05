using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Tests.TargetedAbilityDecisionTests;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class FacingDecisionPlannedMoveTests
{
    private static readonly TilePosition Destination = new(3, 1, 0);
    private static readonly FlatOrdinaryMovementProfile Movement = new(Ground, A, B);

    [Fact]
    public void One_planner_can_choose_each_retained_followup_using_the_existing_Move_and_continuation_rules()
    {
        foreach (string kind in new[] { "Stone", "Wait", "Attack" })
        {
            var state = Ready(); string before = Snapshot(state); int calls = 0;
            var result = FacingDecisionBattle.Execute(state, A, c => {
                calls++;
                var rng = PlanRng(c.RngState);
                return kind switch {
                    "Stone" => c.PrepareMoveThenTargetedAbility(new(A, Destination), Movement, new(A, Stone, B), Profile(), rng, kind),
                    "Attack" => c.PrepareMoveThenAttack(new(A, Destination), Movement, new(A, B), null, rng, kind),
                    _ => c.PrepareMoveThenWait(new(A, Destination), Movement, new FacingChoice.ChooseClosedEnemy(B), rng, kind)
                };
            });
            Assert.True(result.Accepted); Assert.Equal(1, calls); Assert.Equal(kind, result.Diagnostic);
            Assert.Equal(new RandomState(3210001534, 16), result.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(result.State.Battle.TurnBattle.Turns.RngState, result.State.Battle.TurnBattle.Battle.RngState);
            Assert.Equal(Destination, result.Path!.Tiles[^1]); Assert.NotNull(result.Continuation);
            Assert.Equal(state.Revision + 1, result.State.Revision); Assert.Null(result.State.PendingFacing);
            Assert.Empty(result.Events.OfType<ThrowStoneResolvedEvent>());
            var actual = FacingMoveDecisionBattle.Continue(result.State, A, result.Continuation);
            Assert.Equal(1, calls);
            if (kind == "Attack")
            {
                // This fixture is deliberately unequipped: retain the Attack, then reject actual setup after Move.
                Assert.False(actual.Accepted); Assert.Same(result.State, actual.State);
                Assert.Equal(AttackCommandRejection.UnsupportedSetup, actual.Transition.AttackRejection);
            }
            else
            {
                Assert.True(actual.Accepted);
                Assert.Equal(kind == "Stone" ? new RandomState(3297241969, 23) : new(3210001534, 16), actual.State.Battle.TurnBattle.Turns.RngState);
                Assert.Equal(kind == "Stone", actual.State.PendingFacing is not null);
            }
            Assert.Equal(before, Snapshot(state));
        }
    }

    [Fact]
    public void Expired_foreign_and_throwing_planners_cannot_reuse_a_proposal_or_publish_private_data()
    {
        var state = Ready(); FacingDecisionContext? expired = null; FacingDecision<string>? saved = null;
        var first = FacingDecisionBattle.Execute(state, A, c => {
            expired = c; return saved = c.PrepareMoveThenWait(new(A, Destination), Movement, new FacingChoice.KeepCurrent(), c.RngState, "first");
        });
        Assert.True(first.Accepted);
        Assert.Throws<InvalidOperationException>(() => expired!.PrepareMoveThenWait(new(A, Destination), Movement,
            new FacingChoice.KeepCurrent(), expired.RngState, "expired"));
        Atomic(state, FacingDecisionBattle.Execute(state, A, _ => saved!), FacingDecisionRejection.InvalidDecision);
        Assert.Throws<InvalidOperationException>(() => FacingDecisionBattle.Execute<string>(state, A, c => {
            expired = c; throw new InvalidOperationException("planner failure");
        }));
        Assert.Throws<InvalidOperationException>(() => expired!.Reject<string>());
        var replay = FacingDecisionBattle.Execute(state, A, c => c.PrepareMoveThenWait(new(A, Destination), Movement,
            new FacingChoice.KeepCurrent(), c.RngState, "first"));
        Assert.True(replay.Accepted);
        Assert.Equal(FacingMoveRejection.StaleContinuation, FacingMoveDecisionBattle.Continue(replay.State, A, first.Continuation).Rejection);
    }

    [Fact]
    public void Invalid_RNG_intent_and_path_return_original_session_without_token_or_diagnostic()
    {
        var state = Ready(); string before = Snapshot(state);
        var rng = FacingDecisionBattle.Execute(state, A, c => c.PrepareMoveThenWait(new(A, Destination), Movement,
            new FacingChoice.KeepCurrent(), c.RngState with { Value = c.RngState.Value ^ 1 }, "private"));
        Atomic(state, rng, FacingDecisionRejection.InvalidRandomProgress); Assert.Equal(FacingMoveRejection.InvalidRandomProgress, rng.MoveRejection);
        var actor = FacingDecisionBattle.Execute(state, A, c => c.PrepareMoveThenTargetedAbility(new(B, Destination), Movement,
            new(A, Stone, B), Profile(), PlanRng(c.RngState), "private"));
        Atomic(state, actor, FacingDecisionRejection.CommandRejected); Assert.Equal(FacingMoveRejection.InvalidIntent, actor.MoveRejection);
        var path = FacingDecisionBattle.Execute(state, A, c => c.PrepareMoveThenWait(new(A, new(7, 7, 0)), Movement,
            new FacingChoice.KeepCurrent(), PlanRng(c.RngState), "private"));
        Atomic(state, path, FacingDecisionRejection.CommandRejected); Assert.Equal(FlatMovementRejection.InvalidDestination, path.PathRejection);
        Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Choice_revision_and_late_ACT_limits_preserve_per_command_atomicity()
    {
        var ready = Ready(target: new(3, 1, 0));
        var acted = FacingTurnBattle.UseTargetedAbility(ready, new(A, Stone, B), Profile()); Assert.True(acted.Accepted);
        Atomic(acted.State, Move(acted.State), FacingDecisionRejection.UnavailableChoices);
        var original = Ready(); var boundary = new FacingTurnState(original.Battle, original.Profile, ulong.MaxValue - 1, null, original.Lineage);
        var moved = Move(boundary); Assert.True(moved.Accepted); Assert.Equal(ulong.MaxValue, moved.State.Revision);
        var followup = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation);
        Assert.False(followup.Accepted); Assert.Same(moved.State, followup.State);
        Assert.Equal(FacingTurnRejection.RevisionExhausted, followup.Transition.FacingRejection);
        bool called = false;
        Atomic(moved.State, FacingDecisionBattle.Execute<string>(moved.State, A, c => { called = true; return c.Reject<string>(); }), FacingDecisionRejection.AuthorityRejected);
        Assert.False(called);
        // Eight planning draws fit, but only six remain for the seven-draw real Stone.
        var late = Move(Ready(count: ulong.MaxValue - 15)); Assert.True(late.Accepted);
        var failed = FacingMoveDecisionBattle.Continue(late.State, A, late.Continuation);
        Assert.False(failed.Accepted); Assert.Same(late.State, failed.State); Assert.Empty(failed.Events);
        Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, failed.Transition.AttackRejection);
    }

    private static FacingDecisionResult<string> Move(FacingTurnState state) => FacingDecisionBattle.Execute(state, A,
        c => c.PrepareMoveThenTargetedAbility(new(A, Destination), Movement, new(A, Stone, B), Profile(), PlanRng(c.RngState), "Stone"));
    private static void Atomic(FacingTurnState state, FacingDecisionResult<string> result, FacingDecisionRejection reason)
    {
        Assert.False(result.Accepted); Assert.Equal(reason, result.Rejection); Assert.Same(state, result.State);
        Assert.Empty(result.Events); Assert.Null(result.Path); Assert.Null(result.Continuation); Assert.Null(result.Diagnostic);
    }
}
