using System.Collections.Immutable;
using Xunit;
using Zodiac.Framework.Random;
using SelfData = Zodiac.Framework.Ai.Tests.AccumulateAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiDerivedQueryTests;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class BasicSkillAiDerivedExecutionTests
{
    private static readonly SwordAiCommandDispatchProfile Dispatch = new(true, true, true, true);
    private static BasicSkillAiDerivedTurnConfiguration Configuration(SwordAiScenarioTeam team) => new(DerivedProfile(team), Selection, new(true, true, true));
    private static BasicSkillAiCommandResult Command(FacingTurnState state, BasicSkillAiDerivedProfile? profile = null, bool second = false) =>
        BasicSkillAiCommandExecution.ExecuteDerived(state, SelfData.Request(second), profile ?? DerivedProfile(), Selection, Dispatch);
    private static BasicSkillAiTurnResult Run(FacingTurnState state, SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue,
        bool second = false, AccumulateAiPreparationRequest? request = null) =>
        BasicSkillAiTurnExecution.ExecuteDerived(state, request ?? SelfData.Request(second), Configuration(team), Dispatch);

    [Fact]
    public void Derived_command_owns_selection_in_Framework_and_resolves_real_Attack_without_copying_projection()
    {
        var state = Ready(move: 0, evade: 0); var before = Snapshot(state.Battle); var result = Command(state);
        Assert.True(result.Accepted, result.Failure?.ToString()); Assert.Equal(BasicSkillAiCommandKind.Attack, result.Outcome!.Kind);
        Assert.Equal(new RandomState(2937909499, 21), result.Outcome.DecisionRng);
        Assert.Equal(new RandomState(3823029677, 27), result.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(new AttackResolvedEvent(A, B, true, false, 10), Assert.Single(result.Events.OfType<AttackResolvedEvent>()));
        Assert.Equal(40, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp); Assert.Null(result.Continuation);
        Assert.True(result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).ActionTaken);
        Assert.False(result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).MovementTaken);
        var pure = BasicSkillAiDerivedSelection.Evaluate(state.Battle, SelfData.Request(), DerivedProfile(), Selection, new(1, 7));
        Assert.True(pure.Accepted); Assert.Equal(Serialize(pure.Outcome!.Selection), Serialize(result.Outcome.Decision));
        Assert.Equal(before, Snapshot(state.Battle));
    }

    [Fact]
    public void Safe_range_five_Move_retains_one_Stone_and_uses_actual_rules_without_an_actor_rescore()
    {
        var state = Ready(move: 1, target: new(6, 1, 0), evade: 0); var move = Command(state); Assert.True(move.Accepted, move.Failure?.ToString());
        Assert.Equal(BasicSkillAiCommandKind.Move, move.Outcome!.Kind); Assert.Equal(new RandomState(267834847, 17), move.Outcome.DecisionRng);
        Assert.Single(move.Outcome.Decision.Composition.Movement); Assert.Equal(new[] { Origin, new TilePosition(2, 1, 0) }, move.Path!.Tiles.ToArray());
        Assert.NotNull(move.Continuation); Assert.Equal(50, move.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        var act = FacingMoveDecisionBattle.Continue(move.State, A, move.Continuation); Assert.True(act.Accepted);
        Assert.Equal(new ThrowStoneResolvedEvent(A, Stone, B, true, 10, false), Assert.Single(act.Events.OfType<ThrowStoneResolvedEvent>()));
        Assert.Equal(new RandomState(3791773014, 24), act.State.Battle.TurnBattle.Turns.RngState); Assert.NotNull(act.State.PendingFacing);
        var faced = FacingTurnBattle.FinishFacing(act.State, A, act.State.PendingFacing, new FacingChoice.ChooseClosedEnemy(B)); Assert.True(faced.Accepted);
        Assert.Equal(40, faced.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Unknown_keys_and_remaining_context_guards_reject_with_no_partial_RNG_events_path_or_token()
    {
        var unknown = Ready(move: 1, target: new(5, 1, 0)); var result = Command(unknown);
        Atomic(unknown, result); Assert.Equal(BasicSkillAiSelectionStage.Provenance, result.Failure!.Selection!.Stage);
        var blocked = Run(unknown); Assert.False(blocked.Completed); Assert.Same(unknown, blocked.State); Assert.Empty(blocked.Events); Assert.Empty(blocked.Decisions);
        Assert.Equal(BasicSkillAiTurnStage.FirstCommand, blocked.Failure!.Stage); Assert.Equal(BasicSkillAiSelectionStage.Provenance, blocked.Failure.FirstFailure!.Selection!.Stage);
        var retry = BasicSkillAiTurnExecution.Resume(blocked.State, blocked.Recovery!); Assert.False(retry.Completed);
        Assert.Same(blocked.State, retry.State); Assert.Empty(retry.Events); Assert.Empty(retry.Decisions);
        var state = Ready(move: 0); var bad = BasicSkillAiCommandExecution.ExecuteDerived(state, SelfData.Request(), DerivedProfile(), Selection,
            Dispatch with { PresentCompactRecords = false }); Atomic(state, bad);
        Assert.Equal(BasicSkillAiCommandRejection.UnsupportedDispatchContext, bad.Failure!.Reason);
        var wrong = Command(state, second: true); Atomic(state, wrong); Assert.Equal(BasicSkillAiCommandRejection.BoundaryRejected, wrong.Failure!.Reason);
    }

    [Fact]
    public void Complete_derived_Attack_self_cap_Stone_and_empty_turns_have_literal_final_streams()
    {
        var examples = new[] {
            new { State=Ready(move:0,evade:0), Rng=new RandomState(3834121963,37), Ct=35, Pa=5, Hp=40, Decisions=2 },
            new { State=Ready(move:0,evade:97,word:7), Rng=new RandomState(2073554906,34), Ct=35, Pa=6, Hp=50, Decisions=2 },
            // At PA99 both damage projections KO: each clock visits the corpse and then the actor, shifting the self coin to draw9.
            new { State=Ready(move:0,evade:97,word:1,pa:99), Rng=new RandomState(1303003706,36), Ct=35, Pa=99, Hp=50, Decisions=2 },
            new { State=Ready(move:1,evade:0,target:new(6,1,0)), Rng=new RandomState(3791773014,24), Ct=15, Pa=5, Hp=40, Decisions=1 },
            new { State=Ready(move:0,evade:0,target:new(5,1,0)), Rng=new RandomState(3008955749,35), Ct=35, Pa=5, Hp=40, Decisions=2 },
            new { State=Ready(move:0,target:new(6,1,0)), Rng=new RandomState(836760821,19), Ct=55, Pa=5, Hp=50, Decisions=1 }
        };
        foreach (var e in examples)
        {
            var before = Snapshot(e.State.Battle); var result = Run(e.State); Success(result);
            Assert.Equal(e.Rng, result.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(e.Rng, result.State.Battle.TurnBattle.Battle.RngState);
            Assert.Equal(e.Ct, result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
            Assert.Equal(e.Pa, result.State.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
            Assert.Equal(e.Hp, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.Equal(e.Decisions, result.Decisions.Length); Assert.Single(result.Events.OfType<FacingSettledEvent>());
            Assert.IsType<FacingSettledEvent>(result.Events[^1]); Assert.Equal(e.State.Battle.TurnBattle.Turns.Ticks, result.State.Battle.TurnBattle.Turns.Ticks);
            Assert.Equal(before, Snapshot(e.State.Battle));
        }
    }

    [Fact]
    public void In_reach_derived_turn_matches_existing_supplied_scenario_turn_and_ordered_events()
    {
        var state = Ready(word: 7); var selected = BasicSkillAiDerivedSelection.Evaluate(state.Battle, SelfData.Request(), DerivedProfile(), Selection, new(7, 7));
        Assert.True(selected.Accepted); var o = selected.Outcome!; var m = o.Scenarios.Movement;
        var supplied = BasicSkillAiTurnExecution.Execute(state, SelfData.Request(), new(m.ScoreMask, o.ScanOrigins, m.BaselineDestination, m.ActFirstDestination),
            new(Profile(), Selection, new(true, true, true)), Dispatch); Success(supplied);
        var derived = Run(state); Success(derived); Assert.Equal(Snapshot(supplied.State.Battle), Snapshot(derived.State.Battle));
        Assert.Equal(supplied.State.Revision, derived.State.Revision); Assert.Equal(Serialize(supplied.Events), Serialize(derived.Events));
        Assert.Equal(supplied.Decisions.Select(d => Serialize(d)), derived.Decisions.Select(d => Serialize(d)));
    }

    [Fact]
    public void Exact_full_turn_budgets_fit_and_late_actual_Attack_failure_rolls_back_only_that_first_transaction()
    {
        foreach (var state in new[] { Ready(move: 0, evade: 0, count: ulong.MaxValue - 30),
            Ready(move: 0, evade: 97, word: 7, count: ulong.MaxValue - 27),
            Ready(move: 1, evade: 0, target: new(6, 1, 0), count: ulong.MaxValue - 17),
            Ready(move: 0, evade: 0, target: new(5, 1, 0), count: ulong.MaxValue - 28) })
        {
            var result = Run(state); Success(result); Assert.Equal(ulong.MaxValue, result.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        }
        var late = Ready(move: 0, evade: 0, count: ulong.MaxValue - 19); var result2 = Command(late); Atomic(late, result2);
        Assert.Equal(BasicSkillAiCommandRejection.BoundaryRejected, result2.Failure!.Reason);
        Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, result2.Execution.Transition.AttackRejection);
    }

    [Fact]
    public void Derived_recovery_preserves_accepted_Move_or_PA_and_never_replays_selection_or_events()
    {
        foreach (bool self in new[] { false, true })
        {
            var state = self ? Ready(move: 0, evade: 97, word: 7, count: ulong.MaxValue - 26)
                : Ready(move: 1, evade: 0, target: new(6, 1, 0), count: ulong.MaxValue - 16);
            var result = Run(state); Assert.False(result.Completed); Assert.NotSame(state, result.State); Assert.Single(result.Decisions);
            Assert.Equal(self ? BasicSkillAiTurnStage.PostActCommand : BasicSkillAiTurnStage.RetainedCommand, result.Failure!.Stage);
            Assert.Equal(self ? 6 : 5, result.State.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
            var before = Snapshot(result.State.Battle); var retry = BasicSkillAiTurnExecution.Resume(result.State, result.Recovery!);
            Assert.False(retry.Completed); Assert.Same(result.State, retry.State); Assert.Empty(retry.Events); Assert.Empty(retry.Decisions);
            Assert.Equal(result.Failure.Stage, retry.Failure!.Stage); Assert.Equal(before, Snapshot(retry.State.Battle));
            var replay = Run(state); var wrong = BasicSkillAiTurnExecution.Resume(replay.State, result.Recovery!);
            Assert.Equal(BasicSkillAiTurnStage.RecoveryBinding, wrong.Failure!.Stage); Assert.Empty(wrong.Events); Assert.Empty(wrong.Decisions); Assert.Null(wrong.Recovery);
        }
    }

    [Fact]
    public void Late_retained_facing_failure_keeps_the_actual_result_and_pending_receipt_for_exact_retry()
    {
        var ready = Ready(move: 1, evade: 0, target: new(6, 1, 0));
        var state = new FacingTurnState(ready.Battle, ready.Profile, ulong.MaxValue - 2, null, ready.Lineage);
        var result = Run(state); Assert.False(result.Completed); Assert.Equal(BasicSkillAiTurnStage.FinishFacing, result.Failure!.Stage);
        Assert.Equal(FacingTurnRejection.RevisionExhausted, result.Failure.FacingFailure!.FacingRejection);
        Assert.NotNull(result.State.PendingFacing); Assert.Equal(40, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        var retry = BasicSkillAiTurnExecution.Resume(result.State, result.Recovery!); Assert.False(retry.Completed);
        Assert.Same(result.State, retry.State); Assert.Same(result.State.PendingFacing, retry.State.PendingFacing); Assert.Empty(retry.Events); Assert.Empty(retry.Decisions);
    }

    [Fact]
    public void Lethal_derived_ACT_retains_terminal_or_pending_facing_boundary_without_a_fake_living_target()
    {
        foreach (bool moveFirst in new[] { false, true })
        {
            var state = Ready(move: moveFirst ? 1 : 0, evade: 0, target: moveFirst ? new(3, 1, 0) : new(2, 1, 0));
            var result = Run(state, request: SelfData.Request() with { Weapon = new(SelfData.SwordId, 10) });
            Assert.False(result.Completed); Assert.Equal(0, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.Equal(moveFirst ? BasicSkillAiTurnStage.FinishFacing : BasicSkillAiTurnStage.PostActCommand, result.Failure!.Stage);
            Assert.Single(result.Events.OfType<AttackResolvedEvent>()); Assert.Empty(result.Events.OfType<FacingSettledEvent>());
            if (moveFirst) Assert.NotNull(result.State.PendingFacing);
            else Assert.Equal(AccumulateAiCandidateRejection.TerminalInput, result.Failure.PostActFailure!.Selection!.Preparation!.AccumulateReason);
        }
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_teams_identities_and_collection_policy_orders_replay_derived_complete_turns()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        foreach (bool second in new[] { false, true })
        foreach (bool outside in new[] { false, true })
        {
            var state = Ready(move: outside ? 1 : 0, evade: outside ? 0 : 97, word: 7,
                target: outside ? new(6, 1, 0) : new(2, 1, 0), second: second);
            var before = Snapshot(state.Battle); var result = Run(state, team, second); Success(result);
            var reverse = Run(Ready(move: outside ? 1 : 0, evade: outside ? 0 : 97, word: 7,
                target: outside ? new(6, 1, 0) : new(2, 1, 0), second: second, reverse: true), team, second,
                SelfData.Request(second) with { Turn = SelfData.RefreshRequest(second) with { Policies = SelfData.RefreshRequest(second).Policies.Reverse().ToImmutableArray() } });
            Success(reverse); Assert.Equal(Snapshot(result.State.Battle), Snapshot(reverse.State.Battle)); Assert.Equal(Serialize(result.Events), Serialize(reverse.Events));
            Assert.Equal(result.Decisions.Select(d => Serialize(d)), reverse.Decisions.Select(d => Serialize(d))); Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    private static void Atomic(FacingTurnState state, BasicSkillAiCommandResult result)
    {
        Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events); Assert.Null(result.Outcome);
        Assert.Null(result.Path); Assert.Null(result.Continuation);
    }
    private static void Success(BasicSkillAiTurnResult result)
    {
        Assert.True(result.Completed, result.Failure?.ToString()); Assert.Null(result.Recovery); Assert.Null(result.State.PendingFacing);
        Assert.Null(result.State.Battle.TurnBattle.Turns.ActiveUnitId);
    }
}
