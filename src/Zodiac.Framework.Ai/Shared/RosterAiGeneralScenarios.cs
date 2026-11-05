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
    int TargetReach, int EffectiveReach, SwordAiReachAdjustment? ReachAdjustment, int? BaselineDistanceLimit,
    bool FilterUsedReachableFallback, bool DestinationUsedDistanceFallback, bool OutOfReach);
public sealed record RosterAiGeneralScenarioResult(bool Accepted, RosterAiGeneralFailure? Failure,
    RosterAiGeneralScenarioOutcome? Outcome, RandomState RngState);

/// <summary>Own one fresh general prefix and private roster movement scan. No ability search, rank/lottery, plan or command.</summary>
/// <remarks>ZOD-287/289; source 11464943bbdbd2a0af022d90cb7f10455cf911df. Normal focused scenarios; no reflected/stay-near/crystal overrides.</remarks>
public static class RosterAiGeneralScenarios
{
    public static RosterAiGeneralScenarioResult Evaluate(AttackBattleSnapshot state, RosterAiRefreshRequest request,
        RosterAiSimulationProfile simulation, RosterAiRefreshProfile refresh, AiHumanAbilityLists lists,
        RosterAiGeneralProfile general, RosterAiMovementProfile scoring, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(scoring);
        RosterAiGeneralScenarioResult Reject(RosterAiGeneralFailure failure) => new(false, failure, null, rng);
        if (!scoring.OriginalSourceScoreCell || !scoring.CanonicalCoordinates || !scoring.NormalScoring ||
            !scoring.OrdinaryHumansUnchangedEquipment || !scoring.ZeroCurrentAndBaselineGolem)
            return Reject(new(RosterAiGeneralRejection.UnsupportedContext));
        var ready = RosterAiGeneralPreparation.Evaluate(state, request, simulation, refresh, lists, general, rng);
        if (!ready.Accepted) return Reject(ready.Failure!);
        var p = ready.Outcome!; var actor = p.Preparation.Targets.ById[request.ActorId];
        int Distance(TilePosition tile) => p.Geometry.TargetDistanceRows.Single(row => row.Y == tile.Y).Distances[tile.X];
        var partition = AiScenarioLayout.Partition(p.Geometry.AiOrigins, actor.Position, p.Geometry.EffectiveMove, p.FocusReach, Distance);
        var movement = ImmutableArray.CreateBuilder<RosterAiScoredMovementTile>(); var current = ready.RngState;
        int team = simulation.Battle.Units[request.ActorId].StableTeam;
        var enemies = simulation.Battle.Members.Where(u => u.UnitId != request.ActorId && u.StableTeam != team).Select(u => u.UnitId).ToImmutableHashSet();
        foreach (var tile in partition.ScoreMask)
        {
            // Admission and legal mask are owned above; do not re-run public movement/preparation per tile.
            var result = AiEffectFreeProjection.Evaluate(state, request.ActorId, tile, simulation.Units, current);
            if (result.Rejection != AiSimulationClockRejection.None)
                return Reject(new(result.Rejection == AiSimulationClockRejection.RandomDrawCountExhausted
                    ? RosterAiGeneralRejection.RandomDrawCountExhausted : RosterAiGeneralRejection.TickCountExhausted,
                    MovementIndex: movement.Count));
            var clock = result.Clock!;
            var outcome = new RosterAiMovementOutcome(request.ActorId, tile, result.TurnEndCt, clock.Units,
                clock.MoveOpportunityUnitId, clock.ElapsedTicks, clock.Ticks, clock.UpkeepPicks,
                AiCandidateScoring.ScoreFreshUnits(clock.Units, enemies, 100));
            movement.Add(new(outcome, p.Proximity.WrittenValues[tile], clock.RngState)); current = clock.RngState;
        }
        var scored = movement.ToImmutable(); AiScenarioLayoutOutcome layout;
        try
        {
            layout = AiScenarioLayout.Finish(partition, p.Geometry.AiOrigins, actor.Position, Distance,
                scored.Select(m => new AiScenarioCell(m.Outcome.Destination, m.TilePriority, m.Proximity)).ToImmutableArray(),
                p.Proximity.WrittenValues, team != 0,
                !state.TurnBattle.Turns.Units.Single(u => u.Id == request.ActorId).ActionTaken,
                lists.Units[request.ActorId].HasRangedAbility && p.Preparation.Targets.ById[p.Focus.TargetId].Decision.Enemy, current);
        }
        catch (OverflowException) { return Reject(new(RosterAiGeneralRejection.RandomDrawCountExhausted)); }
        return new(true, null, new(p, partition.ScoreMask, layout.Scenario1, layout.Scenario2, scored, layout.Cells,
            layout.D0, layout.D1, layout.BaselineIndex, partition.Distance, partition.Reach, layout.EffectiveReach,
            layout.Adjustment, layout.BaselineLimit, partition.FilterFallback, layout.DestinationFallback, partition.OutOfReach), layout.RngState);
    }
}
