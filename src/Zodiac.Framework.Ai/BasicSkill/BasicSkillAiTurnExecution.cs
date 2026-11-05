using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>One shared preparation owner for the fresh pre-ACT and actual post-ACT decisions.</summary>
public sealed record BasicSkillAiTurnConfiguration(BasicSkillAiProfile BeforeAct,
    BasicSkillAiSelectionProfile Selection, SwordAiPostActScenarioProfile PostActScenarios)
{
    internal BasicSkillAiPostActConfiguration AfterAct => new(BeforeAct.Preparation, PostActScenarios,
        BeforeAct.SourceGeneralEntry, BeforeAct.FreshEmptyRankedList, Selection.NoDefendSupport);
}
/// <summary>Immutable derived pre-ACT entry plus the existing terminal/post-ACT policy assertions.</summary>
public sealed record BasicSkillAiDerivedTurnConfiguration(BasicSkillAiDerivedProfile BeforeAct,
    BasicSkillAiSelectionProfile Selection, SwordAiPostActScenarioProfile PostActScenarios)
{
    internal BasicSkillAiPostActConfiguration AfterAct => new(BeforeAct.Preparation, PostActScenarios,
        BeforeAct.SourceGeneralEntry, BeforeAct.FreshEmptyRankedList, Selection.NoDefendSupport);
}
public enum BasicSkillAiTurnStage { FirstCommand, RetainedCommand, PostActCommand, FinishFacing, RecoveryBinding }
public sealed record BasicSkillAiTurnFailure(BasicSkillAiTurnStage Stage,
    BasicSkillAiCommandFailure? FirstFailure = null, BasicSkillAiPostActExecutionFailure? PostActFailure = null,
    FacingMoveContinueResult? ContinuationFailure = null, FacingTurnResult? FacingFailure = null);
/// <summary>Successful fresh-command diagnostics only; these records grant no command authority.</summary>
public abstract record BasicSkillAiTurnDecision
{
    private protected BasicSkillAiTurnDecision() { }
    public sealed record BeforeAct(BasicSkillAiCommandOutcome Outcome) : BasicSkillAiTurnDecision;
    public sealed record AfterAct(BasicSkillAiPostActExecutionOutcome Outcome) : BasicSkillAiTurnDecision;
}

// Frozen only by the two closed owning factories; no public selector/decision callback is accepted.
internal sealed record BasicSkillAiTurnInputs(AccumulateAiPreparationRequest Request,
    BasicSkillAiPostActConfiguration AfterAct, SwordAiCommandDispatchProfile Dispatch,
    Func<FacingTurnState, AiPriorityWorkspace?, BasicSkillAiTurnFirstResult> First, AiPriorityWorkspace? Workspace = null);
internal sealed record BasicSkillAiTurnFirstResult(BasicSkillAiCommandResult Command, AiPriorityWorkspace? Workspace);
internal sealed record BasicSkillAiTurnRun(BasicSkillAiTurnResult Turn, AiPriorityWorkspace? Workspace);
public sealed record BasicSkillAiWorkspaceTurnResult(BasicSkillAiTurnResult Turn, AiPriorityWorkspace Workspace);

/// <summary>Opaque in-memory cursor bound to exact partial progress and frozen inputs; not a save or new Framework authority.</summary>
public sealed class BasicSkillAiTurnRecovery
{
    public BasicSkillAiTurnStage NextStep { get; }
    internal FacingTurnState ExpectedState { get; }
    internal BasicSkillAiTurnInputs Inputs { get; }
    internal FacingMoveContinuation? Continuation { get; }
    internal BasicSkillAiTurnRecovery(FacingTurnState state, BasicSkillAiTurnStage stage,
        BasicSkillAiTurnInputs inputs, FacingMoveContinuation? continuation)
    { ExpectedState = state; NextStep = stage; Inputs = inputs; Continuation = continuation; }
}

/// <summary>Failure preserves published progress. Resume reports only newly published events and fresh decisions.</summary>
public sealed record BasicSkillAiTurnResult(FacingTurnState State, ImmutableArray<AttackResultEvent> Events,
    ImmutableArray<BasicSkillAiTurnDecision> Decisions, BasicSkillAiTurnFailure? Failure, BasicSkillAiTurnRecovery? Recovery)
{
    public bool Completed => Failure is null;
}

/// <summary>Complete one supplied or derived scenario three-entry turn through existing command, continuation and facing owners.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-247/248/254. No whole-turn rollback,
/// speculative retry, next-turn clock, terminal policy, cached-plan input or host activation.</remarks>
public static class BasicSkillAiTurnExecution
{
    public static BasicSkillAiWorkspaceTurnResult ExecuteWithWorkspace(FacingTurnState state, AccumulateAiPreparationRequest request,
        BasicSkillAiDerivedTurnConfiguration configuration, SwordAiCommandDispatchProfile dispatch, AiPriorityWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var result = Run(state, DerivedInputs(state, request, configuration, dispatch, workspace), BasicSkillAiTurnStage.FirstCommand, null);
        return new(result.Turn, result.Workspace!);
    }

    public static BasicSkillAiTurnResult ExecuteDerived(FacingTurnState state, AccumulateAiPreparationRequest request,
        BasicSkillAiDerivedTurnConfiguration configuration, SwordAiCommandDispatchProfile dispatch) =>
        Run(state, DerivedInputs(state, request, configuration, dispatch, null), BasicSkillAiTurnStage.FirstCommand, null).Turn;

