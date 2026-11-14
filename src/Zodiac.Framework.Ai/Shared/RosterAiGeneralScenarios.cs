using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record RosterAiScoredMovementTile(RosterAiMovementOutcome Outcome, byte Proximity, RandomState RngState)
{
    public short TilePriority => unchecked((short)-Outcome.Score.Priority);
    public int OriginKey => TilePriority * 65536 + Proximity;
}
public sealed record RosterAiGeneralScenarioOutcome(RosterAiGeneralPreparationOutcome Preparation,
    ImmutableArray<TilePosition> ScoreMask, ImmutableArray<TilePosition> Scenario1, ImmutableArray<TilePosition> Scenario2,
    ImmutableArray<RosterAiScoredMovementTile> Movement, ImmutableArray<AiMovementCellProvenance> Cells,
    TilePosition BaselineDestination, TilePosition ActFirstDestination, int BaselineIndex, int TargetDistance,
    int TargetReach, int EffectiveReach, AiReachAdjustment? ReachAdjustment, int? BaselineDistanceLimit,
    bool FilterUsedReachableFallback, bool DestinationUsedDistanceFallback, bool OutOfReach);
public sealed record RosterAiGeneralScenarioResult(bool Accepted, RosterAiGeneralFailure? Failure,
    RosterAiGeneralScenarioOutcome? Outcome, RandomState RngState);

/// <summary>Own one fresh general prefix and private roster movement scan. No ability search, rank/lottery, plan or command.</summary>
/// <remarks>ZOD-287/289; source 11464943bbdbd2a0af022d90cb7f10455cf911df. Normal focused scenarios; no reflected/stay-near/crystal overrides.</remarks>
internal static class RosterAiGeneralScenarios
{
    public static RosterAiGeneralScenarioResult Evaluate(AttackBattleSnapshot state, RosterAiRefreshRequest request,
        RosterAiSimulationProfile simulation, RosterAiRefreshProfile refresh, AiHumanAbilityLists lists,
        RosterAiGeneralProfile general, RosterAiMovementProfile scoring, RandomState rng)
    {
        var ready = Prepare(state, request, simulation, refresh, lists, general, scoring, rng);
        if (!ready.Accepted)
        {
            return new(false, ready.Failure, null, rng);
        }
        var built = ScenariosBuild(state, request, simulation, lists, ready.Outcome!, ready.RngState);
        // This fresh-query adapter owns atomic rejection across both preparation and scenarios.
        return built.Accepted ? built : built with { RngState = rng };
    }

    /// <summary>Fresh admission/roster preparation before action_select enters the admitted general branch.</summary>
    internal static RosterAiGeneralPreparationResult Prepare(AttackBattleSnapshot state, RosterAiRefreshRequest request,
        RosterAiSimulationProfile simulation, RosterAiRefreshProfile refresh, AiHumanAbilityLists lists,
        RosterAiGeneralProfile general, RosterAiMovementProfile scoring, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(scoring);
        if (!scoring.OriginalSourceScoreCell || !scoring.CanonicalCoordinates || !scoring.NormalScoring ||
            !scoring.OrdinaryHumansUnchangedEquipment || !scoring.ZeroCurrentAndBaselineGolem)
        {
            return new(false, new(RosterAiGeneralRejection.UnsupportedContext), null, rng);
        }
        return RosterAiGeneralPreparation.Evaluate(state, request, simulation, refresh, lists, general, rng);
    }

