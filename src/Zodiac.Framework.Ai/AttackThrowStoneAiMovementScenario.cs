using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Unrepresented source facts; other overrides exclude stay-near, reflected and crystal behavior, allowing derived ranged policy.</summary>
public sealed record AttackThrowStoneAiScenarioProfile(bool NormalScoring = false, bool CanonicalCoordinates = false,
    bool NoOtherScenarioOverrides = false);
public enum AttackThrowStoneAiScenarioStage { Admission, Preparation, Branch, Movement, ReachAdjustment }
public enum AttackThrowStoneAiScenarioRejection { UnsupportedProfile, PreparationRejected, OutOfReach, MovementRejected, InReach, RandomDrawCountExhausted }
public sealed record AttackThrowStoneAiScenarioFailure(AttackThrowStoneAiScenarioStage Stage, AttackThrowStoneAiScenarioRejection Reason,
    AttackThrowStoneAiPreparationFailure? PreparationFailure = null, int? MovementIndex = null,
    SwordAiMovementBaselineRejection MovementReason = SwordAiMovementBaselineRejection.None);
public sealed record AttackThrowStoneAiMovementScenarioOutcome(AttackThrowStoneAiPreparationOutcome Preparation,
    ThrowStoneAiScanScenario Scenario, ImmutableArray<AiScoredMovementTile> Movement, int BaselineIndex,
    int TargetDistance, int TargetReach, int BaselineDistanceLimit, bool BaselineUsedDistanceFallback);
/// <summary>Failure exposes no partial preparation/scenario/cache and restores incoming whole-query RNG.</summary>
public sealed record AttackThrowStoneAiMovementScenarioResult(bool Accepted, AttackThrowStoneAiScenarioFailure? Failure,
    AttackThrowStoneAiMovementScenarioOutcome? Outcome, FftRandomState RngState);
internal sealed record AttackThrowStoneAiScenarioCore(AttackThrowStoneAiMovementScenarioResult Result, AiMovementPrefixOutcome? Prefix = null);

/// <summary>Own fresh both-unused NonBlue pair preparation and one in-reach movement prefix; derive D0/D1 with ranged policy.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-195/197.
/// No cached inputs, out-of-reach adjustment, action scans, lottery, authority or live RNG adoption.</remarks>
public static class AttackThrowStoneAiMovementScenario
{
    public static AttackThrowStoneAiMovementScenarioResult Evaluate(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        AssetId throwStoneAbilityId, BoundedSwordWeapon? weapon, SwordAiCandidateProfile attackProfile,
        ThrowStoneAiCandidateProfile throwStoneProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        AttackThrowStoneAiPreparationProfile preparationProfile, AttackThrowStoneAiScenarioProfile scenarioProfile,
        SwordAiScenarioTeam team, FftRandomState decisionRng) =>
        EvaluateFresh(state, request, throwStoneAbilityId, weapon, attackProfile, throwStoneProfile, scoreProfile, refreshProfile,
            preparationProfile, scenarioProfile, team, decisionRng).Result;

    // Only an owning fresh entry can receive this private prefix; public outcomes cannot be supplied back.
    internal static AttackThrowStoneAiScenarioCore EvaluateFresh(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        AssetId throwStoneAbilityId, BoundedSwordWeapon? weapon, SwordAiCandidateProfile attackProfile,
        ThrowStoneAiCandidateProfile throwStoneProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        AttackThrowStoneAiPreparationProfile preparationProfile, AttackThrowStoneAiScenarioProfile scenarioProfile,
        SwordAiScenarioTeam team, FftRandomState decisionRng)
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
        if (!preparation.Accepted)
            return Reject(new(AttackThrowStoneAiScenarioStage.Preparation, AttackThrowStoneAiScenarioRejection.PreparationRejected, preparation.Failure));
        var ready = preparation.Outcome!; var common = ready.Common;
        // Original geometry was already admitted by owned preparation. Read source-equivalent distance rows on restored state.
        var geometry = FlatOrdinaryMovement.Evaluate(state, request.ActorId, attackProfile.Movement).Outcome!;
        int Distance(TilePosition tile) => geometry.TargetDistanceRows.Single(r => r.Y == tile.Y).Distances[tile.X];
        int distance = Distance(common.Origin), reach = common.EnemyAbilityReach;
        if (distance > reach) return Reject(new(AttackThrowStoneAiScenarioStage.Branch, AttackThrowStoneAiScenarioRejection.OutOfReach));
        var origins = common.ReachableOrigins.Where(p => Distance(p) <= common.AllyAbilityReach + distance).ToImmutableArray();
        if (origins.IsEmpty) origins = common.ReachableOrigins; // Source no-match filter restores scenario0.
        // D0 is unknown until scores exist; no provisional destination is asserted to be the baseline.
        var evaluated = AiMovementPrefix.Evaluate(state, request.ActorId, request.TargetId, origins, null, throwStoneProfile.Units, preparation.RngState);
        if (evaluated.Rejection != AiSimulationClockRejection.None)
            return Reject(new(AttackThrowStoneAiScenarioStage.Movement, AttackThrowStoneAiScenarioRejection.MovementRejected,
                MovementIndex: evaluated.FailedIndex, MovementReason: evaluated.Rejection == AiSimulationClockRejection.RandomDrawCountExhausted
                    ? SwordAiMovementBaselineRejection.RandomDrawCountExhausted : SwordAiMovementBaselineRejection.TickCountExhausted));
        var prefix = evaluated.Outcome!;
        var d1 = Select(prefix.Movement, Distance, distanceFirst: false);
        int bound = distance - 1;
        if (ready.Units.Single(u => u.UnitId == request.ActorId).HasRangedAbility &&
            common.Refresh.RefreshedUnits.Single(u => u.UnitId == request.TargetId).Enemy) bound = Math.Max(3, bound);
        var within = prefix.Movement.Where(c => Distance(c.Outcome.Destination) <= bound).ToImmutableArray();
        bool fallback = within.IsEmpty;
        var d0 = Select(fallback ? prefix.Movement : within, Distance, distanceFirst: fallback);
        int baselineIndex = prefix.Movement.Select(m => m.Outcome.Destination).ToImmutableArray().IndexOf(d0);
        prefix = prefix with { BaselineIndex = baselineIndex };
        // In this open flat profile the in-reach filter contains every reachable tile by the triangle bound.
        // Adding the actor after source D0/D1 selection therefore leaves the scenario mask unchanged.
        var scenario = new ThrowStoneAiScanScenario(origins, origins, d0, d1);
        return new(new(true, null, new(ready, scenario, prefix.Movement, baselineIndex, distance, reach, bound, fallback), evaluated.RngState), prefix);
    }

    internal static TilePosition Select(ImmutableArray<AiScoredMovementTile> cells, Func<TilePosition, int> distance, bool distanceFirst) =>
        (distanceFirst ? cells.OrderBy(c => distance(c.Outcome.Destination)).ThenBy(c => c.OriginKey)
            : cells.OrderBy(c => c.OriginKey).ThenBy(c => distance(c.Outcome.Destination)))
        .ThenByDescending(c => c.Outcome.Destination.Layer).ThenByDescending(c => c.Outcome.Destination.Y)
        .ThenByDescending(c => c.Outcome.Destination.X).First().Outcome.Destination;
}
