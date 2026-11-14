using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Finish one bounded supported roster turn with actual frozen follow-ups and explicit fixed/kept facing.</summary>
/// <remarks>ZOD-315; source 11464943bbdbd2a0af022d90cb7f10455cf911df. Stops before the next clock; no corpse/terminal policy or automatic facing.</remarks>
public static class AiTurnController
{
    public static AiTurnResult Execute(
        RosterFacingTurnState state,
        RosterAiRefreshRequest request,
        AiTurnConfiguration configuration,
        AiPriorityWorkspace? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(configuration);

        workspace ??= AiPriorityWorkspace.StartUnknown(state.Battle.TurnBattle.Battle.Map);
        workspace.ValidateMap(state.Battle);

        // An existing receipt remains the authority for its actor, even though UNIT_TURN has already ended.
        AiTurnStage stage;
        if (state.PendingFacing is not null)
        {
            stage = AiTurnStage.FinishFacing;
        }
        else
        {
            stage = state.Battle.TurnBattle.Turns.Units.Any(u => u.Id == request.ActorId && u.ActionTaken)
                ? AiTurnStage.PostActCommand : AiTurnStage.FirstCommand;
        }

        return Run(state, new(request, configuration, workspace), stage, null);
    }

    public static AiTurnResult Resume(RosterFacingTurnState state, AiTurnRecovery recovery)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(recovery);


        if (!ReferenceEquals(state, recovery.ExpectedState))
            return new(state, [], [], recovery.Inputs.Workspace, new(AiTurnStage.RecoveryBinding), null);

        return Run(state, recovery.Inputs, recovery.NextStep, recovery.Continuation);
    }

    private static AiTurnResult Run(RosterFacingTurnState state, AiTurnInputs inputs,
        AiTurnStage stage, RosterMoveContinuation? continuation)
    {
        var events = ImmutableArray.CreateBuilder<AttackResultEvent>();
        var decisions = ImmutableArray.CreateBuilder<AiCommandOutcome>();
        var workspace = inputs.Workspace;
        var request = inputs.Request;
        var c = inputs.Configuration;

        AiTurnResult Failed(AiTurnFailure failure) => new(
            state,
            events.ToImmutable(),
            decisions.ToImmutable(),
            workspace,
            failure,
            new(state, stage, inputs with { Workspace = workspace }, continuation));

        while (true)
        {
            switch (stage)
            {
                case AiTurnStage.FirstCommand:
                case AiTurnStage.PostActCommand:
                    var command = AiCommandExecutor.Execute(state, request, c.Decision, c.Dispatch, c.Facing, workspace);
                    if (!command.Accepted) return Failed(new(stage, CommandFailure: command.Failure));
                    state = command.State; workspace = command.Workspace; events.AddRange(command.Events);
                    decisions.Add(command.Outcome!); continuation = command.Continuation;
                    break;
                case AiTurnStage.RetainedCommand:
                    var retained = RosterMoveDecisionBattle.Continue(state, request.ActorId, continuation);
                    if (!retained.Accepted) return Failed(new(stage, ContinuationFailure: retained));
                    state = retained.State; events.AddRange(retained.Events); continuation = null;
                    break;
                case AiTurnStage.FinishFacing:
                    var facing = RosterFacingTurnBattle.FinishFacing(state, request.ActorId, state.PendingFacing, c.Facing);
                    if (!facing.Accepted) return Failed(new(stage, FacingFailure: facing));
                    state = facing.State; events.AddRange(facing.Events);
                    break;
                default:
                    throw new InvalidOperationException("Unexpected bounded roster turn stage.");
            }
            var next = AiTurnProgress.Next(state, request.ActorId, continuation);
            if (next is null) return new(state, events.ToImmutable(), decisions.ToImmutable(), workspace, null, null);
            stage = next.Value;
        }
    }
}
