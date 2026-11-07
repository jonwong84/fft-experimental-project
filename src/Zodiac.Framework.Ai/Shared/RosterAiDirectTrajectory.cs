using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public sealed record RosterAiSpriteHeight(BattleUnitId UnitId, byte Height);

/// <summary>Actual sprite-sheet graphic heights for every initially living ordinary unit; never inferred from a job or team.</summary>
public sealed class RosterAiProjectileHeights
{
    public ImmutableDictionary<BattleUnitId, byte> Units { get; }
    internal FlatRosterProjectileHeights Shared { get; }
    public RosterAiProjectileHeights(IEnumerable<RosterAiSpriteHeight> units)
    {
        ArgumentNullException.ThrowIfNull(units); var copy = ImmutableDictionary.CreateBuilder<BattleUnitId, byte>();
        foreach (var unit in units)
        {
            if (unit is null || string.IsNullOrWhiteSpace(unit.UnitId.Value) || !copy.TryAdd(unit.UnitId, unit.Height))
                throw new ArgumentException("Specify distinct units with actual byte sprite heights.", nameof(units));
        }
        Units = copy.ToImmutable();
        Shared = new(Units.Select(u => new FlatRosterSpriteHeight(u.Key, u.Value)));
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
internal static class RosterAiDirectTrajectory
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
        var result = FlatRosterDirectTrajectory.EvaluateBetweenUnits(state, new(request.ActorId, request.TargetId, request.Origin), simulation.Battle, heights.Shared);
        if (!result.Accepted) throw new InvalidOperationException("Owned AI geometry failed shared flat roster admission.");
        var o = result.Outcome!;
        return new(o.Clear, o.BlockingUnitId, o.BlockingSample, o.TotalSamples, o.DistanceQ12,
            o.BlockingPoint is { } point ? new AiProjectilePoint(point.X, point.Y, point.Z) : null);
    }
}