    /// <summary>scenarios_build: partition movement, score reachable tiles and finish destination/scenario selection.</summary>
    internal static RosterAiGeneralScenarioResult ScenariosBuild(AttackBattleSnapshot state, RosterAiRefreshRequest request,
        RosterAiSimulationProfile simulation, AiHumanAbilityLists lists, RosterAiGeneralPreparationOutcome p, RandomState rng)
    {
        RosterAiGeneralScenarioResult Reject(RosterAiGeneralFailure failure) => new(false, failure, null, rng);
        var actor = p.Preparation.Targets.ById[request.ActorId];
        int Distance(TilePosition tile) => p.Geometry.TargetDistanceRows.Single(row => row.Y == tile.Y).Distances[tile.X];
        var partition = AiScenarioLayout.Partition(p.Geometry.AiOrigins, actor.Position, p.Geometry.EffectiveMove, p.FocusReach, Distance);
        var movement = ReachableTilesScore(state, request.ActorId, simulation, p, partition.ScoreMask, rng);
        if (movement.Failure is { } failure)
        {
            return Reject(failure);
        }
        var scored = movement.Tiles;
        AiScenarioLayoutOutcome layout;
        int team = simulation.Battle.Units[request.ActorId].StableTeam;
        try
        {
            layout = AiScenarioLayout.Finish(partition, p.Geometry.AiOrigins, actor.Position, Distance,
                scored.Select(m => new AiScenarioCell(m.Outcome.Destination, m.TilePriority, m.Proximity)).ToImmutableArray(),
                p.Proximity.WrittenValues, team != 0,
                !state.TurnBattle.Turns.Units.Single(u => u.Id == request.ActorId).ActionTaken,
                lists.Units[request.ActorId].HasRangedAbility && p.Preparation.Targets.ById[p.Focus.TargetId].Decision.Enemy, movement.RngState);
        }
        catch (OverflowException) { return Reject(new(RosterAiGeneralRejection.RandomDrawCountExhausted)); }
        return new(true, null, new(p, partition.ScoreMask, layout.Scenario1, layout.Scenario2, scored, layout.Cells,
            layout.D0, layout.D1, layout.BaselineIndex, partition.Distance, partition.Reach, layout.EffectiveReach,
            layout.Adjustment, layout.BaselineLimit, partition.FilterFallback, layout.DestinationFallback, partition.OutOfReach), layout.RngState);
    }

    /// <summary>reachable_tiles_score: simulate each admitted move-only candidate in mask order with chained RNG.</summary>
    private static (ImmutableArray<RosterAiScoredMovementTile> Tiles, RosterAiGeneralFailure? Failure, RandomState RngState)
        ReachableTilesScore(AttackBattleSnapshot state, BattleUnitId actorId, RosterAiSimulationProfile simulation,
            RosterAiGeneralPreparationOutcome preparation, ImmutableArray<TilePosition> scoreMask, RandomState rng)
    {
        var movement = ImmutableArray.CreateBuilder<RosterAiScoredMovementTile>();
        var current = rng;
        int team = simulation.Battle.Units[actorId].StableTeam;
        var enemies = simulation.Battle.Members.Where(u => u.UnitId != actorId && u.StableTeam != team).Select(u => u.UnitId).ToImmutableHashSet();
        foreach (var tile in scoreMask)
        {
            // Admission and legal mask are owned above; do not re-run public movement/preparation per tile.
            var result = AiEffectFreeProjection.Evaluate(state, actorId, tile, simulation.Units, current);
            if (result.Rejection != AiSimulationClockRejection.None)
                return ([], new(result.Rejection == AiSimulationClockRejection.RandomDrawCountExhausted
                    ? RosterAiGeneralRejection.RandomDrawCountExhausted : RosterAiGeneralRejection.TickCountExhausted,
                    MovementIndex: movement.Count), rng);
            var clock = result.Clock!;
            var outcome = new RosterAiMovementOutcome(actorId, tile, result.TurnEndCt, clock.Units,
                clock.MoveOpportunityUnitId, clock.ElapsedTicks, clock.Ticks, clock.UpkeepPicks,
                AiCandidateScoring.ScoreFreshUnits(clock.Units, enemies, 100));
            movement.Add(new(outcome, preparation.Proximity.WrittenValues[tile], clock.RngState)); current = clock.RngState;
        }
        return (movement.ToImmutable(), null, current);
    }
}
