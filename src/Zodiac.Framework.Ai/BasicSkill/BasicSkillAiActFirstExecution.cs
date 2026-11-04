using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public enum BasicSkillAiActFirstRejection { UnavailableChoices, UnsupportedDispatchContext, SelectionRejected, ActFirstRequired, BoundaryRejected }
public sealed record BasicSkillAiActFirstFailure(BasicSkillAiActFirstRejection Reason,
    BasicSkillAiSelectionFailure? Selection = null, FacingDecisionRejection Boundary = FacingDecisionRejection.None);
/// <summary>Selection diagnostics only. Predicted D1 grants no post-ACT Move or continuation.</summary>
public sealed record BasicSkillAiActFirstOutcome(BasicSkillAiSelectionOutcome Decision, RandomState DecisionRng);
public sealed record BasicSkillAiActFirstResult(BasicSkillAiActFirstFailure? Failure,
    FacingDecisionResult<BasicSkillAiActFirstOutcome> Execution)
{
    public bool Accepted => Execution.Accepted;
    public FacingTurnState State => Execution.State;
    public ImmutableArray<AttackResultEvent> Events => Execution.Events;
    public BasicSkillAiActFirstOutcome? Outcome => Execution.Diagnostic;
}

/// <summary>Own fresh supplied-scenario three-entry selection and execute one actual ACT-first command.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-237/239. No cached selection input,
/// derived scenarios, Move-first tokens, post-ACT selection, automatic END or host activation.</remarks>
public static class BasicSkillAiActFirstExecution
{
    public static BasicSkillAiActFirstResult Execute(FacingTurnState state, AccumulateAiPreparationRequest request,
        ThrowStoneAiScanScenario scenario, BasicSkillAiProfile profile, BasicSkillAiSelectionProfile selection,
        SwordAiCommandDispatchProfile dispatch)
    {
        var result = BasicSkillAiCommandPipeline.Execute(state, request, scenario, profile, selection, dispatch,
            BasicSkillAiDispatchMode.ActFirst, (decision, rng, _) => new BasicSkillAiActFirstOutcome(decision, rng));
        var failure = result.Failure is { } f ? new BasicSkillAiActFirstFailure(f.Reason switch {
            BasicSkillAiCommandRejection.UnavailableChoices => BasicSkillAiActFirstRejection.UnavailableChoices,
            BasicSkillAiCommandRejection.UnsupportedDispatchContext => BasicSkillAiActFirstRejection.UnsupportedDispatchContext,
            BasicSkillAiCommandRejection.SelectionRejected => BasicSkillAiActFirstRejection.SelectionRejected,
            BasicSkillAiCommandRejection.DispatchMismatch => BasicSkillAiActFirstRejection.ActFirstRequired,
            _ => BasicSkillAiActFirstRejection.BoundaryRejected
        }, f.Selection, f.Boundary) : null;
        return new(failure, result.Execution);
    }
}
