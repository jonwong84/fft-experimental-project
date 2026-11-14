using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Fresh hypothetical movement score and source origin key; never accepted as trusted query input.</summary>
public sealed record AiScoredMovementTile(AiMovementBaselineOutcome Outcome, byte Proximity, RandomState RngState)
{
    public short TilePriority => unchecked((short)-Outcome.Score.Priority);
    public int OriginKey => TilePriority * 65536 + Proximity;
}

internal sealed record AiProximityCache(byte ActorDelay, int TargetDelay, BattleUnitId? EligibleEnemyId,
    ImmutableDictionary<TilePosition, byte> Destinations)
{
    internal static AiProximityCache Create(AttackBattleSnapshot state, BattleUnitId actorId,
        BattleUnitId targetId, IEnumerable<TilePosition> destinations)
    {
        var roster = AiOrdinaryPairRoster.Create(state, actorId, targetId);
        var proximity = AiRosterProximity.Evaluate(roster, destinations);
        return new(roster.CachedActorDelay, roster.ById[targetId].TurnDelay,
            proximity.TargetableEnemies.Contains(targetId) ? targetId : null, proximity.WrittenValues);
    }
}
internal sealed record AiMovementPrefixOutcome(ImmutableArray<AiScoredMovementTile> Movement,
    int BaselineIndex, AiProximityCache Proximity);
internal sealed record AiMovementPrefixResult(AiSimulationClockRejection Rejection, int? FailedIndex,
    AiMovementPrefixOutcome? Outcome, RandomState RngState);

/// <summary>Shared prefix after family admission; source map order and one original proximity cache.</summary>
internal static class AiMovementPrefix
{
    internal static AiMovementPrefixResult Evaluate(AttackBattleSnapshot state, BattleUnitId actorId, BattleUnitId targetId,
        IEnumerable<TilePosition> origins, TilePosition? baselineDestination, ImmutableArray<AiUnitBaseline> units, RandomState decisionRng,
        IEnumerable<TilePosition>? proximityOrigins = null)
    {
        var ordered = origins.OrderBy(p => p.Layer).ThenBy(p => p.Y).ThenBy(p => p.X).ToArray();
        // Source proximity covers scenario0 before scenario1 filtering; only the latter receives priority writes.
        var proximity = AiProximityCache.Create(state, actorId, targetId, proximityOrigins ?? ordered);
        var movement = ImmutableArray.CreateBuilder<AiScoredMovementTile>(ordered.Length);
        int baselineIndex = -1;
        var rng = decisionRng;
        foreach (var destination in ordered)
        {
            var result = AiMovementSimulation.Evaluate(state, actorId, targetId, destination, units, rng);
            if (result.Rejection != AiSimulationClockRejection.None) return new(result.Rejection, movement.Count, null, decisionRng);
            if (destination == baselineDestination) baselineIndex = movement.Count;
            movement.Add(new(result.Outcome!, proximity.Destinations[destination], result.RngState)); rng = result.RngState;
        }
        return new(AiSimulationClockRejection.None, null, new(movement.ToImmutable(), baselineIndex, proximity), rng);
    }
}
