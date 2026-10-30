using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public enum AttackThrowStoneAiPostActSelectionRejection { UnsupportedProfile, ScenariosRejected, LotteryRejected }
public sealed record AttackThrowStoneAiPostActSelectionFailure(AttackThrowStoneAiPostActSelectionRejection Reason,
    AttackThrowStoneAiScenarioFailure? ScenarioFailure = null, int? FailedLotteryIndex = null);
public sealed record AttackThrowStoneAiPostActSelectionOutcome(AttackThrowStoneAiPostActScenarioOutcome Scenarios,
    FftRandomState LotteryEntryRng, ImmutableArray<AiLotteryRoll> Lottery, AiLotteryReason Reason, AttackThrowStoneAiSelectedPlan Plan);
/// <summary>Failure hides all private preparation/scenarios/rolls/plan and restores original query RNG.</summary>
public sealed record AttackThrowStoneAiPostActSelectionResult(bool Accepted, AttackThrowStoneAiPostActSelectionFailure? Failure,
    AttackThrowStoneAiPostActSelectionOutcome? Outcome, FftRandomState RngState);

/// <summary>Own fresh pair post-ACT scenarios and the empty-ranking movement fallback, without an action scan or authority.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-209/213/214. No cached scenario/rank input or live RNG adoption.</remarks>
public static class AttackThrowStoneAiPostActSelection
{
    public static AttackThrowStoneAiPostActSelectionResult EvaluateInReach(AttackBattleSnapshot state,
        AttackThrowStoneAiSelectionRequest request, AttackThrowStoneAiSelectionConfiguration configuration, FftRandomState decisionRng) =>
        EvaluateCore(state, request, configuration, decisionRng, outOfReach: false);

    public static AttackThrowStoneAiPostActSelectionResult EvaluateOutOfReach(AttackBattleSnapshot state,
        AttackThrowStoneAiSelectionRequest request, AttackThrowStoneAiSelectionConfiguration configuration, FftRandomState decisionRng) =>
        EvaluateCore(state, request, configuration, decisionRng, outOfReach: true);

    private static AttackThrowStoneAiPostActSelectionResult EvaluateCore(AttackBattleSnapshot state,
        AttackThrowStoneAiSelectionRequest request, AttackThrowStoneAiSelectionConfiguration configuration, FftRandomState decisionRng, bool outOfReach)
    {
        AttackThrowStoneAiSelectionPipeline.ValidateInputs(state, request, configuration);
        AttackThrowStoneAiPostActSelectionResult Reject(AttackThrowStoneAiPostActSelectionFailure failure) => new(false, failure, null, decisionRng);
        if (!configuration.Selection.NoDefendSupport)
            return Reject(new(AttackThrowStoneAiPostActSelectionRejection.UnsupportedProfile));
        var scenarios = outOfReach
            ? AttackThrowStoneAiPostActScenarios.EvaluateOutOfReach(state, request, configuration, decisionRng)
            : AttackThrowStoneAiPostActScenarios.EvaluateInReach(state, request, configuration, decisionRng);
        if (!scenarios.Accepted)
            return Reject(new(AttackThrowStoneAiPostActSelectionRejection.ScenariosRejected, scenarios.Failure));
        // Owned closed-list preparation clears both actor entries after ACT; non-Frog Frog Attack was already unusable.
        // All ranked slots remain empty, so no origin scan/formula or unknown cast-key read occurs.
        var emptyHits = Enumerable.Repeat((byte)0, 8).ToImmutableArray(); // Original ranked_select slot count, not a content limit.
        var lottery = AiRankedLottery.Evaluate(emptyHits, scenarios.RngState);
        if (!lottery.Accepted)
            return Reject(new(AttackThrowStoneAiPostActSelectionRejection.LotteryRejected, FailedLotteryIndex: lottery.FailedIndex));
        var selected = lottery.Outcome!;
        var plan = new AttackThrowStoneAiSelectedPlan(request.Turn.ActorId, scenarios.Outcome!.D0, null);
        return new(true, null, new(scenarios.Outcome, scenarios.RngState, selected.Rolls, selected.Reason, plan), lottery.RngState);
    }
}
