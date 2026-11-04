using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Diagnostic masks remain distinct from fresh-cell scan scenarios: final scenarios may overlap at the actor.</summary>
/// <remarks>No prior-memory value, action scan, ranked candidate or plan is produced.</remarks>
public sealed record AttackThrowStoneAiOutOfReachScenarioOutcome(AttackThrowStoneAiPreparationOutcome Preparation,
    ImmutableArray<TilePosition> ScoreMask, ImmutableArray<TilePosition> Scenario1, ImmutableArray<TilePosition> Scenario2,
    ImmutableArray<AiScoredMovementTile> Movement, ImmutableArray<AiMovementCellProvenance> Cells,
    TilePosition BaselineDestination, TilePosition ActFirstDestination, int BaselineIndex,
    int TargetDistance, int TargetReach, int EffectiveReach, SwordAiReachAdjustment? ReachAdjustment,
    bool FilterUsedReachableFallback, bool DestinationUsedDistanceFallback);
/// <summary>Failure exposes reasons only and restores incoming whole-query RNG, including after partial private work.</summary>
public sealed record AttackThrowStoneAiOutOfReachScenarioResult(bool Accepted, AttackThrowStoneAiScenarioFailure? Failure,
    AttackThrowStoneAiOutOfReachScenarioOutcome? Outcome, RandomState RngState);
internal sealed record AttackThrowStoneAiOutOfReachScenarioCore(AttackThrowStoneAiOutOfReachScenarioResult Result,
    AiMovementPrefixOutcome? Prefix = null);

/// <summary>Own fresh pair preparation, full proximity, filtered scores and team-specific out-of-reach D0=D1.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-199/200.
/// No unknown priority reads, cached inputs, scans, lottery, command authority or live RNG adoption.</remarks>
public static class AttackThrowStoneAiOutOfReachScenario
{
    public static AttackThrowStoneAiOutOfReachScenarioResult Evaluate(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        AssetId throwStoneAbilityId, BoundedSwordWeapon? weapon, SwordAiCandidateProfile attackProfile,
        ThrowStoneAiCandidateProfile throwStoneProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        AttackThrowStoneAiPreparationProfile preparationProfile, AttackThrowStoneAiScenarioProfile scenarioProfile,
        SwordAiScenarioTeam team, RandomState decisionRng) =>
        EvaluateFresh(state, request, throwStoneAbilityId, weapon, attackProfile, throwStoneProfile, scoreProfile, refreshProfile,
            preparationProfile, scenarioProfile, team, decisionRng).Result;

    // Only an owning query receives this freshly evaluated prefix; public diagnostics cannot be supplied back.
    internal static AttackThrowStoneAiOutOfReachScenarioCore EvaluateFresh(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        AssetId throwStoneAbilityId, BoundedSwordWeapon? weapon, SwordAiCandidateProfile attackProfile,
        ThrowStoneAiCandidateProfile throwStoneProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        AttackThrowStoneAiPreparationProfile preparationProfile, AttackThrowStoneAiScenarioProfile scenarioProfile,
        SwordAiScenarioTeam team, RandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(attackProfile);
        ArgumentNullException.ThrowIfNull(throwStoneProfile); ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(refreshProfile); ArgumentNullException.ThrowIfNull(preparationProfile);
        ArgumentNullException.ThrowIfNull(scenarioProfile);
        AttackThrowStoneAiOutOfReachScenarioCore Reject(AttackThrowStoneAiScenarioFailure failure) => new(new(false, failure, null, decisionRng));
        if (!scenarioProfile.NormalScoring || !scenarioProfile.CanonicalCoordinates || !scenarioProfile.NoOtherScenarioOverrides)
            return Reject(new(AttackThrowStoneAiScenarioStage.Admission, AttackThrowStoneAiScenarioRejection.UnsupportedProfile));
        var preparation = AttackThrowStoneAiPreparation.Evaluate(state, request, throwStoneAbilityId, weapon, attackProfile,
            throwStoneProfile, scoreProfile, refreshProfile, preparationProfile, team, decisionRng);
        return ContinueFromPreparation(state, request, attackProfile, throwStoneProfile, preparation, decisionRng);
    }

    // Reuse scoring, masks, provenance and the post-score reach draw after either owning entry's fresh preparation.
    internal static AttackThrowStoneAiOutOfReachScenarioCore ContinueFromPreparation(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile attackProfile, ThrowStoneAiCandidateProfile throwStoneProfile,
        AttackThrowStoneAiPreparationResult preparation, RandomState decisionRng)
    {
        AttackThrowStoneAiOutOfReachScenarioCore Reject(AttackThrowStoneAiScenarioFailure failure) => new(new(false, failure, null, decisionRng));
        if (!preparation.Accepted)
            return Reject(new(AttackThrowStoneAiScenarioStage.Preparation, AttackThrowStoneAiScenarioRejection.PreparationRejected, preparation.Failure));
        var ready = preparation.Outcome!;
        var result = AiMovementScenarios.Evaluate(state, request, attackProfile, throwStoneProfile.Units, ready.Common,
            ready.Team, ready.Units.Single(u => u.UnitId == request.ActorId).HasRangedAbility,
            preparation.RngState, decisionRng, outside: true);
        if (!result.Accepted) return Reject(result.Failure!);
        var m = result.Outcome!;
        return new(new(true, null, new(ready, m.ScoreMask, m.Scenario1, m.Scenario2, m.Movement, m.Cells,
            m.BaselineDestination, m.ActFirstDestination, m.BaselineIndex, m.TargetDistance, m.TargetReach, m.EffectiveReach,
            m.ReachAdjustment, m.FilterUsedReachableFallback, m.DestinationUsedDistanceFallback), result.RngState), result.Prefix);
    }
}
