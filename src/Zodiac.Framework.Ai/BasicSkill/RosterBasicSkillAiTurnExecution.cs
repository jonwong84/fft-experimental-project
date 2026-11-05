using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public sealed record RosterBasicSkillAiTurnConfiguration(RosterAiSimulationProfile Simulation, RosterAiRefreshProfile Refresh,
    AiHumanAbilityLists Lists, RosterAiGeneralProfile General, RosterAiMovementProfile Movement,
    RosterBasicSkillAiProfile Selection, RosterBasicSkillAiDispatchProfile Dispatch, BattleFacing? Facing = null);
public sealed record RosterBasicSkillAiTurnFailure(BasicSkillAiTurnStage Stage, RosterBasicSkillAiCommandFailure? CommandFailure = null,
    RosterMoveContinueResult? ContinuationFailure = null, RosterFacingResult? FacingFailure = null);
public sealed record RosterBasicSkillAiTurnResult(RosterFacingTurnState State, ImmutableArray<AttackResultEvent> Events,
    ImmutableArray<RosterBasicSkillAiCommandOutcome> Decisions, AiPriorityWorkspace Workspace,
    RosterBasicSkillAiTurnFailure? Failure, RosterBasicSkillAiTurnRecovery? Recovery)
{
    public bool Completed => Failure is null;
}
internal sealed record RosterBasicSkillAiTurnInputs(RosterAiRefreshRequest Request,
    RosterBasicSkillAiTurnConfiguration Configuration, AiPriorityWorkspace Workspace);

/// <summary>Opaque exact-session cursor retaining accepted progress, frozen inputs and the Framework Move token.</summary>
public sealed class RosterBasicSkillAiTurnRecovery
{
    public BasicSkillAiTurnStage NextStep { get; }
    internal RosterFacingTurnState ExpectedState { get; }
    internal RosterBasicSkillAiTurnInputs Inputs { get; }
    internal RosterMoveContinuation? Continuation { get; }
    internal RosterBasicSkillAiTurnRecovery(RosterFacingTurnState state, BasicSkillAiTurnStage step,
        RosterBasicSkillAiTurnInputs inputs, RosterMoveContinuation? continuation)
    { ExpectedState = state; NextStep = step; Inputs = inputs; Continuation = continuation; }
}

/// <summary>Finish one bounded supported roster turn with actual frozen follow-ups and explicit fixed/kept facing.</summary>
/// <remarks>ZOD-315; source 11464943bbdbd2a0af022d90cb7f10455cf911df. Stops before the next clock; no corpse/terminal policy or automatic facing.</remarks>
public static class RosterBasicSkillAiTurnExecution
{
    public static RosterBasicSkillAiTurnResult Execute(RosterFacingTurnState state, RosterAiRefreshRequest request,
        RosterBasicSkillAiTurnConfiguration configuration, AiPriorityWorkspace? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(configuration);
        workspace ??= AiPriorityWorkspace.StartUnknown(state.Battle.TurnBattle.Battle.Map); workspace.ValidateMap(state.Battle);
        // An existing receipt remains the authority for its actor, even though UNIT_TURN has already ended.
        var stage = state.PendingFacing is not null ? BasicSkillAiTurnStage.FinishFacing :
            state.Battle.TurnBattle.Turns.Units.Any(u => u.Id == request.ActorId && u.ActionTaken)
                ? BasicSkillAiTurnStage.PostActCommand : BasicSkillAiTurnStage.FirstCommand;
        return Run(state, new(request, configuration, workspace), stage, null);
    }

    public static RosterBasicSkillAiTurnResult Resume(RosterFacingTurnState state, RosterBasicSkillAiTurnRecovery recovery)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(recovery);
        if (!ReferenceEquals(state, recovery.ExpectedState))
            return new(state, [], [], recovery.Inputs.Workspace, new(BasicSkillAiTurnStage.RecoveryBinding), null);
        return Run(state, recovery.Inputs, recovery.NextStep, recovery.Continuation);
    }

    private static RosterBasicSkillAiTurnResult Run(RosterFacingTurnState state, RosterBasicSkillAiTurnInputs inputs,
        BasicSkillAiTurnStage stage, RosterMoveContinuation? continuation)
    {
        var events = ImmutableArray.CreateBuilder<AttackResultEvent>();
        var decisions = ImmutableArray.CreateBuilder<RosterBasicSkillAiCommandOutcome>();
        var workspace = inputs.Workspace; var request = inputs.Request; var c = inputs.Configuration;
        RosterBasicSkillAiTurnResult Failed(RosterBasicSkillAiTurnFailure failure) => new(state, events.ToImmutable(), decisions.ToImmutable(), workspace,
            failure, new(state, stage, inputs with { Workspace = workspace }, continuation));
        while (true)
        {
            switch (stage)
            {
                case BasicSkillAiTurnStage.FirstCommand:
                case BasicSkillAiTurnStage.PostActCommand:
                    var command = RosterBasicSkillAiCommandExecution.Execute(state, request, c.Simulation, c.Refresh, c.Lists,
                        c.General, c.Movement, c.Selection, c.Dispatch, c.Facing, workspace);
                    if (!command.Accepted) return Failed(new(stage, CommandFailure: command.Failure));
                    state = command.State; workspace = command.Workspace; events.AddRange(command.Events);
                    decisions.Add(command.Outcome!); continuation = command.Continuation;
                    break;
                case BasicSkillAiTurnStage.RetainedCommand:
                    var retained = RosterMoveDecisionBattle.Continue(state, request.ActorId, continuation);
                    if (!retained.Accepted) return Failed(new(stage, ContinuationFailure: retained));
                    state = retained.State; events.AddRange(retained.Events); continuation = null;
                    break;
                case BasicSkillAiTurnStage.FinishFacing:
                    var facing = RosterFacingTurnBattle.FinishFacing(state, request.ActorId, state.PendingFacing, c.Facing);
                    if (!facing.Accepted) return Failed(new(stage, FacingFailure: facing));
                    state = facing.State; events.AddRange(facing.Events);
                    break;
                default: throw new InvalidOperationException("Unexpected bounded roster turn stage.");
            }
            var next = AiTurnProgress.Next(state, request.ActorId, continuation);
            if (next is null) return new(state, events.ToImmutable(), decisions.ToImmutable(), workspace, null, null);
            stage = next.Value;
        }
    }
}
