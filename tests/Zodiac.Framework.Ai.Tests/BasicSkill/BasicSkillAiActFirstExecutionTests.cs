using Xunit;
using Zodiac.Framework.Random;
using SelfData = Zodiac.Framework.Ai.Tests.AccumulateAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class BasicSkillAiActFirstExecutionTests
{
    private static readonly BasicSkillAiSelectionProfile Selection = new(true, true, true);
    private static readonly SwordAiCommandDispatchProfile Dispatch = new(true, true, true, true);

    [Fact]
    public void Fresh_Accumulate_selection_executes_real_PA_at_the_actual_origin_without_copying_predicted_D1_or_clock()
    {
        var state = Ready(); var before = Snapshot(state.Battle); var result = Execute(state); var o = Success(result);
        Assert.Equal(new RandomState(3794901700, 26), o.DecisionRng);
        Assert.Equal(BasicSkillAiPlanKind.Accumulate, o.Decision.Plan.Kind); Assert.Equal(Far, o.Decision.Plan.Destination);
        Assert.Equal(new ImmediateAbilityResolvedEvent(A, SelfData.Self, 5, 6), Assert.Single(result.Events.OfType<ImmediateAbilityResolvedEvent>()));
        Assert.Equal(new AttackPhysicalAttackChangedEvent(A, 5, 6), Assert.Single(result.Events.OfType<AttackPhysicalAttackChangedEvent>()));
        Assert.Equal(6, result.State.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
        Assert.Equal(o.DecisionRng, result.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(o.DecisionRng, result.State.Battle.TurnBattle.Battle.RngState);
        Assert.Equal(Origin, Position(result.State, A)); OpenMove(state, result.State);
        var pure = BasicSkillAiSelection.Evaluate(state.Battle, SelfData.Request(), Scenario(), Profile(), Selection, state.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(Serialize(pure.Outcome), Serialize(o.Decision)); Assert.Equal(pure.RngState, o.DecisionRng);
        Assert.Empty(result.Events.OfType<VoluntaryPathCompletedEvent>()); Assert.Null(result.Execution.Continuation);
        Assert.Equal(before, Snapshot(state.Battle));
    }

    [Fact]
    public void At_PA99_actual_self_ACT_is_zero_draw_and_resolves_without_a_changed_event()
    {
        var state = Ready(pa: 99, word: 5); var scenario = new ThrowStoneAiScanScenario([Origin], [], Origin, Origin);
        var result = Execute(state, scenario: scenario); var o = Success(result);
        Assert.Equal(BasicSkillAiPlanKind.Accumulate, o.Decision.Plan.Kind);
        Assert.Equal(new RandomState(2815492134, 12), o.DecisionRng);
        Assert.Equal(o.DecisionRng, result.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(new ImmediateAbilityResolvedEvent(A, SelfData.Self, 99, 99), Assert.Single(result.Events.OfType<ImmediateAbilityResolvedEvent>()));
        Assert.Empty(result.Events.OfType<AttackPhysicalAttackChangedEvent>()); OpenMove(state, result.State);
    }

    [Fact]
    public void Real_Attack_and_ACT_first_Stone_reuse_existing_resolution_instead_of_projected_damage()
    {
        var state = Ready(evade: 0); var attack = Execute(state); var a = Success(attack);
        Assert.Equal(BasicSkillAiPlanKind.Attack, a.Decision.Plan.Kind);
        Assert.Equal(new RandomState(3791773014, 24), a.DecisionRng);
        Assert.Equal(new AttackResolvedEvent(A, B, true, false, 10), Assert.Single(attack.Events.OfType<AttackResolvedEvent>()));
        Assert.Equal(new RandomState(1343201072, 30), attack.State.Battle.TurnBattle.Turns.RngState); OpenMove(state, attack.State);
        var ranged = Ready(evade: 0, target: new(4, 1, 0));
        var stone = Execute(ranged, scenario: new([Origin], [Origin], Origin, Origin)); var s = Success(stone);
        Assert.Equal(BasicSkillAiPlanKind.ThrowStone, s.Decision.Plan.Kind);
        Assert.Equal(new RandomState(3210001534, 16), s.DecisionRng);
        Assert.Equal(new ThrowStoneResolvedEvent(A, Stone, B, true, 5, true), Assert.Single(stone.Events.OfType<ThrowStoneResolvedEvent>()));
        Assert.Equal(new TilePosition(5, 1, 0), Position(stone.State, B));
        Assert.Equal(new RandomState(3297241969, 23), stone.State.Battle.TurnBattle.Turns.RngState); OpenMove(ranged, stone.State);
    }

    [Fact]
    public void Published_self_PA_survives_Wait_and_clock_advances_and_changes_a_later_actual_Attack()
    {
        var cast = Execute(Ready()); Success(cast);
        var ended = FacingTurnBattle.WaitWithFacing(cast.State, A, new FacingChoice.KeepCurrent()); Assert.True(ended.Accepted);
        var peer = FacingTurnBattle.Advance(ended.State); Assert.True(peer.Accepted); Assert.Equal(B, peer.State.Battle.TurnBattle.Turns.ActiveUnitId);
        // The initial high front evade selects Accumulate. Turn the peer away so this later hit proves PA, not evade luck.
        var peerEnded = FacingTurnBattle.WaitWithFacing(peer.State, B, new FacingChoice.Fixed(BattleFacing.East)); Assert.True(peerEnded.Accepted);
        var actor = FacingTurnBattle.Advance(peerEnded.State); Assert.True(actor.Accepted); Assert.Equal(A, actor.State.Battle.TurnBattle.Turns.ActiveUnitId);
        var attack = FacingTurnBattle.Attack(actor.State, new(A, B), SelfData.Weapon); Assert.True(attack.Accepted);
        Assert.Equal(6, actor.State.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
        Assert.Equal(new AttackResolvedEvent(A, B, true, false, 12), Assert.Single(attack.Events.OfType<AttackResolvedEvent>()));
        Assert.Equal(new RandomState(1116175964, 34), attack.State.Battle.TurnBattle.Turns.RngState);
    }

    [Fact]
    public void Real_lethal_Attack_publishes_the_terminal_result_without_inventing_post_ACT_movement_or_an_enemy()
    {
        var state = Ready(evade: 0);
        var result = Execute(state, request: SelfData.Request() with { Weapon = new(SelfData.SwordId, 10) }); var o = Success(result);
        Assert.Equal(BasicSkillAiPlanKind.Attack, o.Decision.Plan.Kind);
        Assert.Equal(AttackUnitStatus.Dead, result.State.Battle.Combatants.Single(u => u.UnitId == B).Status);
        Assert.Equal(0, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        OpenMove(state, result.State); Assert.Null(result.Execution.Continuation);
        Assert.Empty(result.Events.OfType<VoluntaryPathCompletedEvent>()); Assert.Empty(result.Events.OfType<FacingSettledEvent>());
        Atomic(result.State, Execute(result.State), BasicSkillAiActFirstRejection.UnavailableChoices);
    }

    [Fact]
    public void Exact_selection_budget_leaves_no_actual_self_draw_and_short_or_late_damage_failure_rolls_back_everything()
    {
        var state = Ready(count: ulong.MaxValue - 19); var exact = Execute(state); Success(exact);
        Assert.Equal(ulong.MaxValue, exact.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        var shortState = Ready(count: ulong.MaxValue - 18); Atomic(shortState, Execute(shortState), BasicSkillAiActFirstRejection.SelectionRejected);
        // Attack selection fits, but the actual six resolution draws do not.
        var late = Ready(evade: 0, count: ulong.MaxValue - 22); var before = Snapshot(late.Battle);
        var failed = Execute(late); Atomic(late, failed, BasicSkillAiActFirstRejection.BoundaryRejected);
        Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, failed.Execution.Transition.AttackRejection);
        Assert.Equal(before, Snapshot(late.Battle));
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Move_first_empty_plans_unavailable_choices_context_or_source_flags_publish_no_partial_decision()
    {
        var state = Ready(evade: 0, target: new(4, 1, 0)); Atomic(state, Execute(state), BasicSkillAiActFirstRejection.ActFirstRequired);
        var empty = Ready(target: new(7, 7, 0)); Atomic(empty, Execute(empty), BasicSkillAiActFirstRejection.ActFirstRequired);
        foreach (var dispatch in new[] { Dispatch with { InitializedActorContext = false }, Dispatch with { PresentCompactRecords = false },
            Dispatch with { CanonicalCoordinates = false }, Dispatch with { OrdinaryContinuation = false } })
            Atomic(state, Execute(state, dispatch: dispatch), BasicSkillAiActFirstRejection.UnsupportedDispatchContext);
        var moved = FacingTurnPathMovement.MoveAlongPath(Ready(), new(A, Far), new(Ground, A, B)); Assert.True(moved.Accepted);
        Atomic(moved.State, Execute(moved.State), BasicSkillAiActFirstRejection.UnavailableChoices);
        var acted = Execute(Ready()); Success(acted); Atomic(acted.State, Execute(acted.State), BasicSkillAiActFirstRejection.UnavailableChoices);
        Atomic(state, Execute(state, profile: Profile() with { SourceGeneralEntry = false }), BasicSkillAiActFirstRejection.SelectionRejected);
        var wrong = BasicSkillAiActFirstExecution.Execute(state, SelfData.Request(second: true), Scenario(), Profile(), Selection, Dispatch);
        Atomic(state, wrong, BasicSkillAiActFirstRejection.BoundaryRejected);
        Assert.Equal(FacingTurnRejection.InactiveActor, wrong.Execution.Transition.FacingRejection);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_teams_actor_identities_and_collection_orders_replay_without_mutating_live_input()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        foreach (bool second in new[] { false, true })
        {
            var state = Ready(second: second); var before = Snapshot(state.Battle);
            var first = Execute(state, team: team, second: second); Success(first);
            var reverse = Execute(Ready(second: second, reverse: true), team: team, second: second); Success(reverse);
            Assert.Equal(Serialize(first.Outcome), Serialize(reverse.Outcome)); Assert.Equal(Snapshot(first.State.Battle), Snapshot(reverse.State.Battle));
            Assert.Equal(Snapshot(first.State.Battle), Snapshot(Execute(state, team: team, second: second).State.Battle)); Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    private static BasicSkillAiActFirstResult Execute(FacingTurnState state, ThrowStoneAiScanScenario? scenario = null,
        BasicSkillAiProfile? profile = null, SwordAiCommandDispatchProfile? dispatch = null,
        SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, bool second = false, AccumulateAiPreparationRequest? request = null) =>
        BasicSkillAiActFirstExecution.Execute(state, request ?? SelfData.Request(second), scenario ?? Scenario(), profile ?? Profile(team), Selection, dispatch ?? Dispatch);
    private static BasicSkillAiActFirstOutcome Success(BasicSkillAiActFirstResult result)
    { Assert.True(result.Accepted, result.Failure?.ToString()); Assert.Null(result.Failure); return Assert.IsType<BasicSkillAiActFirstOutcome>(result.Outcome); }
    private static void Atomic(FacingTurnState state, BasicSkillAiActFirstResult result, BasicSkillAiActFirstRejection reason)
    {
        Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events); Assert.Null(result.Outcome);
        Assert.Null(result.Execution.Path); Assert.Null(result.Execution.Continuation); Assert.Equal(reason, result.Failure!.Reason);
    }
    private static TilePosition Position(FacingTurnState state, BattleUnitId id) => state.Battle.TurnBattle.Battle.Units.Single(u => u.Id == id).Position;
    private static void OpenMove(FacingTurnState before, FacingTurnState after)
    {
        var actorId = before.Battle.TurnBattle.Turns.ActiveUnitId!.Value; var actor = after.Battle.TurnBattle.Turns.Units.Single(u => u.Id == actorId);
        Assert.True(actor.ActionTaken); Assert.False(actor.MovementTaken); Assert.Equal(15, actor.Ct);
        Assert.Equal(actorId, after.Battle.TurnBattle.Turns.ActiveUnitId); Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.Battle.TurnBattle.Turns.Ticks, after.Battle.TurnBattle.Turns.Ticks); Assert.Null(after.PendingFacing);
    }
}