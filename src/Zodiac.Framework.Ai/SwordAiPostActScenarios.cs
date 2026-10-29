namespace Zodiac.Framework.Ai;

/// <summary>Remaining source facts; fresh preparation derives focus, reach, cache and restored geometry.</summary>
public sealed record SwordAiPostActScenarioProfile(bool NormalScoring = false, bool CanonicalCoordinates = false,
    bool OrdinaryScenarioPolicy = false);
public enum SwordAiPostActScenarioRejection { UnsupportedProfile, PreparationRejected, ScenarioRejected }
/// <summary>Reason-only failure; no partially prepared/scored output or progressed RNG.</summary>
public sealed record SwordAiPostActScenarioFailure(SwordAiPostActScenarioRejection Reason,
    SwordAiPreparationRejection PreparationRejection = SwordAiPreparationRejection.None,
    SwordAiRefreshRejection RefreshRejection = SwordAiRefreshRejection.None,
    SwordAiMovementScenarioFailure? ScenarioFailure = null);
public sealed record SwordAiPostActScenarioOutcome(SwordAiPreparationOutcome Preparation,
    SwordAiMovementScenarioOutcome MovementScenario, SwordAiOutOfReachScenarioOutcome? OutOfReach);
public sealed record SwordAiPostActScenarioResult(bool Accepted, SwordAiPostActScenarioFailure? Failure,
    SwordAiPostActScenarioOutcome? Outcome, FftRandomState RngState);

/// <summary>Own fresh post-ACT preparation and source-ordered movement scoring; no Attack scan or lottery.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-176/181. No cached outcomes or command authority.</remarks>
public static class SwordAiPostActScenarios
{
    public static SwordAiPostActScenarioResult EvaluateInReach(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, SwordAiPreparationProfile preparationProfile,
        SwordAiScenarioTeam team, SwordAiPostActScenarioProfile scenarioProfile, FftRandomState decisionRng)
        => EvaluateCore(state, request, candidateProfile, scoreProfile, refreshProfile, preparationProfile, team,
            scenarioProfile, decisionRng, outOfReach: false);

    public static SwordAiPostActScenarioResult EvaluateOutOfReach(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, SwordAiPreparationProfile preparationProfile,
        SwordAiScenarioTeam team, SwordAiPostActScenarioProfile scenarioProfile, FftRandomState decisionRng)
        => EvaluateCore(state, request, candidateProfile, scoreProfile, refreshProfile, preparationProfile, team,
            scenarioProfile, decisionRng, outOfReach: true);

    private static SwordAiPostActScenarioResult EvaluateCore(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, SwordAiPreparationProfile preparationProfile,
        SwordAiScenarioTeam team, SwordAiPostActScenarioProfile scenarioProfile, FftRandomState decisionRng, bool outOfReach)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(refreshProfile);
        ArgumentNullException.ThrowIfNull(preparationProfile);
        ArgumentNullException.ThrowIfNull(scenarioProfile);
        SwordAiPostActScenarioResult Reject(SwordAiPostActScenarioFailure failure) => new(false, failure, null, decisionRng);
        if (!scenarioProfile.NormalScoring || !scenarioProfile.CanonicalCoordinates || !scenarioProfile.OrdinaryScenarioPolicy)
            return Reject(new(SwordAiPostActScenarioRejection.UnsupportedProfile));
        var preparation = SwordAiPreparation.EvaluateAfterAct(state, request, candidateProfile, scoreProfile,
            refreshProfile, preparationProfile, team, decisionRng);
        if (!preparation.Accepted)
            return Reject(new(SwordAiPostActScenarioRejection.PreparationRejected, preparation.Rejection, preparation.RefreshRejection));
        var comparison = new SwordAiComparisonProfile(request.ActorId, request.TargetId, FreshDecisionBaseline: true);
        var profile = new SwordAiMovementScenarioProfile(FocusedOpponent: true, OrdinarySwordReach: true, OrdinaryScenarioPolicy: true);
        SwordAiMovementScenarioResult movement;
        SwordAiOutOfReachScenarioOutcome? reach = null;
        if (outOfReach)
        {
            var result = SwordAiMovementScenario.EvaluateOutOfReachAfterAct(state, candidateProfile, scoreProfile,
                comparison, profile, team, preparation.RngState);
            reach = result.Outcome;
            movement = new(result.Accepted, result.Failure, reach?.MovementScenario, result.RngState);
        }
        else movement = SwordAiMovementScenario.EvaluateAfterAct(state, candidateProfile, scoreProfile, comparison, profile, preparation.RngState);
        if (!movement.Accepted)
            return Reject(new(SwordAiPostActScenarioRejection.ScenarioRejected, ScenarioFailure: movement.Failure));
        return new(true, null, new(preparation.Outcome!, movement.Outcome!, reach), movement.RngState);
    }
}
