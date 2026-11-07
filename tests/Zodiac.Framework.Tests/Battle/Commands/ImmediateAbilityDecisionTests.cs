using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Tests.TargetedAbilityDecisionTests;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class ImmediateAbilityDecisionTests
{
    private static readonly AssetId Self = new("sample:ability/accumulate");
    private static ImmediateAbilityBattleProfile Profile(bool granted = true) =>
        new([new(Self)], [new(A, granted ? [Self] : [], 0), new(B, granted ? [Self] : [], 0)]);

    [Fact]
    public void Fresh_transaction_adopts_only_planning_RNG_then_publishes_actual_PA_and_one_revision()
    {
        var state = Ready(); var before = Snapshot(state); int calls = 0;
        var result = FacingDecisionBattle.Execute(state, A, context => {
            calls++; Assert.Same(state.Battle, context.Battle);
            return context.PrepareImmediateAbility(new(A, Self), Profile(), PlanRng(context.RngState), "fresh");
        });
        Assert.True(result.Accepted); Assert.Equal(1, calls); Assert.Equal("fresh", result.Diagnostic);
        Assert.Equal(new RandomState(3210001534, 16), result.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(result.State.Battle.TurnBattle.Turns.RngState, result.State.Battle.TurnBattle.Battle.RngState);
        Assert.Equal(new ImmediateAbilityResolvedEvent(A, Self, 5, 6), Assert.Single(result.Events.OfType<ImmediateAbilityResolvedEvent>()));
        Assert.Equal(new AttackPhysicalAttackChangedEvent(A, 5, 6), Assert.Single(result.Events.OfType<AttackPhysicalAttackChangedEvent>()));
        Assert.Equal(state.Revision + 1, result.State.Revision); Assert.Null(result.State.PendingFacing);
        var actor = result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A);
        Assert.Equal((15, true, false, A), (actor.Ct, actor.ActionTaken, actor.MovementTaken, result.State.Battle.TurnBattle.Turns.ActiveUnitId));
        Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, result.State.Battle.TurnBattle.Turns.Ticks);
        Assert.Equal(Position(state, A), Position(result.State, A)); Assert.Equal(before, Snapshot(state));
    }

    [Theory]
    [InlineData(98, 99)] [InlineData(99, 99)]
    public void Zero_resolution_draws_can_finish_at_draw_MAX_including_the_PA_cap(int pa, int expected)
    {
        var state = Ready(count: ulong.MaxValue - 1, pa: pa); var result = Cast(state, plan: false);
        Assert.True(result.Accepted); Assert.Equal(ulong.MaxValue, result.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        Assert.Equal(new ImmediateAbilityResolvedEvent(A, Self, pa, expected), Assert.Single(result.Events.OfType<ImmediateAbilityResolvedEvent>()));
        Assert.Equal(expected, result.State.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
        Assert.Equal(pa != expected, result.Events.OfType<AttackPhysicalAttackChangedEvent>().Any());
        Assert.Equal(state.Battle.TurnBattle.Turns.RngState, result.State.Battle.TurnBattle.Turns.RngState);
    }

    [Fact]
    public void Already_moved_completion_binds_final_PA_and_explicit_facing_preserves_the_boost()
    {
        var state = Moved(Ready()); var before = Snapshot(state); var cast = Cast(state);
        Assert.True(cast.Accepted); var receipt = Assert.IsType<FacingCompletionReceipt>(cast.State.PendingFacing);
        Assert.Same(cast.State.Battle, receipt.ExpectedBattle); Assert.Equal(6, receipt.ExpectedBattle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
        Assert.Null(cast.State.Battle.TurnBattle.Turns.ActiveUnitId);
        Assert.Equal(15, cast.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
        var otherBranch = Cast(state);
        Assert.Equal(FacingTurnRejection.StaleCompletion,
            FacingTurnBattle.FinishFacing(otherBranch.State, A, receipt, new FacingChoice.KeepCurrent()).FacingRejection);
        var faced = FacingTurnBattle.FinishFacing(cast.State, A, receipt, new FacingChoice.Fixed(BattleFacing.North));
        Assert.True(faced.Accepted); Assert.Equal(6, faced.State.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
        Assert.Equal(cast.State.Battle.TurnBattle.Turns, faced.State.Battle.TurnBattle.Turns); Assert.Single(faced.Events);
        Assert.Equal(cast.State.Revision + 1, faced.State.Revision); Assert.Null(faced.State.PendingFacing); Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Actual_grants_effect_domain_actor_and_spent_Act_reject_without_publishing_adoption()
    {
        var state = Ready(); var before = Snapshot(state);
        var noGrant = Cast(state, profile: Profile(false)); Atomic(state, noGrant, FacingDecisionRejection.CommandRejected);
        Assert.Equal(AttackCommandRejection.AbilityNotGranted, noGrant.Transition.AttackRejection);
        var missing = Cast(state, command: new(A, new("sample:ability/missing"))); Atomic(state, missing, FacingDecisionRejection.CommandRejected);
        Assert.Equal(AttackCommandRejection.UnknownAbility, missing.Transition.AttackRejection);
        var unsupported = Ready(pa: 0); Atomic(unsupported, Cast(unsupported), FacingDecisionRejection.CommandRejected);
        var wrong = Cast(state, command: new(B, Self)); Atomic(state, wrong, FacingDecisionRejection.AuthorityRejected);
        Assert.Equal(FacingTurnRejection.WrongActor, wrong.Transition.FacingRejection);
        var acted = Cast(state); Assert.True(acted.Accepted);
        var repeated = Cast(acted.State); Atomic(acted.State, repeated, FacingDecisionRejection.CommandRejected);
        Assert.Equal(AttackCommandRejection.ActionAlreadyTaken, repeated.Transition.AttackRejection); Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Self_proposals_remain_invocation_bound_and_invalid_progress_or_expired_contexts_never_commit()
    {
        var state = Ready(); var before = Snapshot(state); FacingDecision<string>? saved = null; FacingDecisionContext? expired = null;
        var first = FacingDecisionBattle.Execute(state, A, c => { expired = c;
            return saved = c.PrepareImmediateAbility(new(A, Self), Profile(), c.RngState, "saved"); });
        Assert.True(first.Accepted);
        Atomic(state, FacingDecisionBattle.Execute(state, A, _ => saved!), FacingDecisionRejection.InvalidDecision);
        Assert.Throws<InvalidOperationException>(() => expired!.PrepareImmediateAbility(new(A, Self), Profile(), expired.RngState, "closed"));
        FacingDecisionContext? throwing = null;
        Assert.Throws<InvalidOperationException>(() => FacingDecisionBattle.Execute<string>(state, A, c => { throwing = c; throw new InvalidOperationException(); }));
        Assert.Throws<InvalidOperationException>(() => throwing!.PrepareImmediateAbility(new(A, Self), Profile(), throwing.RngState, "closed"));
        var rng = state.Battle.TurnBattle.Turns.RngState;
        Atomic(state, FacingDecisionBattle.Execute(state, A, c => c.PrepareImmediateAbility(new(A, Self), Profile(), rng with { Value = rng.Value ^ 1 }, "bad")),
            FacingDecisionRejection.InvalidRandomProgress);
        Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Revision_and_pending_receipt_guards_precede_planning_and_completion_uses_the_actual_final_state()
    {
        var state = Moved(Ready()); var last = new FacingTurnState(state.Battle, state.Profile, ulong.MaxValue - 1, null, state.Lineage);
        var cast = Cast(last, plan: false); Assert.True(cast.Accepted); Assert.Equal(ulong.MaxValue, cast.State.Revision);
        Assert.Equal(FacingTurnRejection.RevisionExhausted,
            FacingTurnBattle.FinishFacing(cast.State, A, cast.State.PendingFacing, new FacingChoice.KeepCurrent()).FacingRejection);
        foreach (var guarded in new[] { new FacingTurnState(state.Battle, state.Profile, ulong.MaxValue, null, state.Lineage), Cast(state).State })
        {
            int calls = 0; var failed = FacingDecisionBattle.Execute(guarded, A, c => { calls++; return c.PrepareImmediateAbility(new(A, Self), Profile(), c.RngState, "bad"); });
            Atomic(guarded, failed, FacingDecisionRejection.AuthorityRejected); Assert.Equal(0, calls);
        }
        var direct = FacingTurnBattle.UseImmediateAbility(state, new(A, Self), Profile()); Assert.True(direct.Accepted);
        Assert.Same(direct.State.Battle, direct.State.PendingFacing!.ExpectedBattle);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_and_input_orders_replay_without_mutating_live_input()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = Ready(second: second); var before = Snapshot(state); var first = Cast(state);
            Assert.True(first.Accepted); Assert.Equal(Snapshot(first.State), Snapshot(Cast(state).State));
            Assert.Equal(Snapshot(first.State), Snapshot(Cast(Ready(second: second, reverse: true)).State)); Assert.Equal(before, Snapshot(state));
        }
    }

    private static FacingDecisionResult<string> Cast(FacingTurnState state, bool plan = true,
        UseImmediateAbilityCommand? command = null, ImmediateAbilityBattleProfile? profile = null)
    {
        var actor = state.Battle.TurnBattle.Turns.ActiveUnitId!.Value;
        return FacingDecisionBattle.Execute(state, actor, c => c.PrepareImmediateAbility(command ?? new(actor, Self),
            profile ?? Profile(), plan ? PlanRng(c.RngState) : c.RngState, "self"));
    }
    private static FacingTurnState Moved(FacingTurnState state)
    {
        var result = FacingTurnPathMovement.MoveAlongPath(state, new(A, new(3, 1, 0)), new(Ground, A, B)); Assert.True(result.Accepted); return result.State;
    }
    private static void Atomic(FacingTurnState state, FacingDecisionResult<string> result, FacingDecisionRejection reason)
    {
        Assert.False(result.Accepted); Assert.Equal(reason, result.Rejection); Assert.Same(state, result.State);
        Assert.Empty(result.Events); Assert.Null(result.Diagnostic); Assert.Null(result.Path); Assert.Null(result.Continuation);
    }
}
