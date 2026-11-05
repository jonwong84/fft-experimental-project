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
        var partition = AiScenarioLayout.Partition(common.ReachableOrigins, common.Origin, common.AllyAbilityReach, reach, Distance);
        var scored = partition.ScoreMask;
        var evaluated = AiMovementPrefix.Evaluate(state, request.ActorId, request.TargetId, scored, null, units, rng,
            proximityOrigins: common.ReachableOrigins);
        if (evaluated.Rejection != AiSimulationClockRejection.None)
            return Reject(new(AttackThrowStoneAiScenarioStage.Movement, AttackThrowStoneAiScenarioRejection.MovementRejected,
                MovementIndex: evaluated.FailedIndex, MovementReason: evaluated.Rejection == AiSimulationClockRejection.RandomDrawCountExhausted
                    ? SwordAiMovementBaselineRejection.RandomDrawCountExhausted : SwordAiMovementBaselineRejection.TickCountExhausted));
        var prefix = evaluated.Outcome!; rng = evaluated.RngState;
        AiScenarioLayoutOutcome layout;
        try
        {
            layout = AiScenarioLayout.Finish(partition, common.ReachableOrigins, common.Origin, Distance,
                prefix.Movement.Select(m => new AiScenarioCell(m.Outcome.Destination, m.TilePriority, m.Proximity)).ToImmutableArray(),
                prefix.Proximity.Destinations, team == SwordAiScenarioTeam.NonBlue, common.ActorAttackUsable,
                hasRangedAbility && common.Refresh.RefreshedUnits.Single(u => u.UnitId == request.TargetId).Enemy, rng);
        }
        catch (OverflowException)
        { return Reject(new(AttackThrowStoneAiScenarioStage.ReachAdjustment, AttackThrowStoneAiScenarioRejection.RandomDrawCountExhausted)); }
        prefix = prefix with { BaselineIndex = layout.BaselineIndex };
        return new(new(scored, layout.Scenario1, layout.Scenario2, prefix.Movement, layout.Cells, layout.D0, layout.D1, layout.BaselineIndex,
            distance, reach, layout.EffectiveReach, layout.Adjustment, layout.BaselineLimit, partition.FilterFallback,
            layout.DestinationFallback, outOfReach), null, prefix, layout.RngState);
    }

    internal static TilePosition Select(ImmutableArray<AiScoredMovementTile> cells, Func<TilePosition, int> distance, bool distanceFirst) =>
        AiScenarioLayout.Select(cells.Select(c => new AiScenarioCell(c.Outcome.Destination, c.TilePriority, c.Proximity)).ToImmutableArray(), distance, distanceFirst);
}
