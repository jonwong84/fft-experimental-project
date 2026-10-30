using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class FacingDecisionTests
{
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");

    [Fact]
    public void Framework_invokes_fresh_planner_once_and_commits_one_Wait_with_verified_RNG()
    {
        var state = Active(); int calls = 0;
        var result = FacingDecisionBattle.Execute(state, A, context =>
        {
            calls++; Assert.Same(state.Battle, context.Battle); Assert.Equal(A, context.ActorId);
            var after = new FftBiosRandom().Next(context.RngState).State;
            return context.PrepareWait(new FacingChoice.Fixed(BattleFacing.North), after, "fresh");
        });
        Assert.True(result.Accepted); Assert.Equal(1, calls); Assert.Equal("fresh", result.Diagnostic);
        Assert.Equal(state.Revision + 1, result.State.Revision); Assert.Null(result.State.PendingFacing);
        Assert.Equal(new FftBiosRandom().Next(state.Battle.TurnBattle.Turns.RngState).State, result.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(result.State.Battle.TurnBattle.Turns.RngState, result.State.Battle.TurnBattle.Battle.RngState);
        Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, result.State.Battle.TurnBattle.Turns.Ticks);
        Assert.Equal(55, result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
        Assert.Equal(BattleFacing.North, result.State.Battle.Combatants.Single(u => u.UnitId == A).Facing);
    }

    [Fact]
    public void Old_proposals_cannot_authorize_an_identical_new_invocation_and_contexts_close()
    {
        var state = Active(); FacingDecision<string>? saved = null; FacingDecisionContext? expired = null;
        var first = FacingDecisionBattle.Execute(state, A, context =>
        {
            expired = context;
            return saved = context.PrepareWait(new FacingChoice.KeepCurrent(), context.RngState, "first");
        });
        Assert.True(first.Accepted);
        Assert.Throws<InvalidOperationException>(() => expired!.PrepareWait(new FacingChoice.KeepCurrent(), expired.RngState, "late"));
        Atomic(state, FacingDecisionBattle.Execute(state, A, _ => saved!), FacingDecisionRejection.InvalidDecision);
        var freshReplay = FacingDecisionBattle.Execute(state, A, c => c.PrepareWait(new FacingChoice.KeepCurrent(), c.RngState, "replay"));
        Assert.True(freshReplay.Accepted); Assert.Equal(first.State.Battle.Combatants.ToArray(), freshReplay.State.Battle.Combatants.ToArray());
    }

    [Fact]
    public void Rejected_plans_exceptions_and_wrong_actor_leave_input_unmodified()
    {
        var state = Active(); var rng = state.Battle.TurnBattle.Turns.RngState; FacingDecisionContext? expired = null;
        Atomic(state, FacingDecisionBattle.Execute<string>(state, A, c => c.Reject<string>()), FacingDecisionRejection.DecisionRejected);
        Atomic(state, FacingDecisionBattle.Execute<string>(state, A, _ => null!), FacingDecisionRejection.InvalidDecision);
        Assert.Throws<InvalidOperationException>(() => FacingDecisionBattle.Execute<string>(state, A, c =>
        { expired = c; throw new InvalidOperationException("planner failed"); }));
        Assert.Throws<InvalidOperationException>(() => expired!.Reject<string>());
        var wrong = FacingDecisionBattle.Execute(state, A, c => c.PrepareAttack(new(B, A), new(Weapon, 5), c.RngState, "wrong"));
        Atomic(state, wrong, FacingDecisionRejection.AuthorityRejected);
        Assert.Equal(FacingTurnRejection.WrongActor, wrong.Transition.FacingRejection);
        Assert.Equal(rng, state.Battle.TurnBattle.Turns.RngState);
    }

    [Fact]
    public void Invalid_RNG_words_or_backward_counts_never_reach_execution()
    {
        var state = Active(); var rng = state.Battle.TurnBattle.Turns.RngState;
        foreach (var invalid in new[] { new FftRandomState(rng.Value ^ 1, rng.DrawCount),
            new FftRandomState(rng.Value, rng.DrawCount - 1),
            new FftRandomState(new FftBiosRandom().Next(rng).State.Value ^ 1, rng.DrawCount + 1) })
            Atomic(state, FacingDecisionBattle.Execute(state, A, c => c.PrepareWait(new FacingChoice.KeepCurrent(), invalid, "bad")),
                FacingDecisionRejection.InvalidRandomProgress);
    }

    [Fact]
    public void Late_attack_exhaustion_discards_planning_RNG_and_all_success_diagnostics()
    {
        var state = Active(ulong.MaxValue - 2);
        var result = FacingDecisionBattle.Execute(state, A, c => c.PrepareAttack(new(A, B), new(Weapon, 5),
            new FftBiosRandom().Next(c.RngState).State, "not published"));
        Atomic(state, result, FacingDecisionRejection.CommandRejected);
        Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, result.Transition.AttackRejection);
        Assert.Equal(ulong.MaxValue - 1, state.Battle.TurnBattle.Turns.RngState.DrawCount);
        Assert.Equal(50, state.Battle.Combatants.Single(u => u.UnitId == B).Hp);
    }

    [Fact]
    public void Authority_rejection_happens_before_invoking_the_planner()
    {
        var state = Active(); bool called = false;
        var result = FacingDecisionBattle.Execute<string>(state, B, c => { called = true; return c.Reject<string>(); });
        Atomic(state, result, FacingDecisionRejection.AuthorityRejected); Assert.False(called);
        Assert.Equal(FacingTurnRejection.InactiveActor, result.Transition.FacingRejection);
    }

    [Fact]
    public void Logarithmic_checkpoint_verification_matches_draws_and_full_generator_periods()
    {
        var initial = new FftRandomState(0x80000001, 123); var actual = initial;
        Assert.True(FftBiosRandom.IsForwardCheckpoint(initial, initial));
        for (int i = 0; i < 10000; i++) actual = new FftBiosRandom().Next(actual).State;
        Assert.True(FftBiosRandom.IsForwardCheckpoint(initial, actual));
        // Odd increment and multiplier == 1 mod4 give the UInt32 LCG its full 2^32 period.
        Assert.True(FftBiosRandom.IsForwardCheckpoint(initial, new(initial.Value, initial.DrawCount + (1UL << 32))));
        Assert.True(FftBiosRandom.IsForwardCheckpoint(new(1, 0), new(3945894376, ulong.MaxValue)));
        Assert.False(FftBiosRandom.IsForwardCheckpoint(new(1, 0), new(1, ulong.MaxValue)));
    }

    private static void Atomic(FacingTurnState state, FacingDecisionResult<string> result, FacingDecisionRejection reason)
    { Assert.False(result.Accepted); Assert.Equal(reason, result.Rejection); Assert.Same(state, result.State);
        Assert.Same(state, result.Transition.State); Assert.Empty(result.Events); Assert.Null(result.Diagnostic); }

    private static FacingTurnState Active(ulong count = 7)
    {
        var rng = new FftRandomState(1, count);
        var map = new BattleMapDefinition(4, 3, 1, Enumerable.Repeat(new BattleTile(Ground, 10), 12).ToArray());
        var movement = new UnitMovementProfile(3, 2, 6, UnitMovementMode.Normal, UnitMovementTraits.None, []);
        var battle = new BattleState(map, [new(A, new(1, 1, 0), movement), new(B, new(2, 1, 0), movement)], rng);
        var turns = new TurnCycleState([new(A, 20, 95, 2), new(B, 1, 0, 17)], rng);
        AttackUnitState Unit(BattleUnitId id) => new(id, 50, 50, 0, 0, 5, 0, 0, 0, 0, 70, 0, BattleFacing.West, AttackUnitStatus.None, 0, Weapon);
        var fresh = FacingTurnBattle.Begin(new(new(battle, turns), [Unit(A), Unit(B)]), new(Ground));
        var result = FacingTurnBattle.Advance(fresh); Assert.True(result.Accepted); return result.State;
    }
}
