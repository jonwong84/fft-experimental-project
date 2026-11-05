using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public sealed record BasicSkillAiListTurnConfiguration(BasicSkillAiListProfile Profile, BasicSkillAiListSelectionProfile Selection);
public sealed record BasicSkillAiListTurnFailure(AiTurnStage Stage, BasicSkillAiListCommandFailure? CommandFailure = null,
    FacingMoveContinueResult? ContinuationFailure = null, FacingTurnResult? FacingFailure = null);
public sealed record BasicSkillAiListTurnDecision(bool AfterAct, BasicSkillAiListCommandOutcome Outcome);
public sealed record BasicSkillAiListTurnResult(FacingTurnState State, ImmutableArray<AttackResultEvent> Events,
    ImmutableArray<BasicSkillAiListTurnDecision> Decisions, AiPriorityWorkspace Workspace,
    BasicSkillAiListTurnFailure? Failure, BasicSkillAiListTurnRecovery? Recovery)
{
    public bool Completed => Failure is null;
}
internal sealed record BasicSkillAiListTurnInputs(AccumulateAiPreparationRequest Request,
    BasicSkillAiListTurnConfiguration Configuration, SwordAiCommandDispatchProfile Dispatch, AiPriorityWorkspace Workspace);

/// <summary>Opaque exact-session cursor freezing list configuration, priority knowledge and existing Framework continuation.</summary>
public sealed class BasicSkillAiListTurnRecovery
{
    public AiTurnStage NextStep { get; }
    internal FacingTurnState ExpectedState { get; }
    internal BasicSkillAiListTurnInputs Inputs { get; }
    internal FacingMoveContinuation? Continuation { get; }
    internal BasicSkillAiListTurnRecovery(FacingTurnState state, AiTurnStage step,
        BasicSkillAiListTurnInputs inputs, FacingMoveContinuation? continuation)
    { ExpectedState = state; NextStep = step; Inputs = inputs; Continuation = continuation; }
}

/// <summary>Compose actual variable-list commands through existing continuations/facing and shared stage routing.</summary>
internal static class BasicSkillAiListTurnExecution
{
    public static BasicSkillAiListTurnResult Execute(FacingTurnState state, AccumulateAiPreparationRequest request,
        BasicSkillAiListTurnConfiguration configuration, SwordAiCommandDispatchProfile dispatch, AiPriorityWorkspace? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(configuration);
        BasicSkillAiListScenarios.ValidateArguments(state.Battle, request, configuration.Profile);
        ArgumentNullException.ThrowIfNull(configuration.Selection); ArgumentNullException.ThrowIfNull(dispatch);
        workspace ??= AiPriorityWorkspace.StartUnknown(state.Battle.TurnBattle.Battle.Map); workspace.ValidateMap(state.Battle);
        return Run(state, new(request, configuration, dispatch, workspace), AiTurnStage.FirstCommand, null);
    }

    public static BasicSkillAiListTurnResult Resume(FacingTurnState state, BasicSkillAiListTurnRecovery recovery)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(recovery);
        if (!ReferenceEquals(state, recovery.ExpectedState))
            return new(state, [], [], recovery.Inputs.Workspace, new(AiTurnStage.RecoveryBinding), null);
        return Run(state, recovery.Inputs, recovery.NextStep, recovery.Continuation);
    }

    private static BasicSkillAiListTurnResult Run(FacingTurnState state, BasicSkillAiListTurnInputs inputs,
        AiTurnStage stage, FacingMoveContinuation? continuation)
    {
        var events = ImmutableArray.CreateBuilder<AttackResultEvent>(); var decisions = ImmutableArray.CreateBuilder<BasicSkillAiListTurnDecision>();
        var workspace = inputs.Workspace; var request = inputs.Request;
        BasicSkillAiListTurnResult Failed(BasicSkillAiListTurnFailure failure) => new(state, events.ToImmutable(), decisions.ToImmutable(), workspace,
            failure, new(state, stage, inputs with { Workspace = workspace }, continuation));
        while (true)
        {
            switch (stage)
            {
                case AiTurnStage.FirstCommand:
                case AiTurnStage.PostActCommand:
                    bool after = stage == AiTurnStage.PostActCommand;
                    var c = inputs.Configuration;
                    var command = after ? BasicSkillAiListCommandExecution.ExecuteAfterAct(state, request, c.Profile, c.Selection, inputs.Dispatch, workspace)
                        : BasicSkillAiListCommandExecution.Execute(state, request, c.Profile, c.Selection, inputs.Dispatch, workspace);
                    if (!command.Accepted) return Failed(new(stage, CommandFailure: command.Failure));
                    state = command.State; workspace = command.Workspace; events.AddRange(command.Events);
                    decisions.Add(new(after, command.Outcome!)); continuation = command.Continuation;
                    break;
                case AiTurnStage.RetainedCommand:
                    var retained = FacingMoveDecisionBattle.Continue(state, request.Turn.ActorId, continuation);
                    if (!retained.Accepted) return Failed(new(stage, ContinuationFailure: retained));
                    state = retained.State; events.AddRange(retained.Events); continuation = null;
                    break;
                case AiTurnStage.FinishFacing:
                    var facing = FacingTurnBattle.FinishFacing(state, request.Turn.ActorId, state.PendingFacing,
                        new FacingChoice.ChooseClosedEnemy(request.Turn.TargetId));
                    if (!facing.Accepted) return Failed(new(stage, FacingFailure: facing));
                    state = facing.State; events.AddRange(facing.Events);
                    break;
                default: throw new InvalidOperationException("Unexpected closed variable-list turn stage.");
            }
            var next = AiTurnProgress.Next(state, request.Turn.ActorId, continuation);
            if (next is null) return new(state, events.ToImmutable(), decisions.ToImmutable(), workspace, null, null);
            stage = next.Value;
        }
    }
}
