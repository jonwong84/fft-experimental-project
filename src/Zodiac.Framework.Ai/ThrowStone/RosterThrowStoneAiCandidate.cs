using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Unrepresented source facts for one supplied candidate, including its order-specific trajectory against every occupant.</summary>
/// <remarks>ClearDirectTrajectory includes actual sprite heights and terrain. No computed obstruction or grant inference is claimed.</remarks>
public sealed record RosterThrowStoneAiCandidateProfile(bool ClearDirectTrajectory = false,
    bool NormalScoring = false, bool OrdinaryHumansUnchangedEquipment = false, bool ZeroCurrentAndBaselineGolem = false);
public enum RosterThrowStoneAiCandidateRejection { None, UnsupportedTrajectory, UnsupportedScoring, UnknownActor, UnknownTarget,
    TerminalInput, UnavailableAction, UnsupportedProfile, InvalidCandidate, TargetNotEnemy, UnknownAbility, AbilityNotGranted,
    OutOfRange, RandomDrawCountExhausted, TickCountExhausted }
public sealed record RosterThrowStoneAiCandidateResult(AiScoringDisposition Disposition, RosterThrowStoneAiCandidateRejection Rejection,
    ThrowStoneAiCandidateOutcome? Outcome, AiCandidateScore? Score, RandomState RngState);

/// <summary>One supplied certified-clear roster Stone candidate with fresh whole-roster clock and score. No preparation, search or authority.</summary>
/// <remarks>ZOD-297/298; source 11464943bbdbd2a0af022d90cb7f10455cf911df. Pair command admission remains unchanged.</remarks>
public static class RosterThrowStoneAiCandidate
{
    public static RosterThrowStoneAiCandidateResult Evaluate(AttackBattleSnapshot state, ThrowStoneAiCandidate candidate,
        TargetedAbilityBattleProfile ability, RosterAiSimulationProfile simulation, RosterThrowStoneAiCandidateProfile profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(ability); ArgumentNullException.ThrowIfNull(simulation); ArgumentNullException.ThrowIfNull(profile);
        RosterThrowStoneAiCandidateResult Reject(RosterThrowStoneAiCandidateRejection reason) => new(AiScoringDisposition.Rejected, reason, null, null, rng);
        if (!profile.ClearDirectTrajectory) return Reject(RosterThrowStoneAiCandidateRejection.UnsupportedTrajectory);
        if (!profile.NormalScoring || !profile.OrdinaryHumansUnchangedEquipment || !profile.ZeroCurrentAndBaselineGolem)
            return Reject(RosterThrowStoneAiCandidateRejection.UnsupportedScoring);
        var invalid = simulation.Validate(state, candidate.ActorId);
        if (invalid != RosterAiRejection.None) return Reject(invalid switch {
            RosterAiRejection.UnknownActor => RosterThrowStoneAiCandidateRejection.UnknownActor,
            RosterAiRejection.TerminalInput => RosterThrowStoneAiCandidateRejection.TerminalInput,
            RosterAiRejection.UnavailableAction => RosterThrowStoneAiCandidateRejection.UnavailableAction,
            _ => RosterThrowStoneAiCandidateRejection.UnsupportedProfile });
        var target = state.Combatants.FirstOrDefault(u => u.UnitId == candidate.TargetId);
        if (target is null) return Reject(RosterThrowStoneAiCandidateRejection.UnknownTarget);
        var turn = state.TurnBattle.Turns.Units.Single(u => u.Id == candidate.ActorId);
        if (turn.ActionTaken) return Reject(RosterThrowStoneAiCandidateRejection.UnavailableAction);
        if (candidate.ActorId == candidate.TargetId) return Reject(RosterThrowStoneAiCandidateRejection.InvalidCandidate);
        int team = simulation.Battle.Units[candidate.ActorId].StableTeam;
        if (simulation.Battle.Units[candidate.TargetId].StableTeam == team) return Reject(RosterThrowStoneAiCandidateRejection.TargetNotEnemy);
        var setup = ValidateAbility(state, candidate.ActorId, candidate.AbilityId, ability, simulation);
        if (setup != RosterThrowStoneAiCandidateRejection.None) return Reject(setup);
        // Legal endpoints belong to the initial living roster, before a projected KO changes occupancy.
        var movement = FlatRosterMovement.Evaluate(state, candidate.ActorId, simulation.Battle);
        if (!movement.Accepted) return Reject(RosterThrowStoneAiCandidateRejection.UnsupportedProfile);
        if (!movement.Outcome!.AiOrigins.Contains(candidate.Destination)) return Reject(RosterThrowStoneAiCandidateRejection.InvalidCandidate);
        var old = state.TurnBattle; var actor = old.Battle.Units.Single(u => u.Id == candidate.ActorId);
        var origin = candidate.ActBeforeMove ? actor.Position : candidate.Destination;
        var targetPosition = old.Battle.Units.Single(u => u.Id == candidate.TargetId).Position;
        if (AiRosterNearest.Distance(origin, targetPosition) is < 1 or > 4) return Reject(RosterThrowStoneAiCandidateRejection.OutOfRange);
        return EvaluateFresh(state, candidate, simulation, rng);
    }

