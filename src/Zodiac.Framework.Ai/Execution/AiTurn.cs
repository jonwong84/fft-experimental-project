using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public enum AiTurnStage { FirstCommand, RetainedCommand, PostActCommand, FinishFacing, RecoveryBinding }
public sealed record AiTurnConfiguration(AiDecisionConfiguration Decision, AiDispatchProfile Dispatch, BattleFacing? Facing = null);
public sealed record AiTurnFailure(AiTurnStage Stage, AiCommandFailure? CommandFailure = null,
    RosterMoveContinueResult? ContinuationFailure = null, RosterFacingResult? FacingFailure = null);
public sealed record AiTurnResult(RosterFacingTurnState State, ImmutableArray<AttackResultEvent> Events,
    ImmutableArray<AiCommandOutcome> Decisions, AiPriorityWorkspace Workspace,
    AiTurnFailure? Failure, AiTurnRecovery? Recovery)
{
    public bool Completed => Failure is null;
}
internal sealed record AiTurnInputs(RosterAiRefreshRequest Request,
    AiTurnConfiguration Configuration, AiPriorityWorkspace Workspace);

/// <summary>Opaque exact-session cursor retaining accepted progress, frozen inputs and the Framework Move token.</summary>
public sealed class AiTurnRecovery
{
    public AiTurnStage NextStep { get; }
    internal RosterFacingTurnState ExpectedState { get; }
    internal AiTurnInputs Inputs { get; }
    internal RosterMoveContinuation? Continuation { get; }
    internal AiTurnRecovery(RosterFacingTurnState state, AiTurnStage step,
        AiTurnInputs inputs, RosterMoveContinuation? continuation)
    { ExpectedState = state; NextStep = step; Inputs = inputs; Continuation = continuation; }
}
