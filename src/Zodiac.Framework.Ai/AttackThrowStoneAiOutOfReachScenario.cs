using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Fresh proximity for a scenario0 cell. Null priority means no write in this decision, never an inferred zero.</summary>
/// <remarks>Retained source contents may include event/status staging; their value and history are not modeled here.</remarks>
public sealed record AiMovementCellProvenance(TilePosition Tile, byte Proximity, short? WrittenPriority)
{
    public bool PriorityWritten => WrittenPriority.HasValue;
}

/// <summary>Diagnostic masks remain distinct from fresh-cell scan scenarios: final scenarios may overlap at the actor.</summary>
/// <remarks>No prior-memory value, action scan, ranked candidate or plan is produced.</remarks>
public sealed record AttackThrowStoneAiOutOfReachScenarioOutcome(AttackThrowStoneAiPreparationOutcome Preparation,
    ImmutableArray<TilePosition> ScoreMask, ImmutableArray<TilePosition> Scenario1, ImmutableArray<TilePosition> Scenario2,
    ImmutableArray<AiScoredMovementTile> Movement, ImmutableArray<AiMovementCellProvenance> Cells,
    TilePosition BaselineDestination, TilePosition ActFirstDestination, int BaselineIndex,
    int TargetDistance, int TargetReach, int EffectiveReach, SwordAiReachAdjustment ReachAdjustment,
    bool FilterUsedReachableFallback, bool DestinationUsedDistanceFallback);
/// <summary>Failure exposes reasons only and restores incoming whole-query RNG, including after partial private work.</summary>
public sealed record AttackThrowStoneAiOutOfReachScenarioResult(bool Accepted, AttackThrowStoneAiScenarioFailure? Failure,
    AttackThrowStoneAiOutOfReachScenarioOutcome? Outcome, FftRandomState RngState);
internal sealed record AttackThrowStoneAiOutOfReachScenarioCore(AttackThrowStoneAiOutOfReachScenarioResult Result,
    AiMovementPrefixOutcome? Prefix = null);

/// <summary>Own fresh NonBlue pair preparation, full proximity, filtered scores and out-of-reach D0=D1.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-199/200.
/// No unknown priority reads, cached inputs, scans, lottery, command authority or live RNG adoption.</remarks>
public static class AttackThrowStoneAiOutOfReachScenario
{
    public static AttackThrowStoneAiOutOfReachScenarioResult Evaluate(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        AssetId throwStoneAbilityId, BoundedSwordWeapon? weapon, SwordAiCandidateProfile attackProfile,
        ThrowStoneAiCandidateProfile throwStoneProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        AttackThrowStoneAiPreparationProfile preparationProfile, AttackThrowStoneAiScenarioProfile scenarioProfile,
        SwordAiScenarioTeam team, FftRandomState decisionRng) =>
        EvaluateFresh(state, request, throwStoneAbilityId, weapon, attackProfile, throwStoneProfile, scoreProfile, refreshProfile,
            preparationProfile, scenarioProfile, team, decisionRng).Result;

    // Only an owning query receives this freshly evaluated prefix; public diagnostics cannot be supplied back.
    internal static AttackThrowStoneAiOutOfReachScenarioCore EvaluateFresh(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        AssetId throwStoneAbilityId, BoundedSwordWeapon? weapon, SwordAiCandidateProfile attackProfile,
        ThrowStoneAiCandidateProfile throwStoneProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        AttackThrowStoneAiPreparationProfile preparationProfile, AttackThrowStoneAiScenarioProfile scenarioProfile,
        SwordAiScenarioTeam team, FftRandomState decisionRng)
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
        if (!preparation.Accepted)
            return Reject(new(AttackThrowStoneAiScenarioStage.Preparation, AttackThrowStoneAiScenarioRejection.PreparationRejected, preparation.Failure));
        var ready = preparation.Outcome!; var common = ready.Common;
        var geometry = FlatOrdinaryMovement.Evaluate(state, request.ActorId, attackProfile.Movement).Outcome!;
        int Distance(TilePosition tile) => geometry.TargetDistanceRows.Single(r => r.Y == tile.Y).Distances[tile.X];
        int distance = Distance(common.Origin), reach = common.EnemyAbilityReach;
        if (distance <= reach) return Reject(new(AttackThrowStoneAiScenarioStage.Branch, AttackThrowStoneAiScenarioRejection.InReach));
        var scored = common.ReachableOrigins.Where(p => Distance(p) <= distance - 1).ToImmutableArray();
        bool filterFallback = scored.IsEmpty;
        if (filterFallback) scored = common.ReachableOrigins;
        var evaluated = AiMovementPrefix.Evaluate(state, request.ActorId, request.TargetId, scored, null, throwStoneProfile.Units,
            preparation.RngState, proximityOrigins: common.ReachableOrigins);
        if (evaluated.Rejection != AiSimulationClockRejection.None)
            return Reject(new(AttackThrowStoneAiScenarioStage.Movement, AttackThrowStoneAiScenarioRejection.MovementRejected,
                MovementIndex: evaluated.FailedIndex, MovementReason: evaluated.Rejection == AiSimulationClockRejection.RandomDrawCountExhausted
                    ? SwordAiMovementBaselineRejection.RandomDrawCountExhausted : SwordAiMovementBaselineRejection.TickCountExhausted));
        var prefix = evaluated.Outcome!;
        uint raw; FftRandomState rng;
        try { var draw = new FftBiosRandom().Next(evaluated.RngState); raw = draw.Value; rng = draw.State; }
        catch (OverflowException)
        { return Reject(new(AttackThrowStoneAiScenarioStage.ReachAdjustment, AttackThrowStoneAiScenarioRejection.RandomDrawCountExhausted)); }
        int modulo = (int)(raw % 7), effective = modulo == 0 ? reach / 2 : reach;
        var within = prefix.Movement.Where(m => Distance(m.Outcome.Destination) <= effective).ToImmutableArray();
        bool destinationFallback = within.IsEmpty;
        var destination = AttackThrowStoneAiMovementScenario.Select(destinationFallback ? prefix.Movement : within,
            Distance, distanceFirst: destinationFallback);
        // Source scenario2 is built before actor insertion; it can overlap final scenario1 at that unwritten origin.
        var written = prefix.Movement.ToImmutableDictionary(m => m.Outcome.Destination, m => m.TilePriority);
        var scenario2 = common.ReachableOrigins.Where(p => !written.ContainsKey(p)).ToImmutableArray();
        var scenario1 = common.ReachableOrigins.Where(p => written.ContainsKey(p) || p == common.Origin).ToImmutableArray();
        var cells = common.ReachableOrigins.Select(p => new AiMovementCellProvenance(p, prefix.Proximity.Destinations[p],
            written.TryGetValue(p, out short priority) ? priority : null)).ToImmutableArray();
        int baselineIndex = scored.IndexOf(destination);
        prefix = prefix with { BaselineIndex = baselineIndex };
        return new(new(true, null, new(ready, scored, scenario1, scenario2, prefix.Movement, cells,
            destination, destination, baselineIndex, distance, reach, effective,
            new(raw, modulo, reach, effective), filterFallback, destinationFallback), rng), prefix);
    }
}
