using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public enum AttackThrowStoneAiTurnStage { FreshCommand, RetainedCommand, FinishFacing, RecoveryBinding }
public sealed record AttackThrowStoneAiTurnFailure(AttackThrowStoneAiTurnStage Stage,
    AttackThrowStoneAiCommandFailure? CommandFailure = null,
    FacingMoveContinueResult? ContinuationFailure = null, FacingTurnResult? FacingFailure = null);

/// <summary>An in-memory orchestration cursor, bound to exact partial progress; grants no new Framework authority.</summary>
public sealed class AttackThrowStoneAiTurnRecovery
{
    public AttackThrowStoneAiTurnStage NextStep { get; }
    internal FacingTurnState ExpectedState { get; }
    internal AttackThrowStoneAiSelectionRequest Request { get; }
    internal AttackThrowStoneAiSelectionConfiguration Configuration { get; }
    internal SwordAiCommandDispatchProfile Dispatch { get; }
    internal FacingMoveContinuation? Continuation { get; }
    internal AttackThrowStoneAiTurnRecovery(FacingTurnState state, AttackThrowStoneAiTurnStage step,
        AttackThrowStoneAiSelectionRequest request, AttackThrowStoneAiSelectionConfiguration configuration,
        SwordAiCommandDispatchProfile dispatch, FacingMoveContinuation? continuation)
    { ExpectedState = state; NextStep = step; Request = request; Configuration = configuration; Dispatch = dispatch; Continuation = continuation; }
}

/// <summary>State/events describe published progress even on failure. Resume returns only newly published events/decisions.</summary>
public sealed record AttackThrowStoneAiTurnResult(FacingTurnState State, ImmutableArray<AttackResultEvent> Events,
    ImmutableArray<AttackThrowStoneAiCommandOutcome> Decisions, AttackThrowStoneAiTurnFailure? Failure,
    AttackThrowStoneAiTurnRecovery? Recovery)
{
    public bool Completed => Failure is null;
}

/// <summary>Compose one bounded living-pair turn without advancing the next-turn clock or rolling back published commands.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-220/221/224.
/// No presentation delay, terminal policy, cached-plan input or speculative retry.</remarks>
public static class AttackThrowStoneAiTurnExecution
{
    public static AttackThrowStoneAiTurnResult Execute(FacingTurnState state, AttackThrowStoneAiSelectionRequest request,
        AttackThrowStoneAiSelectionConfiguration configuration, SwordAiCommandDispatchProfile dispatch)
    {
        ArgumentNullException.ThrowIfNull(state);
        AttackThrowStoneAiSelectionPipeline.ValidateInputs(state.Battle, request, configuration);
        ArgumentNullException.ThrowIfNull(dispatch);
        return Run(state, request, configuration, dispatch, AttackThrowStoneAiTurnStage.FreshCommand, null);
    }

    public static AttackThrowStoneAiTurnResult Resume(FacingTurnState state, AttackThrowStoneAiTurnRecovery recovery)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(recovery);
        if (!ReferenceEquals(state, recovery.ExpectedState))
            return new(state, [], [], new(AttackThrowStoneAiTurnStage.RecoveryBinding), null);
        return Run(state, recovery.Request, recovery.Configuration, recovery.Dispatch, recovery.NextStep, recovery.Continuation);
    }

    private static AttackThrowStoneAiTurnResult Run(FacingTurnState state, AttackThrowStoneAiSelectionRequest request,
        AttackThrowStoneAiSelectionConfiguration configuration, SwordAiCommandDispatchProfile dispatch,
        AttackThrowStoneAiTurnStage stage, FacingMoveContinuation? continuation)
    {
        var events = ImmutableArray.CreateBuilder<AttackResultEvent>();
        var decisions = ImmutableArray.CreateBuilder<AttackThrowStoneAiCommandOutcome>();
        AttackThrowStoneAiTurnResult Failed(AttackThrowStoneAiTurnFailure failure) =>
            new(state, events.ToImmutable(), decisions.ToImmutable(), failure, new(state, stage, request, configuration, dispatch, continuation));
        // Every accepted step consumes a live choice, ends the turn or settles a receipt. There is no retry on rejection.
        while (true)
        {
            switch (stage)
            {
                case AttackThrowStoneAiTurnStage.FreshCommand:
                    var command = AttackThrowStoneAiCommandExecution.Execute(state, request, configuration, dispatch);
                    if (!command.Accepted) return Failed(new(stage, CommandFailure: command.Failure));
                    state = command.State; events.AddRange(command.Events); decisions.Add(command.Outcome!);
                    continuation = command.Continuation;
                    break;
                case AttackThrowStoneAiTurnStage.RetainedCommand:
                    var retained = FacingMoveDecisionBattle.Continue(state, request.Turn.ActorId, continuation);
                    if (!retained.Accepted) return Failed(new(stage, ContinuationFailure: retained));
                    state = retained.State; events.AddRange(retained.Events); continuation = null;
                    break;
                case AttackThrowStoneAiTurnStage.FinishFacing:
                    var facing = FacingTurnBattle.FinishFacing(state, request.Turn.ActorId, state.PendingFacing,
                        new FacingChoice.ChooseClosedEnemy(request.Turn.TargetId));
                    if (!facing.Accepted) return Failed(new(stage, FacingFailure: facing));
                    state = facing.State; events.AddRange(facing.Events);
                    break;
                default:
                    throw new InvalidOperationException("Unexpected private turn stage.");
            }
            if (continuation is not null) stage = AttackThrowStoneAiTurnStage.RetainedCommand;
            else if (state.PendingFacing is not null) stage = AttackThrowStoneAiTurnStage.FinishFacing;
            else if (state.Battle.TurnBattle.Turns.ActiveUnitId == request.Turn.ActorId) stage = AttackThrowStoneAiTurnStage.FreshCommand;
            else return new(state, events.ToImmutable(), decisions.ToImmutable(), null, null);
        }
    }
}
