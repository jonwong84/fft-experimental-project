using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Fallback cannot be assumed movement-only when original Defend support is present.</summary>
public sealed record SwordAiPostActSelectionProfile(bool NoDefendSupport = false);
public enum SwordAiPostActSelectionRejection { UnsupportedProfile, ScenariosRejected, LotteryRejected }
public sealed record SwordAiPostActSelectionFailure(SwordAiPostActSelectionRejection Reason,
    SwordAiPostActScenarioFailure? ScenarioFailure = null, SwordAiSelectionFailure? LotteryFailure = null);
public sealed record SwordAiPostActSelectionOutcome(SwordAiPostActScenarioOutcome Scenarios,
    FftRandomState LotteryEntryRng, ImmutableArray<SwordAiLotteryRoll> Lottery,
    SwordAiSelectionReason Reason, SwordAiSelectedPlan Plan);
/// <summary>No partial preparation, scored cells, rolls or plan; failure returns incoming whole-query RNG.</summary>
public sealed record SwordAiPostActSelectionResult(bool Accepted, SwordAiPostActSelectionFailure? Failure,
    SwordAiPostActSelectionOutcome? Outcome, FftRandomState RngState);

/// <summary>Fresh post-ACT scenarios and empty-ranking fallback; no Attack scan or command authority.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-176/182. No supplied ranked list or cached result.</remarks>
public static class SwordAiPostActSelection
{
    public static SwordAiPostActSelectionResult EvaluateInReach(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, SwordAiPreparationProfile preparationProfile, SwordAiScenarioTeam team,
        SwordAiPostActScenarioProfile scenarioProfile, SwordAiPostActSelectionProfile selectionProfile, FftRandomState decisionRng)
        => EvaluateCore(state, request, candidateProfile, scoreProfile, refreshProfile, preparationProfile, team,
            scenarioProfile, selectionProfile, decisionRng, outOfReach: false);

    public static SwordAiPostActSelectionResult EvaluateOutOfReach(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, SwordAiPreparationProfile preparationProfile, SwordAiScenarioTeam team,
        SwordAiPostActScenarioProfile scenarioProfile, SwordAiPostActSelectionProfile selectionProfile, FftRandomState decisionRng)
        => EvaluateCore(state, request, candidateProfile, scoreProfile, refreshProfile, preparationProfile, team,
            scenarioProfile, selectionProfile, decisionRng, outOfReach: true);

    private static SwordAiPostActSelectionResult EvaluateCore(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, SwordAiPreparationProfile preparationProfile, SwordAiScenarioTeam team,
        SwordAiPostActScenarioProfile scenarioProfile, SwordAiPostActSelectionProfile selectionProfile,
        FftRandomState decisionRng, bool outOfReach)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(refreshProfile);
        ArgumentNullException.ThrowIfNull(preparationProfile);
        ArgumentNullException.ThrowIfNull(scenarioProfile);
        ArgumentNullException.ThrowIfNull(selectionProfile);
        SwordAiPostActSelectionResult Reject(SwordAiPostActSelectionFailure failure) => new(false, failure, null, decisionRng);
        if (!selectionProfile.NoDefendSupport) return Reject(new(SwordAiPostActSelectionRejection.UnsupportedProfile));
        var scenarios = outOfReach
            ? SwordAiPostActScenarios.EvaluateOutOfReach(state, request, candidateProfile, scoreProfile,
                refreshProfile, preparationProfile, team, scenarioProfile, decisionRng)
            : SwordAiPostActScenarios.EvaluateInReach(state, request, candidateProfile, scoreProfile,
                refreshProfile, preparationProfile, team, scenarioProfile, decisionRng);
        if (!scenarios.Accepted)
            return Reject(new(SwordAiPostActSelectionRejection.ScenariosRejected, scenarios.Failure));
        // Owned ordinary-only preparation clears Attack usability after deriving reach. All source ranks stay empty.
        var ranked = new SwordAiScannedCandidate?[8].ToImmutableArray(); // Original AI_RANKED_ACTION_COUNT, not a content limit.
        var lottery = SwordAiSelection.SelectFreshRanks(ranked, request.ActorId,
            scenarios.Outcome!.MovementScenario.Scenario.BaselineDestination, scenarios.RngState, decisionRng);
        if (!lottery.Accepted)
            return Reject(new(SwordAiPostActSelectionRejection.LotteryRejected, LotteryFailure: lottery.Failure));
        return new(true, null, new(scenarios.Outcome, scenarios.RngState, lottery.Outcome!.Rolls,
            lottery.Outcome.Reason, lottery.Outcome.Plan), lottery.RngState);
    }
}
