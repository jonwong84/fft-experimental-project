using Xunit;
using Zodiac.Framework.Random;
using SelfData = Zodiac.Framework.Ai.Tests.AccumulateAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiDerivedQueryTests;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiPriorityWorkspaceQueryTests;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class BasicSkillAiPriorityWorkspaceExecutionTests
{
    private static readonly SwordAiCommandDispatchProfile Dispatch = new(true, true, true, true);
    private static BasicSkillAiDerivedTurnConfiguration Config(SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue) =>
        new(DerivedProfile(team), Selection, new(true, true, true));
    private static BasicSkillAiWorkspaceCommandResult Command(FacingTurnState state, AiPriorityWorkspace workspace) =>
        BasicSkillAiCommandExecution.ExecuteWithWorkspace(state, SelfData.Request(), DerivedProfile(), Selection, Dispatch, workspace);
    private static BasicSkillAiWorkspaceTurnResult Run(FacingTurnState state, AiPriorityWorkspace workspace,
        SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, bool second = false) =>
        BasicSkillAiTurnExecution.ExecuteWithWorkspace(state, SelfData.Request(second), Config(team), Dispatch, workspace);

    [Fact]
    public void First_command_commits_new_writes_with_actual_ACT_or_Move_and_preserves_the_input_branch()
    {
        foreach (int sign in new[] { -1, 1 })
        {
            var state = Ready(move: 1, evade: 0, target: new(4, 1, 0)); var history = Seed(state, sign); var result = Command(state, history);
            Assert.True(result.Command.Accepted, result.Command.Failure?.ToString());
            Assert.Equal(sign < 0 ? AiCommandKind.ThrowStone : AiCommandKind.Move, result.Command.Outcome!.Kind);
            Assert.Equal(new RandomState(267834847, 17), result.Command.Outcome.DecisionRng);
            Assert.Equal(sign < 0 ? new RandomState(3791773014, 24) : new RandomState(267834847, 17), result.Command.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal((short)0, result.Workspace.KnownPriorities[new(2, 1, 0)]);
            Assert.False(history.KnownPriorities.ContainsKey(new(2, 1, 0))); // The prior enemy occupied this tile; no prior score was written.
            Assert.Equal(sign * 26, result.Workspace.KnownPriorities[Origin]);
            Assert.Equal(sign < 0 ? 40 : 50, result.Command.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.Equal(sign > 0, result.Command.Continuation is not null);
        }
    }

    [Fact]
    public void Late_first_actual_resolution_failure_returns_the_original_workspace_and_no_published_command()
    {
        var state = Ready(move: 1, evade: 0, target: new(4, 1, 0), count: ulong.MaxValue - 16); var history = Seed(state);
        var pure = Select(state, history); Assert.True(pure.Query.Accepted); Assert.NotSame(history, pure.Workspace);
        var result = Command(state, history); Assert.False(result.Command.Accepted); Assert.Same(history, result.Workspace);
        Assert.Same(state, result.Command.State); Assert.Empty(result.Command.Events); Assert.Null(result.Command.Outcome);
        Assert.Null(result.Command.Path); Assert.Null(result.Command.Continuation);
        Assert.Equal(AiCommandRejection.BoundaryRejected, result.Command.Failure!.Reason);
        Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, result.Command.Execution.Transition.AttackRejection);
    }

    [Fact]
    public void Complete_retained_origin_turns_have_literal_streams_and_only_post_ACT_selection_adds_more_writes()
    {
        foreach (int sign in new[] { 0, 1 })
        {
            var state = Ready(move: 1, evade: 0, target: new(4, 1, 0)); var history = Seed(state, sign); var before = Snapshot(state.Battle);
            var result = Run(state, history); Success(result.Turn);
            Assert.Equal(sign == 0 ? new RandomState(3008955749, 35) : new RandomState(3791773014, 24), result.Turn.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(sign == 0 ? 2 : 1, result.Turn.Decisions.Length);
            Assert.Equal((short)(sign == 0 ? -26 : 0), result.Workspace.KnownPriorities[new(2, 1, 0)]);
            Assert.Equal(sign * 26, result.Workspace.KnownPriorities[Origin]);
            Assert.Equal(40, result.Turn.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.Equal(15, result.Turn.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
            Assert.Single(result.Turn.Events.OfType<ThrowStoneResolvedEvent>()); Assert.IsType<FacingSettledEvent>(result.Turn.Events[^1]);
            Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, result.Turn.State.Battle.TurnBattle.Turns.Ticks); Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    [Fact]
    public void Exact_complete_budgets_fit_and_late_retained_or_post_ACT_failure_preserves_only_accepted_writes()
    {
        foreach (int sign in new[] { 0, 1 })
        {
            int budget = sign == 0 ? 28 : 17;
            var state = Ready(move: 1, evade: 0, target: new(4, 1, 0), count: ulong.MaxValue - (ulong)budget);
            var exact = Run(state, Seed(state, sign)); Success(exact.Turn); Assert.Equal(ulong.MaxValue, exact.Turn.State.Battle.TurnBattle.Turns.RngState.DrawCount);
            var shortState = Ready(move: 1, evade: 0, target: new(4, 1, 0), count: ulong.MaxValue - (ulong)budget + 1);
            var history = Seed(shortState, sign); var result = Run(shortState, history);
            Assert.False(result.Turn.Completed); Assert.Equal(sign == 0 ? AiTurnStage.PostActCommand : AiTurnStage.RetainedCommand, result.Turn.Failure!.Stage);
            Assert.Single(result.Turn.Decisions); Assert.Equal((short)0, result.Workspace.KnownPriorities[new(2, 1, 0)]);
            Assert.Equal(sign == 0 ? 40 : 50, result.Turn.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            var retry = BasicSkillAiTurnExecution.ResumeWithWorkspace(result.Turn.State, result.Turn.Recovery!);
            Assert.False(retry.Turn.Completed); Assert.Same(result.Workspace, retry.Workspace); Assert.Same(result.Turn.State, retry.Turn.State);
            Assert.Empty(retry.Turn.Events); Assert.Empty(retry.Turn.Decisions); Assert.Equal(result.Turn.Failure.Stage, retry.Turn.Failure!.Stage);
            var replay = Run(shortState, history);
            var wrong = BasicSkillAiTurnExecution.ResumeWithWorkspace(replay.Turn.State, result.Turn.Recovery!);
            Assert.Equal(AiTurnStage.RecoveryBinding, wrong.Turn.Failure!.Stage); Assert.Same(result.Workspace, wrong.Workspace);
            Assert.Empty(wrong.Turn.Events); Assert.Empty(wrong.Turn.Decisions); Assert.Null(wrong.Turn.Recovery);
        }
    }

    [Fact]
    public void Facing_failure_preserves_actual_damage_receipt_and_preceding_workspace_without_rescoring()
    {
        var ready = Ready(move: 1, evade: 0, target: new(4, 1, 0));
        var state = new FacingTurnState(ready.Battle, ready.Profile, ulong.MaxValue - 2, null, ready.Lineage);
        var result = Run(state, Seed(state, 1)); Assert.False(result.Turn.Completed);
        Assert.Equal(AiTurnStage.FinishFacing, result.Turn.Failure!.Stage); Assert.NotNull(result.Turn.State.PendingFacing);
        Assert.Equal(40, result.Turn.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.Equal((short)0, result.Workspace.KnownPriorities[new(2, 1, 0)]);
        var retry = BasicSkillAiTurnExecution.ResumeWithWorkspace(result.Turn.State, result.Turn.Recovery!);
        Assert.Same(result.Workspace, retry.Workspace); Assert.Same(result.Turn.State.PendingFacing, retry.Turn.State.PendingFacing);
        Assert.Empty(retry.Turn.Events); Assert.Empty(retry.Turn.Decisions);
    }

    [Fact]
    public void Lethal_ACT_and_retained_ACT_keep_history_at_the_existing_terminal_boundaries()
    {
        foreach (int sign in new[] { -1, 1 })
        {
            var state = Ready(move: 1, evade: 0, pa: 99, target: new(4, 1, 0)); var result = Run(state, Seed(state, sign));
            Assert.False(result.Turn.Completed); Assert.Equal(0, result.Turn.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.Equal(sign < 0 ? AiTurnStage.PostActCommand : AiTurnStage.FinishFacing, result.Turn.Failure!.Stage);
            Assert.Single(result.Turn.Events.OfType<ThrowStoneResolvedEvent>()); Assert.Empty(result.Turn.Events.OfType<FacingSettledEvent>());
            Assert.Equal((short)0, result.Workspace.KnownPriorities[new(2, 1, 0)]);
            var retry = BasicSkillAiTurnExecution.ResumeWithWorkspace(result.Turn.State, result.Turn.Recovery!);
            Assert.Same(result.Workspace, retry.Workspace); Assert.Empty(retry.Turn.Events); Assert.Empty(retry.Turn.Decisions);
        }
    }

    [Fact]
    public void Unknown_or_forgotten_keys_reject_first_stage_without_history_or_command_progress()
    {
        var state = Ready(move: 1, target: new(4, 1, 0)); var history = Seed(state).Forget(); var result = Run(state, history);
        Assert.False(result.Turn.Completed); Assert.Same(state, result.Turn.State); Assert.Same(history, result.Workspace);
        Assert.Equal(BasicSkillAiSelectionStage.Provenance, result.Turn.Failure!.FirstFailure!.Selection!.Stage);
        Assert.Empty(result.Turn.Events); Assert.Empty(result.Turn.Decisions);
        var retry = BasicSkillAiTurnExecution.ResumeWithWorkspace(result.Turn.State, result.Turn.Recovery!);
        Assert.Same(history, retry.Workspace); Assert.Same(state, retry.Turn.State); Assert.Empty(retry.Turn.Decisions);
    }

    [Fact]
    public void Old_derived_turns_match_workspace_entry_for_Attack_self_cap_and_empty_without_history_inputs()
    {
        foreach (var state in new[] { Ready(move: 0, evade: 0), Ready(move: 0, word: 7), Ready(move: 0, pa: 99), Ready(move: 0, target: new(6, 1, 0)) })
        {
            var unknown = AiPriorityWorkspace.StartUnknown(state.Battle.TurnBattle.Battle.Map);
            var workspace = Run(state, unknown); var old = BasicSkillAiTurnExecution.ExecuteDerived(state, SelfData.Request(), Config(), Dispatch);
            Assert.Equal(Snapshot(old.State.Battle), Snapshot(workspace.Turn.State.Battle)); Assert.Equal(Serialize(old.Events), Serialize(workspace.Turn.Events));
            Assert.Equal(old.Decisions.Select(d => Serialize(d)), workspace.Turn.Decisions.Select(d => Serialize(d)));
            Assert.NotEmpty(workspace.Workspace.KnownPriorities); Assert.Empty(unknown.KnownPriorities);
        }
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_teams_and_actors_replay_complete_retained_origin_turns()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        foreach (bool second in new[] { false, true })
        foreach (int sign in new[] { -1, 1 })
        {
            var state = Ready(move: 1, evade: 0, target: new(4, 1, 0), second: second); var history = Seed(state, sign, second);
            var first = Run(state, history, team, second); var replay = Run(state, history, team, second); Success(first.Turn); Success(replay.Turn);
            Assert.Equal(Snapshot(first.Turn.State.Battle), Snapshot(replay.Turn.State.Battle)); Assert.Equal(Serialize(first.Turn.Events), Serialize(replay.Turn.Events));
            Assert.Equal(first.Workspace.KnownPriorities.OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X), replay.Workspace.KnownPriorities.OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X));
        }
    }

    private static void Success(BasicSkillAiTurnResult result)
    {
        Assert.True(result.Completed, result.Failure?.ToString()); Assert.Null(result.Recovery); Assert.Null(result.State.PendingFacing);
        Assert.Null(result.State.Battle.TurnBattle.Turns.ActiveUnitId);
    }
}
