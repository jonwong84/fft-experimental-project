using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Fresh proximity with optional current-decision priority write. Missing priority never implies zero.</summary>
public sealed record AiMovementCellProvenance(TilePosition Tile, byte Proximity, short? WrittenPriority)
{
    public bool PriorityWritten => WrittenPriority.HasValue;
}

/// <summary>Closed ordinary movement diagnostics; original masks may overlap after actor insertion.</summary>
public sealed record AiDerivedMovementScenario(ImmutableArray<TilePosition> ScoreMask,
    ImmutableArray<TilePosition> Scenario1, ImmutableArray<TilePosition> Scenario2,
    ImmutableArray<AiScoredMovementTile> Movement, ImmutableArray<AiMovementCellProvenance> Cells,
    TilePosition BaselineDestination, TilePosition ActFirstDestination, int BaselineIndex,
    int TargetDistance, int TargetReach, int EffectiveReach, SwordAiReachAdjustment? ReachAdjustment,
    int? BaselineDistanceLimit, bool FilterUsedReachableFallback, bool DestinationUsedDistanceFallback, bool OutOfReach);
internal sealed record AiMovementScenarioCore(AiDerivedMovementScenario? Outcome,
    AttackThrowStoneAiScenarioFailure? Failure, AiMovementPrefixOutcome? Prefix, RandomState RngState)
{
    internal bool Accepted => Failure is null;
}

/// <summary>Shared source kernel after family-owned admission/preparation; no list assertion or public cached-input boundary.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-197/200/249/250.
/// Requires ordinary minimum enemy range1 and derived ally reach. Legacy pair failure names carry reasons only.</remarks>
internal static class AiMovementScenarios
{
    internal static AiMovementScenarioCore Evaluate(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile attack, ImmutableArray<AiUnitBaseline> units, SwordAiPreparationOutcome common,
        SwordAiScenarioTeam team, bool hasRangedAbility, RandomState rng, RandomState queryRng, bool? outside)
    {
        AiMovementScenarioCore Reject(AttackThrowStoneAiScenarioFailure failure) => new(null, failure, null, queryRng);
        var geometry = FlatOrdinaryMovement.Evaluate(state, request.ActorId, attack.Movement).Outcome!;
        int Distance(TilePosition tile) => geometry.TargetDistanceRows.Single(r => r.Y == tile.Y).Distances[tile.X];
        int distance = Distance(common.Origin), reach = common.EnemyAbilityReach;
        bool outOfReach = outside ?? distance > reach;
        if (!outOfReach && distance > reach)
            return Reject(new(AttackThrowStoneAiScenarioStage.Branch, AttackThrowStoneAiScenarioRejection.OutOfReach));
        if (outOfReach && distance <= reach)
            return Reject(new(AttackThrowStoneAiScenarioStage.Branch, AttackThrowStoneAiScenarioRejection.InReach));
        int limit = outOfReach ? distance - 1 : common.AllyAbilityReach + distance;
        var scored = common.ReachableOrigins.Where(p => Distance(p) <= limit).ToImmutableArray();
        bool filterFallback = scored.IsEmpty;
        if (filterFallback) scored = common.ReachableOrigins;
        var evaluated = AiMovementPrefix.Evaluate(state, request.ActorId, request.TargetId, scored, null, units, rng,
            proximityOrigins: common.ReachableOrigins);
        if (evaluated.Rejection != AiSimulationClockRejection.None)
            return Reject(new(AttackThrowStoneAiScenarioStage.Movement, AttackThrowStoneAiScenarioRejection.MovementRejected,
                MovementIndex: evaluated.FailedIndex, MovementReason: evaluated.Rejection == AiSimulationClockRejection.RandomDrawCountExhausted
                    ? SwordAiMovementBaselineRejection.RandomDrawCountExhausted : SwordAiMovementBaselineRejection.TickCountExhausted));
        var prefix = evaluated.Outcome!; rng = evaluated.RngState;
        int effective = reach; SwordAiReachAdjustment? adjustment = null;
        if (outOfReach && team == SwordAiScenarioTeam.NonBlue)
        {
            uint raw;
            try { var draw = new RandomStateGenerator().Next(rng); raw = draw.Value; rng = draw.State; }
            catch (OverflowException)
            { return Reject(new(AttackThrowStoneAiScenarioStage.ReachAdjustment, AttackThrowStoneAiScenarioRejection.RandomDrawCountExhausted)); }
            int modulo = (int)(raw % 7); effective = modulo == 0 ? reach / 2 : reach;
            adjustment = new(raw, modulo, reach, effective);
        }
        int? bound = null; bool destinationFallback = false; TilePosition d0, d1;
        if (outOfReach)
        {
            var within = prefix.Movement.Where(m => Distance(m.Outcome.Destination) <= effective).ToImmutableArray();
            destinationFallback = within.IsEmpty;
            d0 = d1 = Select(destinationFallback ? prefix.Movement : within, Distance, destinationFallback);
        }
        else
        {
            d0 = d1 = Select(prefix.Movement, Distance, false);
            if (common.ActorAttackUsable)
            {
                bound = distance - 1;
                if (hasRangedAbility && common.Refresh.RefreshedUnits.Single(u => u.UnitId == request.TargetId).Enemy)
                    bound = Math.Max(3, bound.Value);
                var within = prefix.Movement.Where(m => Distance(m.Outcome.Destination) <= bound.Value).ToImmutableArray();
                destinationFallback = within.IsEmpty;
                d0 = Select(destinationFallback ? prefix.Movement : within, Distance, destinationFallback);
            }
        }
        // Source scenario2 precedes actor OR; priority/proximity provenance covers original M0, not just the score mask.
        var written = prefix.Movement.ToImmutableDictionary(m => m.Outcome.Destination, m => m.TilePriority);
        var scenario2 = common.ReachableOrigins.Where(p => !written.ContainsKey(p)).ToImmutableArray();
        var scenario1 = common.ReachableOrigins.Where(p => written.ContainsKey(p) || p == common.Origin).ToImmutableArray();
        var cells = common.ReachableOrigins.Select(p => new AiMovementCellProvenance(p, prefix.Proximity.Destinations[p],
            written.TryGetValue(p, out short priority) ? priority : null)).ToImmutableArray();
        int baselineIndex = prefix.Movement.Select(m => m.Outcome.Destination).ToImmutableArray().IndexOf(d0);
        prefix = prefix with { BaselineIndex = baselineIndex };
        return new(new(scored, scenario1, scenario2, prefix.Movement, cells, d0, d1, baselineIndex,
            distance, reach, effective, adjustment, bound, filterFallback, destinationFallback, outOfReach), null, prefix, rng);
    }

    internal static TilePosition Select(ImmutableArray<AiScoredMovementTile> cells, Func<TilePosition, int> distance, bool distanceFirst) =>
        (distanceFirst ? cells.OrderBy(c => distance(c.Outcome.Destination)).ThenBy(c => c.OriginKey)
            : cells.OrderBy(c => c.OriginKey).ThenBy(c => distance(c.Outcome.Destination)))
        .ThenByDescending(c => c.Outcome.Destination.Layer).ThenByDescending(c => c.Outcome.Destination.Y)
        .ThenByDescending(c => c.Outcome.Destination.X).First().Outcome.Destination;
}
