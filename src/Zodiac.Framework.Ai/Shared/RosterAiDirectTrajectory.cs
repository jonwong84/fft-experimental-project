using System.Collections.Immutable;
using System.Numerics;

namespace Zodiac.Framework.Ai;

public sealed record RosterAiSpriteHeight(BattleUnitId UnitId, byte Height);

/// <summary>Actual sprite-sheet graphic heights for every initially living ordinary unit; never inferred from a job or team.</summary>
public sealed class RosterAiProjectileHeights
{
    public ImmutableDictionary<BattleUnitId, byte> Units { get; }
    public RosterAiProjectileHeights(IEnumerable<RosterAiSpriteHeight> units)
    {
        ArgumentNullException.ThrowIfNull(units); var copy = ImmutableDictionary.CreateBuilder<BattleUnitId, byte>();
        foreach (var unit in units)
        {
            if (unit is null || string.IsNullOrWhiteSpace(unit.UnitId.Value) || !copy.TryAdd(unit.UnitId, unit.Height))
                throw new ArgumentException("Specify distinct units with actual byte sprite heights.", nameof(units));
        }
        Units = copy.ToImmutable();
    }
}

public sealed record RosterAiDirectTrajectoryRequest(BattleUnitId ActorId, BattleUnitId TargetId, TilePosition Origin);
public enum RosterAiDirectTrajectoryRejection { None, UnknownActor, UnknownTarget, InvalidTarget, UnsupportedProfile,
    MissingSpriteHeights, InvalidOrigin, OutOfRange }
public readonly record struct AiProjectilePoint(int X, int Y, int Z);
public sealed record RosterAiDirectTrajectoryOutcome(bool Clear, BattleUnitId? BlockingUnitId, int? BlockingSample,
    int TotalSamples, int DistanceQ12, AiProjectilePoint? BlockingPoint);
public sealed record RosterAiDirectTrajectoryResult(bool Accepted, RosterAiDirectTrajectoryRejection Rejection,
    RosterAiDirectTrajectoryOutcome? Outcome);

/// <summary>Pure range1..4 direct projectile sampling against every initial living occupant on the existing flat ground domain.</summary>
/// <remarks>ZOD-299/300; source 11464943bbdbd2a0af022d90cb7f10455cf911df. No RNG, effects, execution authority or general terrain/corpse model.</remarks>
public static class RosterAiDirectTrajectory
{
    public static RosterAiDirectTrajectoryResult Evaluate(AttackBattleSnapshot state, RosterAiDirectTrajectoryRequest request,
        RosterAiSimulationProfile simulation, RosterAiProjectileHeights heights)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(simulation); ArgumentNullException.ThrowIfNull(heights);
        RosterAiDirectTrajectoryResult Reject(RosterAiDirectTrajectoryRejection reason) => new(false, reason, null);
        if (!state.Combatants.Any(u => u.UnitId == request.ActorId)) return Reject(RosterAiDirectTrajectoryRejection.UnknownActor);
        if (!state.Combatants.Any(u => u.UnitId == request.TargetId)) return Reject(RosterAiDirectTrajectoryRejection.UnknownTarget);
        if (request.ActorId == request.TargetId) return Reject(RosterAiDirectTrajectoryRejection.InvalidTarget);
        if (simulation.Validate(state, request.ActorId) != RosterAiRejection.None) return Reject(RosterAiDirectTrajectoryRejection.UnsupportedProfile);
        if (!HasCompleteHeights(simulation, heights)) return Reject(RosterAiDirectTrajectoryRejection.MissingSpriteHeights);
        var movement = FlatRosterMovement.Evaluate(state, request.ActorId, simulation.Battle);
        if (!movement.Accepted) return Reject(RosterAiDirectTrajectoryRejection.UnsupportedProfile);
        if (!movement.Outcome!.AiOrigins.Contains(request.Origin)) return Reject(RosterAiDirectTrajectoryRejection.InvalidOrigin);
        var target = state.TurnBattle.Battle.Units.Single(u => u.Id == request.TargetId).Position;
        if (AiRosterNearest.Distance(request.Origin, target) is < 1 or > 4) return Reject(RosterAiDirectTrajectoryRejection.OutOfRange);
        return new(true, RosterAiDirectTrajectoryRejection.None, EvaluateAdmitted(state, request, simulation, heights));
    }

    internal static bool HasCompleteHeights(RosterAiSimulationProfile simulation, RosterAiProjectileHeights heights) =>
        heights.Units.Count == simulation.Battle.Units.Count && heights.Units.Keys.ToHashSet().SetEquals(simulation.Battle.Units.Keys);

    // Only a fresh owning scan can reuse its admitted initial roster/legal origin; no public cached geometry input.
    internal static RosterAiDirectTrajectoryOutcome EvaluateAdmitted(AttackBattleSnapshot state, RosterAiDirectTrajectoryRequest request,
        RosterAiSimulationProfile simulation, RosterAiProjectileHeights heights)
    {
        var bodies = state.TurnBattle.Battle.Units.ToDictionary(u => u.Id);
        var target = bodies[request.TargetId].Position;
        var origin = new AiProjectilePoint(request.Origin.X * 28 + 14, -120 - heights.Units[request.ActorId] * 2 / 3, request.Origin.Y * 28 + 14);
        int targetRaise = heights.Units[request.TargetId] * 2 / 3;
        if (targetRaise == 0) targetRaise = 12;
        int dx = target.X * 28 + 14 - origin.X, dy = -120 - targetRaise - origin.Y, dz = target.Y * 28 + 14 - origin.Z;
        int distance = SquareRoot12((dx * dx + dy * dy + dz * dz) * 4096), samples = distance / 8192;
        for (int i = 1; i <= samples; i++)
        {
            int fraction = ((i * 8192) << 8) / (distance >> 4);
            var point = new AiProjectilePoint(origin.X + ((fraction * dx) >> 12), origin.Y + ((fraction * dy) >> 12), origin.Z + ((fraction * dz) >> 12));
            // Lower-layer terrain precedes units. The absent upper layer cannot intersect this raised flat domain.
            if (point.Y is >= -120 and < 0) return new(false, null, i, samples, distance, point);
            foreach (var unit in simulation.Battle.Members)
            {
                if (unit.UnitId == request.ActorId || unit.UnitId == request.TargetId) continue;
                int height = heights.Units[unit.UnitId]; var tile = bodies[unit.UnitId].Position;
                if (height != 0 && -120 - height < point.Y && point.Y < -120 &&
                    tile.X * 28 + 6 < point.X && point.X < tile.X * 28 + 22 &&
                    tile.Y * 28 + 6 < point.Z && point.Z < tile.Y * 28 + 22)
                    return new(false, unit.UnitId, i, samples, distance, point);
            }
        }
        return new(true, null, null, samples, distance, null);
    }

    // Positive signed32 domain only. Offline evidence proves every normalized SDK factor equals this integer root.
    private static int SquareRoot12(int value)
    {
        int leading = BitOperations.LeadingZeroCount((uint)value) & ~1;
        int normalized = leading >= 24 ? value << (leading - 24) : value >> (24 - leading);
        int squaredFactor = normalized << 18, low = 0, high = 8192;
        while (low + 1 < high)
        {
            int middle = (low + high) / 2;
            if (middle * middle <= squaredFactor) low = middle; else high = middle;
        }
        int exponent = (19 - leading) >> 1;
        return exponent >= 0 ? low << exponent : low >> -exponent;
    }
}
