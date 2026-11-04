using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Unrepresented source facts; other overrides exclude stay-near, reflected and crystal behavior, allowing derived ranged policy.</summary>
public sealed record AttackThrowStoneAiScenarioProfile(bool NormalScoring = false, bool CanonicalCoordinates = false,
    bool NoOtherScenarioOverrides = false);
public enum AttackThrowStoneAiScenarioStage { Admission, Preparation, Branch, Movement, ReachAdjustment }
public enum AttackThrowStoneAiScenarioRejection { UnsupportedProfile, PreparationRejected, OutOfReach, MovementRejected, InReach, RandomDrawCountExhausted }
public sealed record AttackThrowStoneAiScenarioFailure(AttackThrowStoneAiScenarioStage Stage, AttackThrowStoneAiScenarioRejection Reason,
    AttackThrowStoneAiPreparationFailure? PreparationFailure = null, int? MovementIndex = null,
    SwordAiMovementBaselineRejection MovementReason = SwordAiMovementBaselineRejection.None);
/// <summary>BaselineDistanceLimit is absent when post-ACT copies D1; existing pre-ACT entries retain their derived bound.</summary>
public sealed record AttackThrowStoneAiMovementScenarioOutcome(AttackThrowStoneAiPreparationOutcome Preparation,
    ThrowStoneAiScanScenario Scenario, ImmutableArray<AiScoredMovementTile> Movement, int BaselineIndex,
    int TargetDistance, int TargetReach, int? BaselineDistanceLimit, bool BaselineUsedDistanceFallback);
/// <summary>Failure exposes no partial preparation/scenario/cache and restores incoming whole-query RNG.</summary>
public sealed record AttackThrowStoneAiMovementScenarioResult(bool Accepted, AttackThrowStoneAiScenarioFailure? Failure,
    AttackThrowStoneAiMovementScenarioOutcome? Outcome, RandomState RngState);
internal sealed record AttackThrowStoneAiScenarioCore(AttackThrowStoneAiMovementScenarioResult Result, AiMovementPrefixOutcome? Prefix = null);

/// <summary>Own fresh both-unused pair preparation and one in-reach movement prefix; derive D0/D1 with ranged policy.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-195/197.
/// No cached inputs, out-of-reach adjustment, action scans, lottery, authority or live RNG adoption.</remarks>
public static class AttackThrowStoneAiMovementScenario
{
    public static AttackThrowStoneAiMovementScenarioResult Evaluate(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        AssetId throwStoneAbilityId, BoundedSwordWeapon? weapon, SwordAiCandidateProfile attackProfile,
        ThrowStoneAiCandidateProfile throwStoneProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        AttackThrowStoneAiPreparationProfile preparationProfile, AttackThrowStoneAiScenarioProfile scenarioProfile,
        SwordAiScenarioTeam team, RandomState decisionRng) =>
        EvaluateFresh(state, request, throwStoneAbilityId, weapon, attackProfile, throwStoneProfile, scoreProfile, refreshProfile,
            preparationProfile, scenarioProfile, team, decisionRng).Result;

    // Only an owning fresh entry can receive this private prefix; public outcomes cannot be supplied back.
    internal static AttackThrowStoneAiScenarioCore EvaluateFresh(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        AssetId throwStoneAbilityId, BoundedSwordWeapon? weapon, SwordAiCandidateProfile attackProfile,
        ThrowStoneAiCandidateProfile throwStoneProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        AttackThrowStoneAiPreparationProfile preparationProfile, AttackThrowStoneAiScenarioProfile scenarioProfile,
        SwordAiScenarioTeam team, RandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(attackProfile);
        ArgumentNullException.ThrowIfNull(throwStoneProfile); ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(refreshProfile); ArgumentNullException.ThrowIfNull(preparationProfile);
        ArgumentNullException.ThrowIfNull(scenarioProfile);
        AttackThrowStoneAiScenarioCore Reject(AttackThrowStoneAiScenarioFailure failure) => new(new(false, failure, null, decisionRng));
        if (!scenarioProfile.NormalScoring || !scenarioProfile.CanonicalCoordinates || !scenarioProfile.NoOtherScenarioOverrides)
            return Reject(new(AttackThrowStoneAiScenarioStage.Admission, AttackThrowStoneAiScenarioRejection.UnsupportedProfile));
        var preparation = AttackThrowStoneAiPreparation.Evaluate(state, request, throwStoneAbilityId, weapon, attackProfile,
            throwStoneProfile, scoreProfile, refreshProfile, preparationProfile, team, decisionRng);
        return ContinueFromPreparation(state, request, attackProfile, throwStoneProfile, preparation, decisionRng);
    }

    // Both owning entries supply only their own fresh preparation; this is not a public cached-result input.
    internal static AttackThrowStoneAiScenarioCore ContinueFromPreparation(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile attackProfile, ThrowStoneAiCandidateProfile throwStoneProfile,
        AttackThrowStoneAiPreparationResult preparation, RandomState decisionRng)
    {
        AttackThrowStoneAiScenarioCore Reject(AttackThrowStoneAiScenarioFailure failure) => new(new(false, failure, null, decisionRng));
        if (!preparation.Accepted)
            return Reject(new(AttackThrowStoneAiScenarioStage.Preparation, AttackThrowStoneAiScenarioRejection.PreparationRejected, preparation.Failure));
        var ready = preparation.Outcome!;
        var result = AiMovementScenarios.Evaluate(state, request, attackProfile, throwStoneProfile.Units, ready.Common,
            ready.Team, ready.Units.Single(u => u.UnitId == request.ActorId).HasRangedAbility,
            preparation.RngState, decisionRng, outside: false);
        if (!result.Accepted) return Reject(result.Failure!);
        var m = result.Outcome!;
        // The admitted in-reach triangle bound covers all M0. Retain the legacy shared fresh-mask identity as well as its values.
        var scenario = new ThrowStoneAiScanScenario(m.ScoreMask, m.ScoreMask, m.BaselineDestination, m.ActFirstDestination);
        return new(new(true, null, new(ready, scenario, m.Movement, m.BaselineIndex, m.TargetDistance, m.TargetReach,
            m.BaselineDistanceLimit, m.DestinationUsedDistanceFallback), result.RngState), result.Prefix);
    }

    internal static TilePosition Select(ImmutableArray<AiScoredMovementTile> cells, Func<TilePosition, int> distance, bool distanceFirst) =>
        AiMovementScenarios.Select(cells, distance, distanceFirst);
}
