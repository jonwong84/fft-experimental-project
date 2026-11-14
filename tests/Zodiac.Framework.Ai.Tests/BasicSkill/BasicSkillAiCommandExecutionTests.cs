using Xunit;
using Zodiac.Framework.Random;
using SelfData = Zodiac.Framework.Ai.Tests.AccumulateAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class BasicSkillAiCommandExecutionTests
{
    private static readonly BasicSkillAiSelectionProfile Selection = new(true, true, true);
    private static readonly SwordAiCommandDispatchProfile Dispatch = new(true, true, true, true);
    private static readonly TilePosition Near = new(2, 1, 0);
    private static ThrowStoneAiScanScenario AttackScenario => new([Origin, Near], [Origin, Near], Origin, Near);
    private static ThrowStoneAiScanScenario EmptyMoveScenario => new([Origin, Far], [Origin, Far], Far, Far);

    [Fact]
    public void Fresh_Move_first_Stone_adopts_selection_once_then_retains_real_ACT_and_actual_receipt()
    {
        var state = Ready(evade: 0, target: new(4, 1, 0)); var before = Snapshot(state.Battle);
        var move = Execute(state, mode: 1); var o = Success(move, AiCommandKind.Move);
        Assert.Equal(BasicSkillAiPlanKind.ThrowStone, o.Decision.Plan.Kind);
        Assert.Equal(new RandomState(267834847, 17), o.DecisionRng);
        Assert.Equal(o.DecisionRng, move.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(o.DecisionRng, move.State.Battle.TurnBattle.Battle.RngState);
        Assert.Equal(new[] { Origin, Far }, move.Path!.Tiles.ToArray()); Assert.Equal(BattleFacing.West, move.Path.FinalHeading);
        Assert.NotNull(move.Continuation); Assert.Null(move.State.PendingFacing);
        Assert.Equal(50, move.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.Empty(move.Events.OfType<ThrowStoneResolvedEvent>()); Assert.Empty(move.Events.OfType<AttackResolvedEvent>());
        var turn = move.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A);
        Assert.True(turn.MovementTaken); Assert.False(turn.ActionTaken); Assert.Equal(15, turn.Ct);
        var pure = BasicSkillAiSelection.Evaluate(state.Battle, SelfData.Request(), Scenario(), Profile(), Selection,
            state.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(Serialize(pure.Outcome), Serialize(o.Decision)); Assert.Equal(pure.RngState, o.DecisionRng);
        var act = FacingMoveDecisionBattle.Continue(move.State, A, move.Continuation); Assert.True(act.Accepted);
        Assert.Equal(new ThrowStoneResolvedEvent(A, Stone, B, true, 10, false), Assert.Single(act.Events.OfType<ThrowStoneResolvedEvent>()));
        Assert.Equal(new RandomState(3791773014, 24), act.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(40, act.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.Same(act.State.Battle, act.State.PendingFacing!.ExpectedBattle);
        Assert.Equal(state.Revision + 2, act.State.Revision);
        var faced = FacingTurnBattle.FinishFacing(act.State, A, act.State.PendingFacing, new FacingChoice.ChooseClosedEnemy(B));
        Assert.True(faced.Accepted); Assert.Equal(state.Revision + 3, faced.State.Revision);
        Assert.Equal(act.State.Battle.TurnBattle, faced.State.Battle.TurnBattle); Assert.Equal(before, Snapshot(state.Battle));
    }

    [Fact]
    public void Move_first_Attack_retains_typed_Attack_and_uses_actual_damage_after_the_confirmed_path()
    {
        var state = Ready(evade: 0, target: new(3, 1, 0)); var move = Execute(state, AttackScenario, mode: 1);
        var o = Success(move, AiCommandKind.Move); Assert.Equal(BasicSkillAiPlanKind.Attack, o.Decision.Plan.Kind);
        Assert.Equal(new RandomState(3791773014, 24), o.DecisionRng); Assert.Equal(new[] { Origin, Near }, move.Path!.Tiles.ToArray());
        var actual = FacingMoveDecisionBattle.Continue(move.State, A, move.Continuation); Assert.True(actual.Accepted);
        Assert.Equal(new AttackResolvedEvent(A, B, true, false, 10), Assert.Single(actual.Events.OfType<AttackResolvedEvent>()));
        Assert.Empty(actual.Events.OfType<ThrowStoneResolvedEvent>());
        Assert.Equal(new RandomState(1343201072, 30), actual.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(15, actual.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct); Assert.NotNull(actual.State.PendingFacing);
    }

    [Fact]
    public void Different_tile_empty_ranking_retains_Wait_and_refunds_only_the_unused_ACT()
    {
        var state = Ready(target: new(7, 7, 0)); var move = Execute(state, EmptyMoveScenario, mode: 1);
        var o = Success(move, AiCommandKind.Move); Assert.Equal(BasicSkillAiPlanKind.MoveOnly, o.Decision.Plan.Kind);
        Assert.Equal(8, o.Decision.Lottery.Length); Assert.Equal(new RandomState(836760821, 19), o.DecisionRng);
        var ended = FacingMoveDecisionBattle.Continue(move.State, A, move.Continuation); Assert.True(ended.Accepted);
        Assert.Equal(o.DecisionRng, ended.State.Battle.TurnBattle.Turns.RngState);
        Assert.Null(ended.State.PendingFacing); Assert.Null(ended.State.Battle.TurnBattle.Turns.ActiveUnitId);
        Assert.Equal(35, ended.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
        Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, ended.State.Battle.TurnBattle.Turns.Ticks);
        Assert.Single(ended.Events.OfType<FacingSettledEvent>()); Assert.Empty(ended.Events.OfType<AttackResolvedEvent>());
        Assert.Equal(BattleFacing.East, ended.State.Battle.Combatants.Single(u => u.UnitId == A).Facing);
    }

    [Fact]
    public void Late_frozen_ACT_failure_preserves_published_Move_and_retry_while_short_selection_adopts_nothing()
    {
        var state = Ready(evade: 0, target: new(4, 1, 0), count: ulong.MaxValue - 9);
        Atomic(state, Execute(state, mode: 1), AiCommandRejection.SelectionRejected);
        var move = Execute(Ready(evade: 0, target: new(4, 1, 0), count: ulong.MaxValue - 16), mode: 1);
        var o = Success(move, AiCommandKind.Move); Assert.Equal(ulong.MaxValue - 6, o.DecisionRng.DrawCount);
        var before = Snapshot(move.State.Battle);
        for (int i = 0; i < 2; i++)
        {
            var failed = FacingMoveDecisionBattle.Continue(move.State, A, move.Continuation);
            Assert.False(failed.Accepted); Assert.Same(move.State, failed.State); Assert.Empty(failed.Events);
            Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, failed.Transition.AttackRejection);
        }
        Assert.Equal(before, Snapshot(move.State.Battle)); Assert.Equal(Far, move.State.Battle.TurnBattle.Battle.Units.Single(u => u.Id == A).Position);
        var cancelled = FacingTurnBattle.WaitWithFacing(move.State, A, new FacingChoice.KeepCurrent()); Assert.True(cancelled.Accepted);
        Assert.Equal(FacingMoveRejection.StaleContinuation, FacingMoveDecisionBattle.Continue(cancelled.State, A, move.Continuation).Rejection);
    }

    [Fact]
    public void Frozen_commands_bind_exact_returned_session_actor_lineage_and_revision()
    {
        var state = Ready(evade: 0, target: new(4, 1, 0)); var move = Execute(state, mode: 1); Success(move, AiCommandKind.Move);
        var replay = Execute(state, mode: 1); Success(replay, AiCommandKind.Move);
        var branch = Execute(Ready(evade: 0, target: new(4, 1, 0)), mode: 1); Success(branch, AiCommandKind.Move);
        foreach (var wrong in new[] { state, replay.State, branch.State })
        {
            var failed = FacingMoveDecisionBattle.Continue(wrong, A, move.Continuation);
            Assert.False(failed.Accepted); Assert.Same(wrong, failed.State); Assert.Empty(failed.Events);
            Assert.Equal(FacingMoveRejection.StaleContinuation, failed.Rejection);
        }
        Assert.False(FacingMoveDecisionBattle.Continue(move.State, B, move.Continuation).Accepted);
        var act = FacingMoveDecisionBattle.Continue(move.State, A, move.Continuation); Assert.True(act.Accepted);
        Assert.False(FacingMoveDecisionBattle.Continue(act.State, A, move.Continuation).Accepted);
        Assert.Equal(Snapshot(act.State.Battle), Snapshot(FacingMoveDecisionBattle.Continue(move.State, A, move.Continuation).State.Battle));
    }

    [Fact]
    public void Move_first_and_same_tile_END_entries_reject_the_other_routes_including_ACT_first_self()
    {
        foreach (var state in new[] { Ready(), Ready(evade: 0), Ready(target: new(7, 7, 0)) })
            Atomic(state, Execute(state, mode: 1), AiCommandRejection.DispatchMismatch);
        foreach (var state in new[] { Ready(), Ready(evade: 0), Ready(evade: 0, target: new(4, 1, 0)) })
            Atomic(state, Execute(state, mode: 2), AiCommandRejection.DispatchMismatch);
        var empty = Ready(target: new(7, 7, 0));
        Atomic(empty, Execute(empty, EmptyMoveScenario, mode: 2), AiCommandRejection.DispatchMismatch);
    }

    [Fact]
    public void Same_tile_empty_selection_executes_END_with_both_refunds_and_actual_deferred_facing()
    {
        var state = Ready(target: new(7, 7, 0)); var before = Snapshot(state.Battle);
        var end = Execute(state, mode: 2); var o = Success(end, AiCommandKind.End);
        Assert.Equal(BasicSkillAiPlanKind.MoveOnly, o.Decision.Plan.Kind); Assert.Equal(8, o.Decision.Lottery.Length);
        Assert.Equal(new RandomState(836760821, 19), o.DecisionRng);
        Assert.Equal(o.DecisionRng, end.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(o.DecisionRng, end.State.Battle.TurnBattle.Battle.RngState);
        var actor = end.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A);
        Assert.False(actor.MovementTaken); Assert.False(actor.ActionTaken); Assert.False(actor.HasTurn); Assert.Equal(55, actor.Ct);
        Assert.Null(end.State.PendingFacing); Assert.Null(end.Continuation); Assert.Null(end.Path);
        Assert.Equal(state.Revision + 1, end.State.Revision); Assert.Equal(TurnCyclePhase.PickUnit, end.State.Battle.TurnBattle.Turns.Phase);
        Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, end.State.Battle.TurnBattle.Turns.Ticks);
        Assert.Equal(BattleFacing.North, end.State.Battle.Combatants.Single(u => u.UnitId == A).Facing);
        Assert.Single(end.Events.OfType<FacingSettledEvent>()); Assert.Empty(end.Events.OfType<AttackResolvedEvent>());
        Assert.Empty(end.Events.OfType<ThrowStoneResolvedEvent>()); Assert.Empty(end.Events.OfType<ImmediateAbilityResolvedEvent>());
        Assert.Equal(before, Snapshot(state.Battle));
    }

    [Fact]
    public void Same_tile_END_caps_actual_CT_after_both_unused_choice_refunds_without_advancing_the_clock()
    {
        var state = Ready(target: new(7, 7, 0), ct: 35); Assert.Equal(35, state.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
        var end = Execute(state, mode: 2); Success(end, AiCommandKind.End);
        Assert.Equal(60, end.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
        Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, end.State.Battle.TurnBattle.Turns.Ticks);
        Assert.Equal(new RandomState(836760821, 19), end.State.Battle.TurnBattle.Turns.RngState);
    }

    [Fact]
    public void Unified_actual_ACT_failure_rolls_back_selection_adoption_and_retries_from_identical_input()
    {
        var state = Ready(evade: 0, count: ulong.MaxValue - 22); var before = Snapshot(state.Battle);
        for (int i = 0; i < 2; i++)
        {
            var result = Execute(state); Atomic(state, result, AiCommandRejection.BoundaryRejected);
            Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, result.Execution.Transition.AttackRejection);
        }
        Assert.Equal(before, Snapshot(state.Battle));
    }

    [Fact]
    public void Exact_Move_ACT_and_END_budgets_fit_and_short_END_budget_restores_everything()
    {
        foreach (var (scenario, target, draws) in new[] {
            (Scenario(), new TilePosition(4, 1, 0), 17ul), (AttackScenario, new TilePosition(3, 1, 0), 23ul),
            (EmptyMoveScenario, new TilePosition(7, 7, 0), 12ul) })
        {
            var move = Execute(Ready(evade: 0, target: target, count: ulong.MaxValue - draws), scenario, mode: 1);
            Success(move, AiCommandKind.Move);
            var actual = FacingMoveDecisionBattle.Continue(move.State, A, move.Continuation); Assert.True(actual.Accepted);
            Assert.Equal(ulong.MaxValue, actual.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        }
        var end = Execute(Ready(target: new(7, 7, 0), count: ulong.MaxValue - 12), mode: 2); Success(end, AiCommandKind.End);
        Assert.Equal(ulong.MaxValue, end.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        var shortEnd = Ready(target: new(7, 7, 0), count: ulong.MaxValue - 11);
        Atomic(shortEnd, Execute(shortEnd, mode: 2), AiCommandRejection.SelectionRejected);
    }

    [Fact]
    public void Unified_entry_matches_specialized_Move_END_and_existing_ACT_first_paths_without_extra_selection()
    {
        foreach (var (state, scenario, mode, kind) in new[] {
            (Ready(evade: 0, target: new(4, 1, 0)), Scenario(), 1, AiCommandKind.Move),
            (Ready(target: new(7, 7, 0)), Scenario(), 2, AiCommandKind.End) })
        {
            var unified = Execute(state, scenario); var specialized = Execute(state, scenario, mode: mode);
            Success(unified, kind); Success(specialized, kind);
            Assert.Equal(Serialize(unified.Outcome), Serialize(specialized.Outcome)); Assert.Equal(Snapshot(unified.State.Battle), Snapshot(specialized.State.Battle));
            Assert.Equal(Serialize(unified.Events), Serialize(specialized.Events)); Assert.Equal(unified.State.Revision, specialized.State.Revision);
        }
        foreach (var (state, scenario, kind) in new[] {
            (Ready(), Scenario(), AiCommandKind.Accumulate), (Ready(evade: 0), Scenario(), AiCommandKind.Attack),
            (Ready(evade: 0, target: new(4, 1, 0)), new ThrowStoneAiScanScenario([Origin], [Origin], Origin, Origin), AiCommandKind.ThrowStone) })
        {
            var unified = Execute(state, scenario); var o = Success(unified, kind);
            var old = BasicSkillAiActFirstExecution.Execute(state, SelfData.Request(), scenario, Profile(), Selection, Dispatch);
            Assert.True(old.Accepted); Assert.Equal(Serialize(old.Outcome!.Decision), Serialize(o.Decision)); Assert.Equal(old.Outcome.DecisionRng, o.DecisionRng);
            Assert.Equal(Snapshot(old.State.Battle), Snapshot(unified.State.Battle)); Assert.Equal(Serialize(old.Events), Serialize(unified.Events));
            Assert.Null(unified.Continuation); Assert.Null(unified.Path);
        }
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Shared_pipeline_rejects_missing_context_source_facts_spent_choices_and_wrong_actor_atomically()
    {
        var state = Ready(evade: 0, target: new(4, 1, 0));
        foreach (var dispatch in new[] { Dispatch with { InitializedActorContext = false }, Dispatch with { PresentCompactRecords = false },
            Dispatch with { CanonicalCoordinates = false }, Dispatch with { OrdinaryContinuation = false } })
            Atomic(state, Execute(state, dispatch: dispatch), AiCommandRejection.UnsupportedDispatchContext);
        Atomic(state, Execute(state, profile: Profile() with { SourceGeneralEntry = false }), AiCommandRejection.SelectionRejected);
        Atomic(state, Execute(state, selection: Selection with { NoDefendSupport = false }), AiCommandRejection.SelectionRejected);
        var wrong = Execute(state, second: true); Atomic(state, wrong, AiCommandRejection.BoundaryRejected);
        Assert.Equal(FacingTurnRejection.InactiveActor, wrong.Execution.Transition.FacingRejection);
        var move = Execute(state, mode: 1); Success(move, AiCommandKind.Move);
        Atomic(move.State, Execute(move.State), AiCommandRejection.UnavailableChoices);
        var acted = Execute(Ready()); Success(acted, AiCommandKind.Accumulate);
        Atomic(acted.State, Execute(acted.State), AiCommandRejection.UnavailableChoices);
        var actual = FacingMoveDecisionBattle.Continue(move.State, A, move.Continuation); Assert.True(actual.Accepted);
        Atomic(actual.State, Execute(actual.State), AiCommandRejection.BoundaryRejected);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_teams_actor_identities_and_collection_orders_replay_all_new_routes_with_unchanged_input()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        foreach (bool second in new[] { false, true })
        foreach (var (target, scenario, kind) in new[] {
            (new TilePosition(4, 1, 0), Scenario(), AiCommandKind.Move),
            (new TilePosition(7, 7, 0), EmptyMoveScenario, AiCommandKind.Move),
            (new TilePosition(7, 7, 0), Scenario(), AiCommandKind.End) })
        {
            var state = Ready(evade: 0, target: target, second: second); var before = Snapshot(state.Battle);
            var first = Execute(state, scenario, team: team, second: second); Success(first, kind);
            var reverse = Execute(Ready(evade: 0, target: target, second: second, reverse: true), scenario, team: team, second: second);
            Success(reverse, kind); Assert.Equal(Serialize(first.Outcome), Serialize(reverse.Outcome));
            Assert.Equal(Snapshot(first.State.Battle), Snapshot(reverse.State.Battle));
            if (kind == AiCommandKind.Move)
            {
                var actor = second ? B : A;
                var a = FacingMoveDecisionBattle.Continue(first.State, actor, first.Continuation);
                var b = FacingMoveDecisionBattle.Continue(reverse.State, actor, reverse.Continuation);
                Assert.True(a.Accepted); Assert.True(b.Accepted); Assert.Equal(Snapshot(a.State.Battle), Snapshot(b.State.Battle));
            }
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    private static BasicSkillAiCommandResult Execute(FacingTurnState state, ThrowStoneAiScanScenario? scenario = null, int mode = 0,
        BasicSkillAiProfile? profile = null, BasicSkillAiSelectionProfile? selection = null, SwordAiCommandDispatchProfile? dispatch = null,
        SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, bool second = false) => mode switch {
            1 => BasicSkillAiMoveFirstExecution.Execute(state, SelfData.Request(second), scenario ?? Scenario(), profile ?? Profile(team), selection ?? Selection, dispatch ?? Dispatch),
            2 => BasicSkillAiSameTileEndExecution.Execute(state, SelfData.Request(second), scenario ?? Scenario(), profile ?? Profile(team), selection ?? Selection, dispatch ?? Dispatch),
            _ => BasicSkillAiCommandExecution.Execute(state, SelfData.Request(second), scenario ?? Scenario(), profile ?? Profile(team), selection ?? Selection, dispatch ?? Dispatch)
        };
    private static BasicSkillAiCommandOutcome Success(BasicSkillAiCommandResult result, AiCommandKind kind)
    {
        Assert.True(result.Accepted, result.Failure?.ToString()); Assert.Null(result.Failure);
        var outcome = Assert.IsType<BasicSkillAiCommandOutcome>(result.Outcome); Assert.Equal(kind, outcome.Kind); return outcome;
    }
    private static void Atomic(FacingTurnState state, BasicSkillAiCommandResult result, AiCommandRejection reason)
    {
        Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events);
        Assert.Null(result.Outcome); Assert.Null(result.Path); Assert.Null(result.Continuation); Assert.Equal(reason, result.Failure!.Reason);
    }
}
