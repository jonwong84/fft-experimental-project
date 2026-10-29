using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Fresh hypothetical movement score and source origin key; never accepted as trusted query input.</summary>
public sealed record AiScoredMovementTile(AiMovementBaselineOutcome Outcome, byte Proximity, FftRandomState RngState)
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
        static int Delay(TurnUnitState unit) => (Math.Max(0, 100 - unit.Ct) + unit.Speed - 1) / unit.Speed;
        var turns = state.TurnBattle.Turns.Units;
        byte actorDelay = (byte)Delay(turns.Single(u => u.Id == actorId));
        int targetDelay = Delay(turns.Single(u => u.Id == targetId));
        bool eligible = targetDelay <= actorDelay;
        state.TurnBattle.Battle.TryGetUnit(targetId, out var target);
        var position = target!.Position;
        var proximity = destinations.Distinct().ToImmutableDictionary(p => p, p => eligible
            ? (byte)Math.Max(0, 10 - Math.Abs(position.X - p.X) - Math.Abs(position.Y - p.Y)) : (byte)0);
        return new(actorDelay, targetDelay, eligible ? targetId : null, proximity);
    }
}
internal sealed record AiMovementPrefixOutcome(ImmutableArray<AiScoredMovementTile> Movement,
    int BaselineIndex, AiProximityCache Proximity);
internal sealed record AiMovementPrefixResult(AiSimulationClockRejection Rejection, int? FailedIndex,
    AiMovementPrefixOutcome? Outcome, FftRandomState RngState);

/// <summary>Shared prefix after family admission; source map order and one original proximity cache.</summary>
internal static class AiMovementPrefix
{
    internal static AiMovementPrefixResult Evaluate(AttackBattleSnapshot state, BattleUnitId actorId, BattleUnitId targetId,
        IEnumerable<TilePosition> origins, TilePosition baselineDestination, ImmutableArray<AiUnitBaseline> units, FftRandomState decisionRng)
    {
        var ordered = origins.OrderBy(p => p.Layer).ThenBy(p => p.Y).ThenBy(p => p.X).ToArray();
        var proximity = AiProximityCache.Create(state, actorId, targetId, ordered);
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