    private static BasicSkillAiTurnInputs DerivedInputs(FacingTurnState state, AccumulateAiPreparationRequest request,
        BasicSkillAiDerivedTurnConfiguration configuration, SwordAiCommandDispatchProfile dispatch, AiPriorityWorkspace? workspace)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(configuration);
        BasicSkillAiDerivedComposition.ValidateArguments(state.Battle, request, configuration.BeforeAct);
        ArgumentNullException.ThrowIfNull(configuration.Selection); ArgumentNullException.ThrowIfNull(configuration.PostActScenarios);
        ArgumentNullException.ThrowIfNull(dispatch);
        workspace?.ValidateMap(state.Battle);
        return new(request, configuration.AfterAct, dispatch, (current, history) => {
            if (history is null) return new(BasicSkillAiCommandExecution.ExecuteDerived(current, request, configuration.BeforeAct, configuration.Selection, dispatch), null);
            var first = BasicSkillAiCommandExecution.ExecuteWithWorkspace(current, request, configuration.BeforeAct, configuration.Selection, dispatch, history);
            return new(first.Command, first.Workspace);
        }, workspace);
    }

    public static BasicSkillAiTurnResult Execute(FacingTurnState state, AccumulateAiPreparationRequest request,
        ThrowStoneAiScanScenario scenario, BasicSkillAiTurnConfiguration configuration, SwordAiCommandDispatchProfile dispatch)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(configuration);
        BasicSkillAiComposition.ValidateArguments(state.Battle, request, scenario, configuration.BeforeAct);
        ArgumentNullException.ThrowIfNull(configuration.Selection); ArgumentNullException.ThrowIfNull(configuration.PostActScenarios);
        ArgumentNullException.ThrowIfNull(dispatch);
        return Run(state, new(request, configuration.AfterAct, dispatch,
            (current, history) => new(BasicSkillAiCommandExecution.Execute(current, request, scenario, configuration.BeforeAct, configuration.Selection, dispatch), history)),
            BasicSkillAiTurnStage.FirstCommand, null).Turn;
    }

    public static BasicSkillAiTurnResult Resume(FacingTurnState state, BasicSkillAiTurnRecovery recovery)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(recovery);
        if (!ReferenceEquals(state, recovery.ExpectedState))
            return new(state, [], [], new(BasicSkillAiTurnStage.RecoveryBinding), null);
        return Run(state, recovery.Inputs, recovery.NextStep, recovery.Continuation).Turn;
    }

    public static BasicSkillAiWorkspaceTurnResult ResumeWithWorkspace(FacingTurnState state, BasicSkillAiTurnRecovery recovery)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(recovery);
        if (recovery.Inputs.Workspace is not { } workspace)
            throw new ArgumentException("Recovery belongs to an entry without priority workspace state.", nameof(recovery));
        if (!ReferenceEquals(state, recovery.ExpectedState))
            return new(new(state, [], [], new(BasicSkillAiTurnStage.RecoveryBinding), null), workspace);
        var result = Run(state, recovery.Inputs, recovery.NextStep, recovery.Continuation);
        return new(result.Turn, result.Workspace!);
    }

    private static BasicSkillAiTurnRun Run(FacingTurnState state, BasicSkillAiTurnInputs inputs,
        BasicSkillAiTurnStage stage, FacingMoveContinuation? continuation)
    {
        var request = inputs.Request;
        var workspace = inputs.Workspace;
        var events = ImmutableArray.CreateBuilder<AttackResultEvent>();
        var decisions = ImmutableArray.CreateBuilder<BasicSkillAiTurnDecision>();
        BasicSkillAiTurnRun Failed(BasicSkillAiTurnFailure failure) => new(new(state, events.ToImmutable(), decisions.ToImmutable(), failure,
            new(state, stage, inputs with { Workspace = workspace }, continuation)), workspace);
        // Each success consumes a choice, ends the turn or settles its actual receipt. Rejection never retries in this call.
        while (true)
        {
            switch (stage)
            {
                case BasicSkillAiTurnStage.FirstCommand:
                    var owned = inputs.First(state, workspace); var first = owned.Command;
                    if (!first.Accepted) return Failed(new(stage, FirstFailure: first.Failure));
                    workspace = owned.Workspace;
                    state = first.State; events.AddRange(first.Events); decisions.Add(new BasicSkillAiTurnDecision.BeforeAct(first.Outcome!));
                    continuation = first.Continuation;
                    break;
                case BasicSkillAiTurnStage.RetainedCommand:
                    var retained = FacingMoveDecisionBattle.Continue(state, request.Turn.ActorId, continuation);
                    if (!retained.Accepted) return Failed(new(stage, ContinuationFailure: retained));
                    state = retained.State; events.AddRange(retained.Events); continuation = null;
                    break;
                case BasicSkillAiTurnStage.PostActCommand:
                    var post = BasicSkillAiPostActExecution.Execute(state, request, inputs.AfterAct, inputs.Dispatch);
                    if (!post.Accepted) return Failed(new(stage, PostActFailure: post.Failure));
                    workspace = workspace?.Write(post.Outcome!.Selection.MovementScenario.Movement);
                    state = post.State; events.AddRange(post.Events); decisions.Add(new BasicSkillAiTurnDecision.AfterAct(post.Outcome!));
                    break;
                case BasicSkillAiTurnStage.FinishFacing:
                    var facing = FacingTurnBattle.FinishFacing(state, request.Turn.ActorId, state.PendingFacing,
                        new FacingChoice.ChooseClosedEnemy(request.Turn.TargetId));
                    if (!facing.Accepted) return Failed(new(stage, FacingFailure: facing));
                    state = facing.State; events.AddRange(facing.Events);
                    break;
                default: throw new InvalidOperationException("Unexpected private turn stage.");
            }
            var next = AiTurnProgress.Next(state, request.Turn.ActorId, continuation);
            if (next is null) return new(new(state, events.ToImmutable(), decisions.ToImmutable(), null, null), workspace);
            stage = next.Value;
        }
    }
}
