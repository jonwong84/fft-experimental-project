using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Source terminal-list/default/no-Defend facts, not inferred from the action grants.</summary>
public sealed record BasicSkillAiSelectionProfile(bool TerminalThrowStoneEntry = false,
    bool NoDefendSupport = false, bool FreshOrdinarySelectionDefaults = false);
public enum BasicSkillAiPlanKind { Attack, Accumulate, ThrowStone, MoveOnly }
/// <summary>A hypothetical plan with deferred facing; cannot be passed to a live command transaction.</summary>
public sealed record BasicSkillAiSelectedPlan(BattleUnitId ActorId, TilePosition Destination, BasicSkillAiAction? Action)
{
    public BasicSkillAiPlanKind Kind => Action?.Kind switch
    { BasicSkillAiActionKind.Attack => BasicSkillAiPlanKind.Attack, BasicSkillAiActionKind.Accumulate => BasicSkillAiPlanKind.Accumulate,
        BasicSkillAiActionKind.ThrowStone => BasicSkillAiPlanKind.ThrowStone, _ => BasicSkillAiPlanKind.MoveOnly };
    public bool ActBeforeMove => Action?.ActBeforeMove ?? false;
    public bool ChooseFacingAtWait => true;
}
public enum BasicSkillAiSelectionStage { Admission, Composition, Lottery }
public sealed record BasicSkillAiSelectionFailure(BasicSkillAiSelectionStage Stage, BasicSkillAiFailure? Composition = null, int? LotteryIndex = null);
public sealed record BasicSkillAiSelectionOutcome(BasicSkillAiCompositionOutcome Composition, RandomState LotteryEntryRng,
    ImmutableArray<AiLotteryRoll> Lottery, int? SelectedRankedIndex, AiLotteryReason Reason, BasicSkillAiSelectedPlan Plan);
/// <summary>Failure discards the whole prefix/ranks/lottery/plan and returns the query's incoming RNG.</summary>
public sealed record BasicSkillAiSelectionResult(bool Accepted, BasicSkillAiSelectionFailure? Failure,
    BasicSkillAiSelectionOutcome? Outcome, RandomState RngState);

/// <summary>Fresh three-entry supplied-scenario composition followed by the shared terminal source lottery.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-233/236. No cached plan/rank input,
/// live choice/RNG adoption, self-command authority, derived scenarios or presentation activation.</remarks>
public static class BasicSkillAiSelection
{
    public static BasicSkillAiSelectionResult Evaluate(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        ThrowStoneAiScanScenario scenario, BasicSkillAiProfile profile, BasicSkillAiSelectionProfile selection, RandomState decisionRng)
    {
        BasicSkillAiComposition.ValidateArguments(state, request, scenario, profile); ArgumentNullException.ThrowIfNull(selection);
        if (!selection.TerminalThrowStoneEntry || !selection.NoDefendSupport || !selection.FreshOrdinarySelectionDefaults)
            return new(false, new(BasicSkillAiSelectionStage.Admission), null, decisionRng);
        var composition = BasicSkillAiComposition.Evaluate(state, request, scenario, profile, decisionRng);
        if (!composition.Accepted) return new(false, new(BasicSkillAiSelectionStage.Composition, composition.Failure), null, decisionRng);
        var fresh = composition.Outcome!;
        var lottery = AiRankedLottery.Evaluate(fresh.RankedActions.Select(a => a?.Score.HitPercent ?? (byte)0).ToImmutableArray(), composition.RngState);
        if (!lottery.Accepted) return new(false, new(BasicSkillAiSelectionStage.Lottery, LotteryIndex: lottery.FailedIndex), null, decisionRng);
        var chosen = lottery.Outcome!;
        var action = chosen.SelectedIndex is { } i ? fresh.RankedActions[i] : null;
        var plan = new BasicSkillAiSelectedPlan(request.Turn.ActorId, action?.Destination ?? scenario.BaselineDestination, action);
        return new(true, null, new(fresh, composition.RngState, chosen.Rolls, chosen.SelectedIndex, chosen.Reason, plan), lottery.RngState);
    }
}
