using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record BasicSkillAiDerivedSelectionOutcome(BasicSkillAiScenarioOutcome Scenarios,
    ImmutableArray<TilePosition> ScanOrigins, BasicSkillAiSelectionOutcome Selection);
public sealed record BasicSkillAiDerivedSelectionResult(bool Accepted, BasicSkillAiDerivedFailure? Failure,
    BasicSkillAiDerivedSelectionOutcome? Outcome, RandomState RngState);
public sealed record BasicSkillAiWorkspaceSelectionResult(BasicSkillAiDerivedSelectionResult Query,
    AiPriorityWorkspace Workspace, ImmutableArray<AiCastOriginPriority> CastOrigins);

/// <summary>Privately derive provenance-safe ranks and run the existing terminal lottery exactly once.</summary>
/// <remarks>ZOD-249/252. No cached inputs, public score assertions, command authority or live RNG adoption.</remarks>
public static class BasicSkillAiDerivedSelection
{
    public static BasicSkillAiDerivedSelectionResult Evaluate(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        BasicSkillAiDerivedProfile profile, BasicSkillAiSelectionProfile selection, RandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state);
        return EvaluateWithWorkspace(state, request, profile, selection,
            AiPriorityWorkspace.StartUnknown(state.TurnBattle.Battle.Map), decisionRng).Query;
    }

    public static BasicSkillAiWorkspaceSelectionResult EvaluateWithWorkspace(AttackBattleSnapshot state,
        AccumulateAiPreparationRequest request, BasicSkillAiDerivedProfile profile, BasicSkillAiSelectionProfile selection,
        AiPriorityWorkspace workspace, RandomState decisionRng)
    {
        BasicSkillAiDerivedComposition.ValidateArguments(state, request, profile); ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(workspace); workspace.ValidateMap(state);
        BasicSkillAiWorkspaceSelectionResult Reject(BasicSkillAiDerivedFailure failure) => new(new(false, failure, null, decisionRng), workspace, []);
        if (!selection.TerminalThrowStoneEntry || !selection.NoDefendSupport || !selection.FreshOrdinarySelectionDefaults)
            return Reject(new(BasicSkillAiDerivedStage.Admission));
        var owned = BasicSkillAiDerivedComposition.EvaluateWithWorkspace(state, request, profile, workspace, decisionRng);
        var composition = owned.Query;
        if (!composition.Accepted) return Reject(composition.Failure!);
        var fresh = composition.Outcome!;
        var selected = BasicSkillAiSelection.SelectFresh(new(true, null, fresh.Composition, composition.RngState),
            request.Turn.ActorId, fresh.Scenarios.Movement.BaselineDestination, decisionRng);
        if (!selected.Accepted) return Reject(new(BasicSkillAiDerivedStage.Lottery, LotteryIndex: selected.Failure!.LotteryIndex));
        return new(new(true, null, new(fresh.Scenarios, fresh.ScanOrigins, selected.Outcome!), selected.RngState), owned.Workspace, owned.CastOrigins);
    }

    // The live command owner keeps its existing result shape. New stages distinguish provenance/scenario rejection.
    internal static BasicSkillAiSelectionResult ForCommand(BasicSkillAiDerivedSelectionResult result)
    {
        if (result.Accepted) return new(true, null, result.Outcome!.Selection, result.RngState);
        var f = result.Failure!;
        var failure = f.Stage switch {
            BasicSkillAiDerivedStage.Admission => new BasicSkillAiSelectionFailure(BasicSkillAiSelectionStage.Admission),
            BasicSkillAiDerivedStage.Provenance => new(BasicSkillAiSelectionStage.Provenance),
            BasicSkillAiDerivedStage.Composition => new(BasicSkillAiSelectionStage.Composition, f.Composition),
            BasicSkillAiDerivedStage.Lottery => new(BasicSkillAiSelectionStage.Lottery, LotteryIndex: f.LotteryIndex),
            _ => new(BasicSkillAiSelectionStage.Scenarios, f.Scenarios!.Preparation is { } p
                ? new(BasicSkillAiStage.Preparation, Preparation: p)
                : new(BasicSkillAiStage.Movement, f.Scenarios.Movement?.MovementIndex, Movement: f.Scenarios.Movement?.MovementReason ?? SwordAiMovementBaselineRejection.None))
        };
        return new(false, failure, null, result.RngState);
    }
}
