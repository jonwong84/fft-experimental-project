using System.Collections.Immutable;
using System.Numerics;

namespace Zodiac.Framework;

/// <summary>Exact sampled direct paths on the bounded living flat roster. A supplied free origin is geometry, not movement permission.</summary>
/// <remarks>ZOD-306/307; source 11464943bbdbd2a0af022d90cb7f10455cf911df. No RNG, effects, file I/O or PS1 runtime dependency.</remarks>
public static class FlatRosterDirectTrajectory
{
    public static FlatRosterDirectTrajectoryResult EvaluateBetweenUnits(AttackBattleSnapshot state, FlatRosterDirectTrajectoryRequest request,
        FlatRosterBattleProfile profile, FlatRosterProjectileHeights heights) => Evaluate(state, request, profile, heights, false);
    public static FlatRosterDirectTrajectoryResult EvaluateToTarget(AttackBattleSnapshot state, FlatRosterDirectTrajectoryRequest request,
        FlatRosterBattleProfile profile, FlatRosterProjectileHeights heights) => Evaluate(state, request, profile, heights, true);

    private static FlatRosterDirectTrajectoryResult Evaluate(AttackBattleSnapshot state, FlatRosterDirectTrajectoryRequest request,
        FlatRosterBattleProfile profile, FlatRosterProjectileHeights heights, bool includeTarget)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(heights);
        FlatRosterDirectTrajectoryResult Reject(FlatRosterDirectTrajectoryRejection reason) => new(false, reason, null);
        if (!state.Combatants.Any(u => u.UnitId == request.ActorId)) return Reject(FlatRosterDirectTrajectoryRejection.UnknownActor);
        if (!state.Combatants.Any(u => u.UnitId == request.TargetId)) return Reject(FlatRosterDirectTrajectoryRejection.UnknownTarget);
        if (request.ActorId == request.TargetId) return Reject(FlatRosterDirectTrajectoryRejection.InvalidTarget);
        try { profile.ValidateState(state); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException) { return Reject(FlatRosterDirectTrajectoryRejection.UnsupportedProfile); }
        if (state.Combatants.Any(u => u.Hp == 0)) return Reject(FlatRosterDirectTrajectoryRejection.UnsupportedProfile);
        if (heights.Units.Count != profile.Units.Count || !heights.Units.Keys.ToHashSet().SetEquals(profile.Units.Keys))
            return Reject(FlatRosterDirectTrajectoryRejection.MissingSpriteHeights);
        var bodies = state.TurnBattle.Battle.Units.ToDictionary(u => u.Id); var target = bodies[request.TargetId].Position;
        if (!state.TurnBattle.Battle.Map.TryGetTile(request.Origin, out _) ||
            bodies.Values.Any(u => u.Id != request.ActorId && u.Position == request.Origin)) return Reject(FlatRosterDirectTrajectoryRejection.InvalidOrigin);
        int range = Math.Abs(request.Origin.X - target.X) + Math.Abs(request.Origin.Y - target.Y);
        if (range is < 1 or > 4) return Reject(FlatRosterDirectTrajectoryRejection.OutOfRange);
        var origin = new FlatProjectilePoint(request.Origin.X * 28 + 14, -120 - heights.Units[request.ActorId] * 2 / 3, request.Origin.Y * 28 + 14);
        int targetRaise = heights.Units[request.TargetId] * 2 / 3;
        if (targetRaise == 0) targetRaise = 12;
        int dx = target.X * 28 + 14 - origin.X, dy = -120 - targetRaise - origin.Y, dz = target.Y * 28 + 14 - origin.Z;
        int distance = SquareRoot12((dx * dx + dy * dy + dz * dz) * 4096), samples = distance / 8192;
        for (int i = 1; i <= samples; i++)
        {
            int fraction = ((i * 8192) << 8) / (distance >> 4);
            var point = new FlatProjectilePoint(origin.X + ((fraction * dx) >> 12), origin.Y + ((fraction * dy) >> 12), origin.Z + ((fraction * dz) >> 12));
            if (point.Y is >= -120 and < 0) return Success(new(false, null, i, samples, distance, point));
            foreach (var unit in profile.Members)
            {
                if (unit.UnitId == request.ActorId || (!includeTarget && unit.UnitId == request.TargetId)) continue;
                int height = heights.Units[unit.UnitId]; var tile = bodies[unit.UnitId].Position;
                if (height != 0 && -120 - height < point.Y && point.Y < -120 &&
                    tile.X * 28 + 6 < point.X && point.X < tile.X * 28 + 22 &&
                    tile.Y * 28 + 6 < point.Z && point.Z < tile.Y * 28 + 22)
                    return Success(new(false, unit.UnitId, i, samples, distance, point));
            }
        }
        return Success(new(true, null, null, samples, distance, null));
        static FlatRosterDirectTrajectoryResult Success(FlatRosterDirectTrajectoryOutcome outcome) => new(true, FlatRosterDirectTrajectoryRejection.None, outcome);
    }

    // Positive signed32 input in the range4/byte-height domain. Every normalized factor is independently verified offline.
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
