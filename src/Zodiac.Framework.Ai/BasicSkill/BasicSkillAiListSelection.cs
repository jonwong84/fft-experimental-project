using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Remaining terminal policy facts; the final entered command is derived from occurrence traversal.</summary>
public sealed record BasicSkillAiListSelectionProfile(bool NoDefendSupport = false, bool FreshOrdinarySelectionDefaults = false);
public sealed record BasicSkillAiListSelectionOutcome(BasicSkillAiListCompositionOutcome Composition, RandomState LotteryEntryRng,
    ImmutableArray<AiLotteryRoll> Lottery, int? SelectedRankedIndex, AiLotteryReason Reason, BasicSkillAiSelectedPlan Plan,
    int? SelectedOccurrenceIndex);
public sealed record BasicSkillAiListSelectionResult(bool Accepted, BasicSkillAiListFailure? Failure,
    BasicSkillAiListSelectionOutcome? Outcome, RandomState RngState, AiPriorityWorkspace Workspace);

/// <summary>One private variable-list composition and existing lottery. Failed work publishes neither RNG nor priority writes.</summary>
internal static class BasicSkillAiListSelection
{
    public static BasicSkillAiListSelectionResult Evaluate(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        BasicSkillAiListProfile profile, BasicSkillAiListSelectionProfile selection, RandomState rng, AiPriorityWorkspace? workspace = null) =>
        EvaluateCore(state, request, profile, selection, rng, workspace, false);

    internal static BasicSkillAiListSelectionResult EvaluateCore(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        BasicSkillAiListProfile profile, BasicSkillAiListSelectionProfile selection, RandomState rng, AiPriorityWorkspace? workspace, bool afterAct)
    {
        BasicSkillAiListScenarios.ValidateArguments(state, request, profile); ArgumentNullException.ThrowIfNull(selection);
        workspace ??= AiPriorityWorkspace.StartUnknown(state.TurnBattle.Battle.Map); workspace.ValidateMap(state);
        BasicSkillAiListSelectionResult Reject(BasicSkillAiListFailure failure) => new(false, failure, null, rng, workspace);
        if (!selection.NoDefendSupport || !selection.FreshOrdinarySelectionDefaults) return Reject(new(BasicSkillAiDerivedStage.Admission));
        var composed = BasicSkillAiListComposition.EvaluateCore(state, request, profile, rng, workspace, afterAct);
        if (!composed.Accepted) return Reject(composed.Failure!);
        var c = composed.Outcome!;
        var lottery = AiRankedLottery.Evaluate(c.RankedActions.Select(r => r?.Action.Score.HitPercent ?? (byte)0).ToImmutableArray(), composed.RngState);
        if (!lottery.Accepted) return Reject(new(BasicSkillAiDerivedStage.Lottery, LotteryIndex: lottery.FailedIndex));
        var l = lottery.Outcome!; var selected = l.SelectedIndex is { } i ? c.RankedActions[i] : null;
        var plan = new BasicSkillAiSelectedPlan(request.Turn.ActorId, selected?.Action.Destination ?? c.Scenarios.Movement.BaselineDestination, selected?.Action);
        return new(true, null, new(c, composed.RngState, l.Rolls, l.SelectedIndex, l.Reason, plan, selected?.OccurrenceIndex), lottery.RngState, composed.Workspace);
    }
}
