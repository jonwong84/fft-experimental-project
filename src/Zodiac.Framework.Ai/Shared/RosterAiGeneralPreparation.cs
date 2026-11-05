using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Unrepresented control/decision facts. Initial auto-battle setting is overwritten in this automatic, non-special domain.</summary>
public sealed record RosterAiGeneralProfile(bool NoDirectPlayerControl = false, bool NoSpecialDecisionBehavior = false);
public enum RosterAiGeneralRejection { None, UnsupportedContext, PreparationRejected, BlueHealerFocus,
    ConditionalRetreat, FadingLife, MovementRejected, RandomDrawCountExhausted, TickCountExhausted }
public sealed record RosterAiGeneralFailure(RosterAiGeneralRejection Reason,
    RosterAiRejection PreparationReason = RosterAiRejection.None, int? MovementIndex = null);
public sealed record RosterAiGeneralPreparationOutcome(RosterAiPreparationOutcome Preparation,
    AiRosterNearestOutcome Focus, int FocusReach, FlatMovementOutcome Geometry, AiRosterActiveOutcome ActiveTargets,
    AiRosterProximityOutcome Proximity);
public sealed record RosterAiGeneralPreparationResult(bool Accepted, RosterAiGeneralFailure? Failure,
    RosterAiGeneralPreparationOutcome? Outcome, RandomState RngState);

/// <summary>One fresh automatic manual/general prefix; derive policy admission and focus, never accept a cached preparation.</summary>
/// <remarks>ZOD-287/288; decomp 11464943bbdbd2a0af022d90cb7f10455cf911df. No action search or live publication.</remarks>
public static class RosterAiGeneralPreparation
{
    public static RosterAiGeneralPreparationResult Evaluate(AttackBattleSnapshot state, RosterAiRefreshRequest request,
        RosterAiSimulationProfile simulation, RosterAiRefreshProfile refresh, AiHumanAbilityLists lists,
        RosterAiGeneralProfile profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(profile);
        RosterAiGeneralPreparationResult Reject(RosterAiGeneralRejection reason, RosterAiRejection preparation = RosterAiRejection.None) =>
            new(false, new(reason, preparation), null, rng);
        if (!profile.NoDirectPlayerControl || !profile.NoSpecialDecisionBehavior) return Reject(RosterAiGeneralRejection.UnsupportedContext);
        var prepared = RosterAiPreparation.Evaluate(state, request, simulation, refresh, lists, rng);
        if (!prepared.Accepted) return Reject(RosterAiGeneralRejection.PreparationRejected, prepared.Rejection);
        var p = prepared.Outcome!; int team = simulation.Battle.Units[request.ActorId].StableTeam;
        var actor = state.Combatants.Single(u => u.UnitId == request.ActorId);
        // Target consideration precedes action_select. Unsupported healer focus is not replaced by enemy focus.
        if (team == 0 && actor.Hp * 128 / actor.MaxHp < 45) return Reject(RosterAiGeneralRejection.BlueHealerFocus);
        if (actor.Status == AttackUnitStatus.Critical && p.TeamSupport.Available) return Reject(RosterAiGeneralRejection.ConditionalRetreat);
        if (team != 0 && p.Units.Single(u => u.UnitId == request.ActorId).MostlySupport == true &&
            !p.TeamSupport.Policies.Single(u => u.UnitId == request.ActorId).TeamSupportUnavailable)
            return Reject(RosterAiGeneralRejection.FadingLife);
        var focus = AiRosterNearest.FindEligibleEnemy(p.Targets, simulation.Battle.Members.Select(u => u.UnitId));
        var movement = FlatRosterMovement.Evaluate(state, request.ActorId, simulation.Battle, focusId: focus.TargetId);
        if (!movement.Accepted) return Reject(RosterAiGeneralRejection.MovementRejected);
        var geometry = movement.Outcome!;
        // Source scenarios_build clears the complete distance grid for a focused actor.
        if (focus.TargetId == request.ActorId)
            geometry = geometry with { TargetDistanceRows = geometry.TargetDistanceRows.Select(row =>
                row with { Distances = row.Distances.Select(_ => (byte)0).ToImmutableArray() }).ToImmutableArray() };
        int reach = p.Targets.ById[focus.TargetId].Decision.Enemy ? p.EnemyAbilityReach : p.AllyAbilityReach;
        return new(true, null, new(p, focus, reach, geometry,
            AiRosterActiveTargets.Select(p.Targets, AiRosterTargetFilter.Any),
            AiRosterProximity.Evaluate(p.Targets, geometry.AiOrigins)), prepared.RngState);
    }
}
