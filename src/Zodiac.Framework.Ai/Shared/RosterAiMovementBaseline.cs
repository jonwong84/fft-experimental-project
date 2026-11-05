using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record RosterAiMovementRequest(BattleUnitId ActorId, TilePosition Destination);
public sealed record RosterAiMovementProfile(bool OriginalSourceScoreCell = false, bool CanonicalCoordinates = false,
    bool NormalScoring = false, bool OrdinaryHumansUnchangedEquipment = false, bool ZeroCurrentAndBaselineGolem = false);
public sealed record RosterAiMovementOutcome(BattleUnitId ActorId, TilePosition Destination, int TurnEndCt,
    ImmutableArray<AiProjectedUnit> Units, BattleUnitId MoveOpportunityUnitId, ulong ElapsedTicks, ulong Ticks,
    ImmutableArray<AiUpkeepPick> UpkeepPicks, AiCandidateScore Score);
public sealed record RosterAiMovementResult(bool Accepted, RosterAiRejection Rejection, RosterAiMovementOutcome? Outcome, RandomState RngState);

/// <summary>One supplied legal roster move-only hypothesis and absolute score; no refresh, scenarios, rank selection or command authority.</summary>
public static class RosterAiMovementBaseline
{
    public static RosterAiMovementResult Evaluate(AttackBattleSnapshot state, RosterAiMovementRequest request,
        RosterAiSimulationProfile simulation, RosterAiMovementProfile profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(simulation); ArgumentNullException.ThrowIfNull(profile);
        RosterAiMovementResult Reject(RosterAiRejection reason) => new(false, reason, null, rng);
        if (!profile.OriginalSourceScoreCell || !profile.CanonicalCoordinates || !profile.NormalScoring ||
            !profile.OrdinaryHumansUnchangedEquipment || !profile.ZeroCurrentAndBaselineGolem) return Reject(RosterAiRejection.UnsupportedContext);
        var invalid = simulation.Validate(state, request.ActorId); if (invalid != RosterAiRejection.None) return Reject(invalid);
        var movement = FlatRosterMovement.Evaluate(state, request.ActorId, simulation.Battle);
        if (!movement.Accepted) return Reject(RosterAiRejection.UnsupportedProfile);
        if (!movement.Outcome!.AiOrigins.Contains(request.Destination)) return Reject(RosterAiRejection.InvalidDestination);
        var projected = AiEffectFreeProjection.Evaluate(state, request.ActorId, request.Destination, simulation.Units, rng);
        if (projected.Rejection != AiSimulationClockRejection.None) return Reject(RosterAiRefresh.ClockReason(projected.Rejection));
        int team = simulation.Battle.Units[request.ActorId].StableTeam;
        var enemies = simulation.Battle.Members.Where(u => u.UnitId != request.ActorId && u.StableTeam != team).Select(u => u.UnitId).ToImmutableHashSet();
        var clock = projected.Clock!; var score = AiCandidateScoring.ScoreFreshUnits(clock.Units, enemies, 100);
        return new(true, RosterAiRejection.None, new(request.ActorId, request.Destination, projected.TurnEndCt, clock.Units,
            clock.MoveOpportunityUnitId, clock.ElapsedTicks, clock.Ticks, clock.UpkeepPicks, score), clock.RngState);
    }
}
