using Xunit;
using Zodiac.Framework.Random;
using SelfData = Zodiac.Framework.Ai.Tests.AccumulateAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class BasicSkillAiPostActTests
{
    private static readonly SwordAiCommandDispatchProfile Dispatch = new(true, true, true, true);
    private static BasicSkillAiPostActConfiguration Configuration(SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue) =>
        new(SelfData.Configuration(team), new(true, true, true), true, true, true);
    private static FacingTurnState Self(FacingTurnState? ready = null, bool second = false)
    {
        var actual = FacingTurnBattle.UseImmediateAbility(ready ?? Ready(second: second), new(second ? B : A, SelfData.Self), SelfData.Profile().Ability);
        Assert.True(actual.Accepted); return actual.State;
    }
    private static AccumulateAiPreparationResult Prepare(FacingTurnState state, AccumulateAiPreparationConfiguration? config = null, bool second = false) =>
        AccumulateAiPreparation.EvaluateAfterAct(state.Battle, SelfData.Request(second), config ?? SelfData.Configuration(), state.Battle.TurnBattle.Turns.RngState);
    private static BasicSkillAiPostActResult Select(FacingTurnState state, bool outside = false, BasicSkillAiPostActConfiguration? config = null, bool second = false) => outside
        ? BasicSkillAiPostActSelection.EvaluateOutOfReach(state.Battle, SelfData.Request(second), config ?? Configuration(), state.Battle.TurnBattle.Turns.RngState)
        : BasicSkillAiPostActSelection.EvaluateInReach(state.Battle, SelfData.Request(second), config ?? Configuration(), state.Battle.TurnBattle.Turns.RngState);
    private static BasicSkillAiPostActExecutionResult Execute(FacingTurnState state, bool outside = false, BasicSkillAiPostActConfiguration? config = null,
        SwordAiCommandDispatchProfile? dispatch = null, bool second = false) => outside
        ? BasicSkillAiPostActExecution.ExecuteOutOfReach(state, SelfData.Request(second), config ?? Configuration(), dispatch ?? Dispatch)
        : BasicSkillAiPostActExecution.ExecuteInReach(state, SelfData.Request(second), config ?? Configuration(), dispatch ?? Dispatch);

    [Fact]
    public void Actual_self_boost_and_cap_feed_one_refresh_with_retained_counts_and_all_actor_entries_cleared()
    {
        foreach (var pa in new[] { 5, 99 })
        {
            var state = Self(Ready(pa: pa)); var before = Snapshot(state.Battle); var result = Prepare(state);
            Assert.True(result.Accepted, result.Failure?.ToString()); var o = result.Outcome!;
            Assert.Equal(new RandomState(1103527590, 8), result.RngState); Assert.Equal(35, o.Common.Refresh.TurnEndCt);
            Assert.Equal(4, o.Common.EnemyAbilityReach); Assert.Equal(3, o.Common.AllyAbilityReach);
            Assert.False(o.Common.ActorAttackUsable); Assert.All(o.ActorEntries, e => Assert.False(e.Usable));
            Assert.Equal(new[] { AccumulateAiEntryKind.Attack, AccumulateAiEntryKind.Accumulate, AccumulateAiEntryKind.ThrowStone }, o.ActorEntries.Select(e => e.Kind));
            Assert.All(o.Units, u => {
                Assert.Equal(4, u.InitializedEntryCount); Assert.Equal(2, u.EvadeQuarterShare);
                Assert.Equal(0, u.SilenceQuarterShare); Assert.Equal(0, u.MpQuarterShare);
                Assert.Equal(2, u.HarmfulCount); Assert.Equal(1, u.HelpfulCount); Assert.True(u.HasRangedAbility); Assert.False(u.CanHealAllies);
            });
            Assert.False(o.Units.Single(u => u.UnitId == A).MostlySupport); Assert.Null(o.Units.Single(u => u.UnitId == B).MostlySupport);
            Assert.Equal(2, o.EnemyHarmfulCount); Assert.Equal(2, o.EnemyPhysicalCount);
            Assert.Equal(Math.Min(99, pa + 1), state.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
            Assert.Equal(before, Snapshot(state.Battle)); Assert.Equal(new RandomState(1, 7), state.Battle.TurnBattle.Turns.RngState);
        }
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Spent_ACT_never_bypasses_self_and_Stone_grants_setup_or_choice_and_cache_guards()
    {
        var state = Self(); var original = SelfData.Configuration();
        foreach (var id in new[] { A, B })
        {
            var selfAbility = new ImmediateAbilityBattleProfile([new(SelfData.Self)], [new(A, id == A ? [] : [SelfData.Self], 0), new(B, id == B ? [] : [SelfData.Self], 0)]);
            var self = original with { Accumulate = new(selfAbility, original.Accumulate.Movement, original.Accumulate.Units) };
            var rejected = Prepare(state, self); Assert.False(rejected.Accepted); Assert.Null(rejected.Outcome);
            Assert.Equal(AccumulateAiCandidateRejection.AbilityNotGranted, rejected.Failure!.AccumulateReason);
            var stoneAbility = new TargetedAbilityBattleProfile(Ground, [new(Stone)], [new(A, id == A ? [] : [Stone], 0), new(B, id == B ? [] : [Stone], 0)]);
            var stone = original with { ThrowStone = new(stoneAbility, original.ThrowStone.Movement, original.ThrowStone.Units) };
            rejected = Prepare(state, stone); Assert.False(rejected.Accepted); Assert.Null(rejected.Outcome);
            Assert.Equal(AttackCommandRejection.AbilityNotGranted, rejected.Failure!.ThrowStoneReason);
            Assert.Equal(AttackCommandRejection.ActionAlreadyTaken,
                TargetedAbilityBattleSimulator.ValidateCommand(state.Battle, new(A, Stone, B), stoneAbility));
        }
        Assert.Equal(AccumulateAiCandidateRejection.UnavailableAction, Prepare(Ready()).Failure!.AccumulateReason);
        Assert.Equal(SwordAiRefreshRejection.UnsupportedRefreshProfile,
            Prepare(state, original with { Refresh = original.Refresh with { FreshRefreshEntry = false } }).Failure!.RefreshReason);
        var moved = FacingTurnPathMovement.MoveAlongPath(state, new(A, Far), original.Attack.Movement); Assert.True(moved.Accepted);
        Assert.False(Prepare(moved.State).Accepted);
        var blue = original with { Team = SwordAiScenarioTeam.Blue, Preparation = original.Preparation with { BlueAiControlled = false } };
        Assert.Equal(AccumulateAiPreparationRejection.UnsupportedTeam, Prepare(state, blue).Failure!.Reason);
    }

    [Fact]
    public void Actual_Attack_damage_and_Stone_push_use_current_HP_and_focus_while_terminal_KO_rejects_without_rewinding_ACT()
    {
        var attack = FacingTurnBattle.Attack(Ready(evade: 0), new(A, B), SelfData.Weapon); Assert.True(attack.Accepted);
        Assert.Equal(40, attack.State.Battle.Combatants.Single(u => u.UnitId == B).Hp); Assert.True(Prepare(attack.State).Accepted);
        var afterAttack = Select(attack.State); Assert.True(afterAttack.Accepted);
        Assert.All(afterAttack.Outcome!.MovementScenario.Movement, m => Assert.Equal(40, m.Outcome.Units.Single(u => u.UnitId == B).Hp));
        var stone = FacingTurnBattle.UseTargetedAbility(Ready(evade: 0, word: 2711922929, count: 20), new(A, Stone, B), CandidateProfile().Ability);
        Assert.True(stone.Accepted); Assert.Single(stone.Events.OfType<ThrowStoneKnockbackEvent>());
        var focus = Prepare(stone.State); Assert.True(focus.Accepted, focus.Failure?.ToString());
        Assert.Equal(new TilePosition(3, 1, 0), focus.Outcome!.Common.FocusPosition);
        Assert.DoesNotContain(focus.Outcome.Common.FocusPosition, focus.Outcome.Common.ReachableOrigins);
        var afterPush = Select(stone.State); Assert.True(afterPush.Accepted); Assert.Equal(2, afterPush.Outcome!.MovementScenario.TargetDistance);
        var killed = FacingTurnBattle.Attack(Ready(evade: 0), new(A, B), new(SelfData.SwordId, 10)); Assert.True(killed.Accepted);
        var before = Snapshot(killed.State.Battle); var rejected = Prepare(killed.State);
        Assert.False(rejected.Accepted); Assert.Equal(AccumulateAiCandidateRejection.TerminalInput, rejected.Failure!.AccumulateReason);
        Assert.Null(rejected.Outcome); Assert.Equal(killed.State.Battle.TurnBattle.Turns.RngState, rejected.RngState);
        Atomic(killed.State, Execute(killed.State), BasicSkillAiPostActExecutionRejection.SelectionRejected); Assert.Equal(before, Snapshot(killed.State.Battle));
    }

    [Fact]
    public void Refresh_exact_budget_and_exhausted_draw_or_tick_budget_restore_the_pure_query()
    {
        var state = Self(Ready(count: ulong.MaxValue - 1)); var exact = Prepare(state); Assert.True(exact.Accepted);
        Assert.Equal(ulong.MaxValue, exact.RngState.DrawCount);
        var exhausted = Self(Ready(count: ulong.MaxValue)); var failed = Prepare(exhausted);
        Assert.False(failed.Accepted); Assert.Null(failed.Outcome); Assert.Equal(exhausted.Battle.TurnBattle.Turns.RngState, failed.RngState);
        var t = exhausted.Battle.TurnBattle;
        var ticks = new AttackBattleSnapshot(new(new(t.Battle.Map, t.Battle.Units, new(1, 7)),
            new(t.Turns.Units, new(1, 7), ulong.MaxValue, t.Turns.Phase)), exhausted.Battle.Combatants);
        failed = AccumulateAiPreparation.EvaluateAfterAct(ticks, SelfData.Request(), SelfData.Configuration(), new(1, 7));
        Assert.False(failed.Accepted); Assert.Equal(SwordAiRefreshRejection.TickCountExhausted, failed.Failure!.RefreshReason);
        Assert.Equal(new RandomState(1, 7), failed.RngState); Assert.Null(failed.Outcome);
    }

    [Fact]
    public void Derived_in_reach_movement_and_empty_lottery_use_no_self_coin_or_action_scan()
    {
        var state = Self(); var before = Snapshot(state.Battle); var result = Select(state);
        Assert.True(result.Accepted, result.Failure?.ToString()); var o = result.Outcome!;
        Assert.Equal(new RandomState(1343201072, 30), result.RngState);
        Assert.Equal(new TilePosition(1, 4, 0), o.Plan.Destination); Assert.Equal(BasicSkillAiPlanKind.MoveOnly, o.Plan.Kind);
        Assert.Null(o.Plan.Action); Assert.False(o.Plan.ActBeforeMove); Assert.Equal(8, o.Lottery.Length); Assert.Null(o.OutOfReach);
        Assert.Equal(14, o.MovementScenario.Movement.Length); Assert.Equal(new RandomState(2111915288, 22), o.LotteryEntryRng);
        Assert.Equal(o.MovementScenario.Scenario.BaselineDestination, o.MovementScenario.Scenario.AttackDestination);
        Assert.Equal(35, o.MovementScenario.Movement.Single(m => m.Outcome.Destination == Origin).Outcome.TurnEndCt);
        Assert.All(o.MovementScenario.Movement.Where(m => m.Outcome.Destination != Origin), m => Assert.Equal(15, m.Outcome.TurnEndCt));
        Assert.Equal(before, Snapshot(state.Battle));
    }

    [Fact]
    public void Out_of_reach_NonBlue_adjusts_only_after_scoring_while_Blue_skips_that_draw()
    {
        var state = Self(Ready(move: 0, target: new(7, 7, 0)));
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        {
            var result = Select(state, outside: true, config: Configuration(team)); Assert.True(result.Accepted, result.Failure?.ToString());
            var o = result.Outcome!; Assert.Single(o.MovementScenario.Movement); Assert.Equal(Origin, o.Plan.Destination);
            Assert.Equal(12, o.MovementScenario.TargetDistance); Assert.Equal(1, o.MovementScenario.TargetReach);
            Assert.Equal(o.Plan.Destination, o.MovementScenario.Scenario.AttackDestination);
            if (team == SwordAiScenarioTeam.NonBlue)
            {
                Assert.NotNull(o.OutOfReach!.ReachAdjustment); Assert.Equal((uint)10113, o.OutOfReach.ReachAdjustment.Raw);
                Assert.Equal(new RandomState(180171308, 18), result.RngState);
            }
            else { Assert.Null(o.OutOfReach!.ReachAdjustment); Assert.Equal(new RandomState(267834847, 17), result.RngState); }
        }
        Assert.Equal(SwordAiMovementScenarioRejection.OutOfReach, Select(state).Failure!.Movement!.Reason);
        Assert.Equal(SwordAiMovementScenarioRejection.WrongBranch, Select(Self(), outside: true).Failure!.Movement!.Reason);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Selection_requires_fresh_policy_facts_and_short_lottery_discards_the_entire_owned_prefix()
    {
        var state = Self(); var c = Configuration();
        foreach (var bad in new[] { c with { SourceGeneralEntry = false }, c with { FreshEmptyRankedList = false }, c with { NoDefendSupport = false },
            c with { Scenarios = c.Scenarios with { NormalScoring = false } }, c with { Scenarios = c.Scenarios with { CanonicalCoordinates = false } },
            c with { Scenarios = c.Scenarios with { OrdinaryScenarioPolicy = false } } })
        {
            var result = Select(state, config: bad); Assert.False(result.Accepted); Assert.Null(result.Outcome);
            Assert.Equal(BasicSkillAiPostActStage.Admission, result.Failure!.Stage); Assert.Equal(new RandomState(1, 7), result.RngState);
        }
        var shortState = Self(Ready(count: ulong.MaxValue - 22)); var failed = Select(shortState);
        Assert.False(failed.Accepted); Assert.Null(failed.Outcome); Assert.Equal(BasicSkillAiPostActStage.Lottery, failed.Failure!.Stage);
        Assert.Equal(shortState.Battle.TurnBattle.Turns.RngState, failed.RngState);
    }

    [Fact]
    public void Actual_boosted_Move_and_facing_settle_without_another_ACT_token_refund_or_clock_advance()
    {
        var state = Self(); var before = Snapshot(state.Battle); var result = Execute(state); var o = Success(result);
        Assert.Equal(SwordAiDispatchKind.Move, o.Dispatch.Kind); Assert.Equal(new RandomState(1343201072, 30), o.DecisionRng);
        Assert.Equal(o.DecisionRng, result.State.Battle.TurnBattle.Turns.RngState); Assert.Equal(o.DecisionRng, result.State.Battle.TurnBattle.Battle.RngState);
        Assert.Equal(new[] { Origin, new TilePosition(1, 2, 0), new(1, 3, 0), new(1, 4, 0) }, result.Path!.Tiles.ToArray());
        Assert.Equal(6, result.State.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
        Assert.Equal(50, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.Equal(15, result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
        Assert.Equal(state.Revision + 2, result.State.Revision); Assert.Null(result.State.PendingFacing); Assert.Null(result.Execution.Continuation);
        Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, result.State.Battle.TurnBattle.Turns.Ticks);
        Assert.Single(result.Events.OfType<VoluntaryPathCompletedEvent>()); Assert.Single(result.Events.OfType<FacingSettledEvent>());
        Assert.Empty(result.Events.OfType<ImmediateAbilityResolvedEvent>()); Assert.Empty(result.Events.OfType<AttackResolvedEvent>());
        Assert.Empty(result.Events.OfType<ThrowStoneResolvedEvent>()); Assert.Equal(before, Snapshot(state.Battle));
        Assert.Equal(Serialize(Select(state).Outcome), Serialize(o.Selection));
    }

    [Fact]
    public void Fresh_ACT_first_self_command_then_fresh_post_ACT_selection_preserves_both_real_checkpoints()
    {
        var ready = Ready(); var cast = BasicSkillAiCommandExecution.Execute(ready, SelfData.Request(), Scenario(), Profile(), new(true, true, true), Dispatch);
        Assert.True(cast.Accepted); Assert.Equal(BasicSkillAiCommandKind.Accumulate, cast.Outcome!.Kind);
        Assert.Equal(new RandomState(3794901700, 26), cast.State.Battle.TurnBattle.Turns.RngState);
        var before = Snapshot(cast.State.Battle); var remaining = Execute(cast.State); var o = Success(remaining);
        Assert.Equal(new RandomState(995405759, 49), o.DecisionRng);
        Assert.Equal(6, remaining.State.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
        Assert.Equal(new TilePosition(1, 4, 0), remaining.State.Battle.TurnBattle.Battle.Units.Single(u => u.Id == A).Position);
        Assert.Null(remaining.State.PendingFacing); Assert.Null(remaining.State.Battle.TurnBattle.Turns.ActiveUnitId);
        Assert.Empty(remaining.Events.OfType<ImmediateAbilityResolvedEvent>()); Assert.Equal(before, Snapshot(cast.State.Battle));
    }

    [Fact]
    public void Same_tile_END_refunds_only_Move_caps_CT_and_preserves_actual_PA99()
    {
        foreach (int ct in new[] { 15, 55 })
        {
            var state = Self(Ready(move: 0, pa: 99, ct: ct)); var result = Execute(state); var o = Success(result);
            Assert.Equal(SwordAiDispatchKind.EndTurn, o.Dispatch.Kind); Assert.Null(result.Path);
            Assert.Equal(new RandomState(267834847, 17), o.DecisionRng);
            Assert.Equal(Math.Min(60, ct + 20), result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
            Assert.Equal(99, result.State.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
            Assert.Equal(state.Revision + 1, result.State.Revision); Assert.Null(result.State.PendingFacing);
            Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, result.State.Battle.TurnBattle.Turns.Ticks);
        }
    }

    [Fact]
    public void Exact_selection_budget_allows_zero_draw_settlement_and_late_facing_failure_preserves_the_published_ACT()
    {
        foreach (var (move, draws) in new[] { (3, 23ul), (0, 10ul) })
        {
            var result = Execute(Self(Ready(move: move, count: ulong.MaxValue - draws))); Success(result);
            Assert.Equal(ulong.MaxValue, result.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        }
        var state = Self(Ready(count: ulong.MaxValue - 22)); Atomic(state, Execute(state), BasicSkillAiPostActExecutionRejection.SelectionRejected);
        var acted = Self(); var late = new FacingTurnState(acted.Battle, acted.Profile, ulong.MaxValue - 1, null, acted.Lineage);
        var before = Snapshot(late.Battle);
        for (int i = 0; i < 2; i++)
        {
            var failed = Execute(late); Atomic(late, failed, BasicSkillAiPostActExecutionRejection.BoundaryRejected);
            Assert.Equal(FacingTurnRejection.RevisionExhausted, failed.Execution.Transition.FacingRejection);
        }
        Assert.Equal(before, Snapshot(late.Battle)); Assert.Equal(6, late.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
        Assert.True(late.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).ActionTaken);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Execution_rejects_wrong_choices_dispatch_context_and_pending_facing_without_partial_output()
    {
        var state = Self(); var fresh = Ready(); Atomic(fresh, Execute(fresh), BasicSkillAiPostActExecutionRejection.UnavailableChoices);
        foreach (var d in new[] { Dispatch with { InitializedActorContext = false }, Dispatch with { PresentCompactRecords = false },
            Dispatch with { CanonicalCoordinates = false }, Dispatch with { OrdinaryContinuation = false } })
            Atomic(state, Execute(state, dispatch: d), BasicSkillAiPostActExecutionRejection.UnsupportedDispatchContext);
        var wrong = Execute(state, second: true); Atomic(state, wrong, BasicSkillAiPostActExecutionRejection.BoundaryRejected);
        var pending = FacingTurnPathMovement.MoveAlongPath(state, new(A, Far), SelfData.Attack().Movement); Assert.True(pending.Accepted);
        Atomic(pending.State, Execute(pending.State), BasicSkillAiPostActExecutionRejection.BoundaryRejected);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_teams_actor_identities_and_collection_orders_replay_from_actual_post_ACT_input()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        foreach (bool second in new[] { false, true })
        foreach (bool outside in new[] { false, true })
        {
            var target = outside ? new TilePosition(7, 7, 0) : new(2, 1, 0);
            var state = Self(Ready(second: second, target: target), second); var before = Snapshot(state.Battle);
            var first = Execute(state, outside, Configuration(team), second: second); var o = Success(first);
            var reverse = Execute(Self(Ready(second: second, reverse: true, target: target), second), outside, Configuration(team), second: second);
            Success(reverse); Assert.Equal(Serialize(o), Serialize(reverse.Outcome)); Assert.Equal(Snapshot(first.State.Battle), Snapshot(reverse.State.Battle));
            Assert.Equal(6, first.State.Battle.Combatants.Single(u => u.UnitId == (second ? B : A)).PhysicalAttack);
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    private static BasicSkillAiPostActExecutionOutcome Success(BasicSkillAiPostActExecutionResult result)
    { Assert.True(result.Accepted, result.Failure?.ToString()); Assert.Null(result.Failure); return Assert.IsType<BasicSkillAiPostActExecutionOutcome>(result.Outcome); }
    private static void Atomic(FacingTurnState state, BasicSkillAiPostActExecutionResult result, BasicSkillAiPostActExecutionRejection reason)
    {
        Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events); Assert.Null(result.Outcome);
        Assert.Null(result.Path); Assert.Null(result.Execution.Continuation); Assert.Equal(reason, result.Failure!.Reason);
    }
}
