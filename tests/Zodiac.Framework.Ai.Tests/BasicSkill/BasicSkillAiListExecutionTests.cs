using Xunit;
using Zodiac.Framework.Random;
using SelfData = Zodiac.Framework.Ai.Tests.AccumulateAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiListQueryTests;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class BasicSkillAiListExecutionTests
{
    private static readonly SwordAiCommandDispatchProfile Dispatch = new(true, true, true, true);
    private static BasicSkillAiListTurnResult Run(FacingTurnState state, AiHumanAbilityKind[] actions,
        AiPriorityWorkspace? workspace = null, SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, bool second = false) =>
        BasicSkillAiListTurnExecution.Execute(state, SelfData.Request(second), new(ListProfile(actions, team: team), ListSelection), Dispatch, workspace);

    [Fact]
    public void Later_duplicate_Stone_selection_executes_once_and_actual_miss_does_not_copy_hypothetical_damage()
    {
        var state = Ready(move: 0, target: new(5, 1, 0), word: 27); var before = Snapshot(state.Battle);
        var result = BasicSkillAiListCommandExecution.Execute(state, SelfData.Request(),
            ListProfile([AiHumanAbilityKind.ThrowStone, AiHumanAbilityKind.ThrowStone]), ListSelection, Dispatch);
        Assert.True(result.Accepted, result.Failure?.ToString()); Assert.Equal(2, result.Outcome!.Selection.SelectedOccurrenceIndex);
        Assert.Equal(new RandomState(3910582746, 22), result.Outcome.DecisionRng);
        Assert.Equal(new RandomState(3526304166, 26), result.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(new ThrowStoneResolvedEvent(A, Stone, B, false, 0, false), Assert.Single(result.Events.OfType<ThrowStoneResolvedEvent>()));
        Assert.Equal(50, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.Equal(10, result.Outcome.Selection.Composition.Entries[2].ThrowStone!.Candidate!.Evaluation.Projection!.Outcome!.NormalDamage);
        Assert.Equal(before, Snapshot(state.Battle));
    }

    [Fact]
    public void Complete_missing_repeated_reordered_cap_and_empty_lists_have_literal_final_streams()
    {
        var examples = new[] {
            new { State=Ready(move:0,evade:0), Actions=Array.Empty<AiHumanAbilityKind>(), Rng=new RandomState(675780521,31), Pa=5, Hp=40, Ct=35, Decisions=2 },
            new { State=Ready(move:0), Actions=new[] { AiHumanAbilityKind.Accumulate, AiHumanAbilityKind.Accumulate }, Rng=new RandomState(2473755763,29), Pa=6, Hp=50, Ct=35, Decisions=2 },
            new { State=Ready(move:0), Actions=new[] { AiHumanAbilityKind.ThrowStone, AiHumanAbilityKind.Accumulate }, Rng=new RandomState(1116175964,34), Pa=6, Hp=50, Ct=35, Decisions=2 },
            new { State=Ready(move:0,pa:99), Actions=new[] { AiHumanAbilityKind.Accumulate, AiHumanAbilityKind.Accumulate }, Rng=new RandomState(1343201072,30), Pa=99, Hp=50, Ct=35, Decisions=2 },
            new { State=Ready(move:0,evade:0,target:new(5,1,0)), Actions=new[] { AiHumanAbilityKind.ThrowStone, AiHumanAbilityKind.ThrowStone }, Rng=new RandomState(4015624929,39), Pa=5, Hp=40, Ct=35, Decisions=2 },
            new { State=Ready(move:0,target:new(6,1,0)), Actions=Array.Empty<AiHumanAbilityKind>(), Rng=new RandomState(180171308,18), Pa=5, Hp=50, Ct=55, Decisions=1 }
        };
        foreach (var e in examples)
        {
            var before = Snapshot(e.State.Battle); var result = Run(e.State, e.Actions); Success(result);
            Assert.Equal(e.Rng, result.State.Battle.TurnBattle.Turns.RngState); Assert.Equal(e.Rng, result.State.Battle.TurnBattle.Battle.RngState);
            Assert.Equal(e.Pa, result.State.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
            Assert.Equal(e.Hp, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.Equal(e.Ct, result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
            Assert.Equal(e.Decisions, result.Decisions.Length); Assert.IsType<FacingSettledEvent>(result.Events[^1]);
            foreach (var post in result.Decisions.Where(d => d.AfterAct))
            {
                Assert.Null(post.Outcome.Selection.Composition.LastEnteredKind);
                Assert.All(post.Outcome.Selection.Composition.Entries, entry => {
                    Assert.False(entry.Entry.Usable); Assert.Null(entry.Attack); Assert.Null(entry.Accumulate); Assert.Null(entry.ThrowStone);
                });
                Assert.Equal(8, post.Outcome.Selection.Lottery.Length);
            }
            Assert.Equal(e.State.Battle.TurnBattle.Turns.Ticks, result.State.Battle.TurnBattle.Turns.Ticks); Assert.Equal(before, Snapshot(e.State.Battle));
        }
    }

    [Fact]
    public void Canonical_variable_turn_matches_existing_complete_driver_state_events_and_query_streams()
    {
        var actions = new[] { AiHumanAbilityKind.Accumulate, AiHumanAbilityKind.ThrowStone };
        foreach (var state in new[] { Ready(move: 0, word: 7), Ready(move: 0, evade: 0), Ready(move: 1, evade: 0, target: new(6, 1, 0)) })
        {
            var p = ListProfile(actions); var legacy = new BasicSkillAiDerivedProfile(p.Preparation, p.Scenarios, true, true, true);
            var old = BasicSkillAiTurnExecution.ExecuteDerived(state, SelfData.Request(), new(legacy, new(true, true, true), new(true, true, true)), Dispatch);
            var result = Run(state, actions); Success(result); Assert.True(old.Completed);
            Assert.Equal(Snapshot(old.State.Battle), Snapshot(result.State.Battle)); Assert.Equal(Serialize(old.Events), Serialize(result.Events));
            Assert.Equal(old.Decisions.Length, result.Decisions.Length); Assert.Equal(old.State.Revision, result.State.Revision);
        }
    }

    [Fact]
    public void Exact_variable_turn_budgets_fit_without_speculative_extra_selection()
    {
        foreach (var e in new[] {
            new { State=Ready(move:0,evade:0,count:ulong.MaxValue-24), Actions=Array.Empty<AiHumanAbilityKind>() },
            new { State=Ready(move:0,count:ulong.MaxValue-22), Actions=new[] { AiHumanAbilityKind.Accumulate, AiHumanAbilityKind.Accumulate } },
            new { State=Ready(move:0,count:ulong.MaxValue-27), Actions=new[] { AiHumanAbilityKind.ThrowStone, AiHumanAbilityKind.Accumulate } },
            new { State=Ready(move:0,evade:0,target:new(5,1,0),count:ulong.MaxValue-32), Actions=new[] { AiHumanAbilityKind.ThrowStone, AiHumanAbilityKind.ThrowStone } } })
        {
            var result = Run(e.State, e.Actions); Success(result); Assert.Equal(ulong.MaxValue, result.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        }
    }

    [Fact]
    public void Retained_and_post_ACT_failure_preserve_actual_progress_workspace_and_exact_recovery_stage()
    {
        foreach (bool self in new[] { false, true })
        {
            var actions = self ? new[] { AiHumanAbilityKind.Accumulate, AiHumanAbilityKind.Accumulate } : [];
            var state = self ? Ready(move: 0, count: ulong.MaxValue - 21) : Ready(move: 1, evade: 0, target: new(3, 1, 0), count: ulong.MaxValue - 17);
            var result = Run(state, actions); Assert.False(result.Completed);
            Assert.Equal(self ? AiTurnStage.PostActCommand : AiTurnStage.RetainedCommand, result.Failure!.Stage);
            Assert.Single(result.Decisions); Assert.Equal(self ? 6 : 5, result.State.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
            Assert.NotSame(state, result.State); Assert.NotEmpty(result.Workspace.KnownPriorities);
            var retry = BasicSkillAiListTurnExecution.Resume(result.State, result.Recovery!);
            Assert.Same(result.State, retry.State); Assert.Same(result.Workspace, retry.Workspace); Assert.Empty(retry.Events); Assert.Empty(retry.Decisions);
            Assert.Equal(result.Failure.Stage, retry.Failure!.Stage);
            var replay = Run(state, actions); var foreign = BasicSkillAiListTurnExecution.Resume(replay.State, result.Recovery!);
            Assert.Equal(AiTurnStage.RecoveryBinding, foreign.Failure!.Stage); Assert.Null(foreign.Recovery);
            Assert.Empty(foreign.Events); Assert.Empty(foreign.Decisions);
        }
    }

    [Fact]
    public void Late_first_actual_attack_failure_rolls_back_private_selection_and_priority_writes()
    {
        var state = Ready(move: 0, evade: 0, count: ulong.MaxValue - 13); var history = AiPriorityWorkspace.StartUnknown(state.Battle.TurnBattle.Battle.Map);
        var result = BasicSkillAiListCommandExecution.Execute(state, SelfData.Request(), ListProfile([]), ListSelection, Dispatch, history);
        Atomic(state, result); Assert.Same(history, result.Workspace); Assert.Equal(AiCommandRejection.BoundaryRejected, result.Failure!.Reason);
        Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, result.Execution.Transition.AttackRejection);
    }

    [Fact]
    public void Unknown_cast_keys_dispatch_context_wrong_actor_and_wrong_choice_mode_do_not_publish_work()
    {
        var unknown = Ready(move: 1, target: new(4, 1, 0)); var p = ListProfile([AiHumanAbilityKind.ThrowStone]);
        var result = BasicSkillAiListCommandExecution.Execute(unknown, SelfData.Request(), p, ListSelection, Dispatch); Atomic(unknown, result);
        Assert.Equal(BasicSkillAiDerivedStage.Provenance, result.Failure!.Selection!.Stage);
        var state = Ready(move: 0);
        var context = BasicSkillAiListCommandExecution.Execute(state, SelfData.Request(), ListProfile([]), ListSelection, Dispatch with { PresentCompactRecords = false });
        Atomic(state, context); Assert.Equal(AiCommandRejection.UnsupportedDispatchContext, context.Failure!.Reason);
        var actor = BasicSkillAiListCommandExecution.Execute(state, SelfData.Request(true), ListProfile([]), ListSelection, Dispatch); Atomic(state, actor);
        var spent = BasicSkillAiListCommandExecution.ExecuteAfterAct(state, SelfData.Request(), ListProfile([]), ListSelection, Dispatch); Atomic(state, spent);
        Assert.Equal(AiCommandRejection.UnavailableChoices, spent.Failure!.Reason);
    }

    [Fact]
    public void Late_facing_failure_retains_the_actual_attack_and_receipt_without_repeating_any_entry()
    {
        var ready = Ready(move: 1, evade: 0, target: new(3, 1, 0));
        var state = new FacingTurnState(ready.Battle, ready.Profile, ulong.MaxValue - 2, null, ready.Lineage);
        var result = Run(state, []); Assert.False(result.Completed); Assert.Equal(AiTurnStage.FinishFacing, result.Failure!.Stage);
        Assert.Equal(40, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp); Assert.NotNull(result.State.PendingFacing);
        var retry = BasicSkillAiListTurnExecution.Resume(result.State, result.Recovery!);
        Assert.Same(result.State.PendingFacing, retry.State.PendingFacing); Assert.Same(result.Workspace, retry.Workspace); Assert.Empty(retry.Events); Assert.Empty(retry.Decisions);
    }

    [Fact]
    public void Lethal_variable_Stone_preserves_explicit_terminal_failure_boundaries_and_real_progress()
    {
        foreach (bool moveFirst in new[] { false, true })
        {
            var state = Ready(move: moveFirst ? 1 : 0, evade: 0, pa: 99, target: moveFirst ? new(4, 1, 0) : new(5, 1, 0));
            var history = moveFirst ? BasicSkillAiPriorityWorkspaceQueryTests.Seed(state, 1) : null;
            var result = Run(state, [AiHumanAbilityKind.ThrowStone], history);
            Assert.False(result.Completed); Assert.Equal(moveFirst ? AiTurnStage.FinishFacing : AiTurnStage.PostActCommand, result.Failure!.Stage);
            Assert.Equal(0, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp); Assert.Single(result.Events.OfType<ThrowStoneResolvedEvent>());
            var retry = BasicSkillAiListTurnExecution.Resume(result.State, result.Recovery!);
            Assert.Same(result.State, retry.State); Assert.Same(result.Workspace, retry.Workspace); Assert.Empty(retry.Events); Assert.Empty(retry.Decisions);
        }
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_teams_and_actor_identities_replay_reordered_occurrences_without_mutation()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        foreach (bool second in new[] { false, true })
        {
            var actions = new[] { AiHumanAbilityKind.ThrowStone, AiHumanAbilityKind.Accumulate, AiHumanAbilityKind.Accumulate };
            var state = Ready(move: 0, word: 10, second: second); var before = Snapshot(state.Battle);
            var first = Run(state, actions, team: team, second: second); Success(first);
            var replay = Run(Ready(move: 0, word: 10, second: second, reverse: true), actions, team: team, second: second); Success(replay);
            Assert.Equal(Snapshot(first.State.Battle), Snapshot(replay.State.Battle)); Assert.Equal(Serialize(first.Events), Serialize(replay.Events));
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    private static void Atomic(FacingTurnState state, BasicSkillAiListCommandResult result)
    {
        Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events); Assert.Null(result.Outcome);
        Assert.Null(result.Path); Assert.Null(result.Continuation);
    }
    private static void Success(BasicSkillAiListTurnResult result)
    {
        Assert.True(result.Completed, result.Failure?.ToString()); Assert.Null(result.Recovery); Assert.Null(result.State.PendingFacing);
        Assert.Null(result.State.Battle.TurnBattle.Turns.ActiveUnitId);
    }
}
