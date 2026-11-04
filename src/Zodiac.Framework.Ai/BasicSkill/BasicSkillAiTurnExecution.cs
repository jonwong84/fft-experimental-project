using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>One shared preparation owner for the fresh pre-ACT and actual post-ACT decisions.</summary>
public sealed record BasicSkillAiTurnConfiguration(BasicSkillAiProfile BeforeAct,
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

/// <summary>Opaque in-memory cursor bound to exact partial progress and frozen inputs; not a save or new Framework authority.</summary>
public sealed class BasicSkillAiTurnRecovery
{
    public BasicSkillAiTurnStage NextStep { get; }
    internal FacingTurnState ExpectedState { get; }
    internal AccumulateAiPreparationRequest Request { get; }
    internal ThrowStoneAiScanScenario Scenario { get; }
    internal BasicSkillAiTurnConfiguration Configuration { get; }
    internal SwordAiCommandDispatchProfile Dispatch { get; }
    internal FacingMoveContinuation? Continuation { get; }
    internal BasicSkillAiTurnRecovery(FacingTurnState state, BasicSkillAiTurnStage stage,
        AccumulateAiPreparationRequest request, ThrowStoneAiScanScenario scenario, BasicSkillAiTurnConfiguration configuration,
        SwordAiCommandDispatchProfile dispatch, FacingMoveContinuation? continuation)
    { ExpectedState = state; NextStep = stage; Request = request; Scenario = scenario; Configuration = configuration; Dispatch = dispatch; Continuation = continuation; }
}

/// <summary>Failure preserves published progress. Resume reports only newly published events and fresh decisions.</summary>
public sealed record BasicSkillAiTurnResult(FacingTurnState State, ImmutableArray<AttackResultEvent> Events,
    ImmutableArray<BasicSkillAiTurnDecision> Decisions, BasicSkillAiTurnFailure? Failure, BasicSkillAiTurnRecovery? Recovery)
{
    public bool Completed => Failure is null;
}

/// <summary>Complete one supplied-scenario three-entry turn through existing command, continuation and facing owners.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-247/248. No whole-turn rollback,
/// speculative retry, next-turn clock, terminal policy, cached-plan input or host activation.</remarks>
public static class BasicSkillAiTurnExecution
{
    public static BasicSkillAiTurnResult Execute(FacingTurnState state, AccumulateAiPreparationRequest request,
        ThrowStoneAiScanScenario scenario, BasicSkillAiTurnConfiguration configuration, SwordAiCommandDispatchProfile dispatch)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(configuration);
        BasicSkillAiComposition.ValidateArguments(state.Battle, request, scenario, configuration.BeforeAct);
        ArgumentNullException.ThrowIfNull(configuration.Selection); ArgumentNullException.ThrowIfNull(configuration.PostActScenarios);
        ArgumentNullException.ThrowIfNull(dispatch);
        return Run(state, request, scenario, configuration, dispatch, BasicSkillAiTurnStage.FirstCommand, null);
    }

    public static BasicSkillAiTurnResult Resume(FacingTurnState state, BasicSkillAiTurnRecovery recovery)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(recovery);
        if (!ReferenceEquals(state, recovery.ExpectedState))
            return new(state, [], [], new(BasicSkillAiTurnStage.RecoveryBinding), null);
        return Run(state, recovery.Request, recovery.Scenario, recovery.Configuration, recovery.Dispatch, recovery.NextStep, recovery.Continuation);
    }

    private static BasicSkillAiTurnResult Run(FacingTurnState state, AccumulateAiPreparationRequest request,
        ThrowStoneAiScanScenario scenario, BasicSkillAiTurnConfiguration configuration, SwordAiCommandDispatchProfile dispatch,
        BasicSkillAiTurnStage stage, FacingMoveContinuation? continuation)
    {
        var events = ImmutableArray.CreateBuilder<AttackResultEvent>();
        var decisions = ImmutableArray.CreateBuilder<BasicSkillAiTurnDecision>();
        BasicSkillAiTurnResult Failed(BasicSkillAiTurnFailure failure) => new(state, events.ToImmutable(), decisions.ToImmutable(), failure,
            new(state, stage, request, scenario, configuration, dispatch, continuation));
        // Each success consumes a choice, ends the turn or settles its actual receipt. Rejection never retries in this call.
        while (true)
        {
            switch (stage)
            {
                case BasicSkillAiTurnStage.FirstCommand:
                    var first = BasicSkillAiCommandExecution.Execute(state, request, scenario, configuration.BeforeAct, configuration.Selection, dispatch);
                    if (!first.Accepted) return Failed(new(stage, FirstFailure: first.Failure));
                    state = first.State; events.AddRange(first.Events); decisions.Add(new BasicSkillAiTurnDecision.BeforeAct(first.Outcome!));
                    continuation = first.Continuation;
                    break;
                case BasicSkillAiTurnStage.RetainedCommand:
                    var retained = FacingMoveDecisionBattle.Continue(state, request.Turn.ActorId, continuation);
                    if (!retained.Accepted) return Failed(new(stage, ContinuationFailure: retained));
                    state = retained.State; events.AddRange(retained.Events); continuation = null;
                    break;
                case BasicSkillAiTurnStage.PostActCommand:
                    var post = BasicSkillAiPostActExecution.Execute(state, request, configuration.AfterAct, dispatch);
                    if (!post.Accepted) return Failed(new(stage, PostActFailure: post.Failure));
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
            if (continuation is not null) stage = BasicSkillAiTurnStage.RetainedCommand;
            else if (state.PendingFacing is not null) stage = BasicSkillAiTurnStage.FinishFacing;
            else if (state.Battle.TurnBattle.Turns.ActiveUnitId == request.Turn.ActorId) stage = BasicSkillAiTurnStage.PostActCommand;
            else return new(state, events.ToImmutable(), decisions.ToImmutable(), null, null);
        }
    }
}