    internal static RosterThrowStoneAiCandidateRejection ValidateAbility(AttackBattleSnapshot state, BattleUnitId actorId, AssetId abilityId,
        TargetedAbilityBattleProfile ability, RosterAiSimulationProfile simulation)
    {
        if (ability.OrdinaryGroundSurface != simulation.Battle.Ground || ability.Units.Count != simulation.Battle.Units.Count ||
            !ability.Units.Keys.ToHashSet().SetEquals(simulation.Battle.Units.Keys) || state.Combatants.Any(u => u.PhysicalAttack is < 1 or > 99))
            return RosterThrowStoneAiCandidateRejection.UnsupportedProfile;
        if (!ability.Abilities.ContainsKey(abilityId)) return RosterThrowStoneAiCandidateRejection.UnknownAbility;
        return ability.Units[actorId].Abilities.Contains(abilityId) ? RosterThrowStoneAiCandidateRejection.None : RosterThrowStoneAiCandidateRejection.AbilityNotGranted;
    }

    // Fresh entry ownership only: admitted initial roster, effect/grant, legal destination, range and computed clear trajectory.
    internal static RosterThrowStoneAiCandidateResult EvaluateFresh(AttackBattleSnapshot state, ThrowStoneAiCandidate candidate,
        RosterAiSimulationProfile simulation, RandomState rng)
    {
        RosterThrowStoneAiCandidateResult Reject(RosterThrowStoneAiCandidateRejection reason) => new(AiScoringDisposition.Rejected, reason, null, null, rng);
        var turn = state.TurnBattle.Turns.Units.Single(u => u.Id == candidate.ActorId);
        int team = simulation.Battle.Units[candidate.ActorId].StableTeam;
        var old = state.TurnBattle; var actor = old.Battle.Units.Single(u => u.Id == candidate.ActorId);
        var origin = candidate.ActBeforeMove ? actor.Position : candidate.Destination;
        var bodies = old.Battle.Units.Select(u => u.Id == candidate.ActorId ? new BattleUnitState(u.Id, origin, u.MovementProfile, u.HasMoved) : u);
        var staged = new AttackBattleSnapshot(new(new(old.Battle.Map, bodies, rng),
            new(old.Turns.Units, rng, old.Turns.Ticks, old.Turns.Phase)), state.Combatants);
        var formula = ThrowStoneAiFormulaProjection.EvaluateAdmitted(staged, new(candidate.ActorId, candidate.AbilityId, candidate.TargetId), rng);
        if (!formula.Accepted) return Reject(RosterThrowStoneAiCandidateRejection.RandomDrawCountExhausted);
        if (!formula.Outcome!.HasAccuracy) return new(AiScoringDisposition.NotValuable, RosterThrowStoneAiCandidateRejection.None, null, null, formula.RngState);
        bool moved = turn.MovementTaken || candidate.Destination != actor.Position;
        int endCt = Math.Min(60, turn.Ct + (moved ? 0 : 20));
        var units = AiCandidateState.CreateUnits(state, candidate.ActorId, candidate.TargetId, candidate.Destination,
            formula.Outcome.TargetHp, endCt, moved, simulation.Units);
        var reason = AiSimulationClock.Advance(units, old.Turns.Ticks, formula.RngState, out var clock);
        if (reason != AiSimulationClockRejection.None) return Reject(reason == AiSimulationClockRejection.RandomDrawCountExhausted
            ? RosterThrowStoneAiCandidateRejection.RandomDrawCountExhausted : RosterThrowStoneAiCandidateRejection.TickCountExhausted);
        var enemies = simulation.Battle.Members.Where(u => u.StableTeam != team).Select(u => u.UnitId).ToImmutableHashSet();
        var outcome = new ThrowStoneAiCandidateOutcome(candidate.ActorId, candidate.AbilityId, candidate.TargetId,
            formula.Outcome.HitPercent, formula.Outcome.NormalDamage, endCt, clock!.Units, clock.MoveOpportunityUnitId,
            clock.ElapsedTicks, clock.Ticks, clock.UpkeepPicks);
        return new(AiScoringDisposition.Scored, RosterThrowStoneAiCandidateRejection.None, outcome,
            AiCandidateScoring.ScoreFreshUnits(clock.Units, enemies, (byte)formula.Outcome.HitPercent), clock.RngState);
    }
}
