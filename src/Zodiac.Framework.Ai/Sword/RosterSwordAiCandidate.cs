using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Unrepresented source scoring facts for every roster unit. No pair-opponent certificate is reused.</summary>
public sealed record RosterSwordAiScoringProfile(bool NormalScoring = false,
    bool OrdinaryHumansUnchangedEquipment = false, bool ZeroCurrentAndBaselineGolem = false);
public enum RosterSwordAiCandidateRejection { None, UnsupportedScoring, UnknownActor, UnknownTarget, TerminalInput,
    UnavailableAction, UnsupportedProfile, InvalidCandidate, TargetNotEnemy, OutOfRange,
    RandomDrawCountExhausted, TickCountExhausted }
public sealed record RosterSwordAiCandidateResult(AiScoringDisposition Disposition, RosterSwordAiCandidateRejection Rejection,
    SwordAiCandidateOutcome? Outcome, AiCandidateScore? Score, RandomState RngState);

/// <summary>One supplied ordinary useful-enemy sword candidate and fresh all-unit score. No preparation, scan, rank, lottery or live command.</summary>
/// <remarks>ZOD-294/295; source 11464943bbdbd2a0af022d90cb7f10455cf911df. Initially living roster; only selected target HP changes.</remarks>
public static class RosterSwordAiCandidate
{
    public static RosterSwordAiCandidateResult Evaluate(AttackBattleSnapshot state, SwordAiCandidate candidate,
        BoundedSwordWeapon? weapon, RosterAiSimulationProfile simulation, RosterSwordAiScoringProfile scoring, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(simulation); ArgumentNullException.ThrowIfNull(scoring);
        RosterSwordAiCandidateResult Reject(RosterSwordAiCandidateRejection reason) => new(AiScoringDisposition.Rejected, reason, null, null, rng);
        if (!scoring.NormalScoring || !scoring.OrdinaryHumansUnchangedEquipment || !scoring.ZeroCurrentAndBaselineGolem)
            return Reject(RosterSwordAiCandidateRejection.UnsupportedScoring);
        var invalid = simulation.Validate(state, candidate.ActorId);
        if (invalid != RosterAiRejection.None) return Reject(invalid switch {
            RosterAiRejection.UnknownActor => RosterSwordAiCandidateRejection.UnknownActor,
            RosterAiRejection.TerminalInput => RosterSwordAiCandidateRejection.TerminalInput,
            RosterAiRejection.UnavailableAction => RosterSwordAiCandidateRejection.UnavailableAction,
            _ => RosterSwordAiCandidateRejection.UnsupportedProfile });
        var target = state.Combatants.FirstOrDefault(u => u.UnitId == candidate.TargetId);
        if (target is null) return Reject(RosterSwordAiCandidateRejection.UnknownTarget);
        var turn = state.TurnBattle.Turns.Units.Single(u => u.Id == candidate.ActorId);
        if (turn.ActionTaken) return Reject(RosterSwordAiCandidateRejection.UnavailableAction);
        if (candidate.ActorId == candidate.TargetId || !Enum.IsDefined(candidate.Order)) return Reject(RosterSwordAiCandidateRejection.InvalidCandidate);
        int team = simulation.Battle.Units[candidate.ActorId].StableTeam;
        if (simulation.Battle.Units[candidate.TargetId].StableTeam == team) return Reject(RosterSwordAiCandidateRejection.TargetNotEnemy);
        // Own legal endpoints before a possible KO; never rebuild movement against the projected corpse.
        var movement = FlatRosterMovement.Evaluate(state, candidate.ActorId, simulation.Battle);
        if (!movement.Accepted) return Reject(RosterSwordAiCandidateRejection.UnsupportedProfile);
        if (!movement.Outcome!.AiOrigins.Contains(candidate.Destination)) return Reject(RosterSwordAiCandidateRejection.InvalidCandidate);
        return EvaluateFresh(state, candidate, weapon, simulation, rng);
    }

    // Only owning fresh entry scans bypass repeated state/movement admission; never a public cached result input.
    internal static RosterSwordAiCandidateResult EvaluateFresh(AttackBattleSnapshot state, SwordAiCandidate candidate,
        BoundedSwordWeapon? weapon, RosterAiSimulationProfile simulation, RandomState rng)
    {
        RosterSwordAiCandidateResult Reject(RosterSwordAiCandidateRejection reason) => new(AiScoringDisposition.Rejected, reason, null, null, rng);
        var turn = state.TurnBattle.Turns.Units.Single(u => u.Id == candidate.ActorId);
        int team = simulation.Battle.Units[candidate.ActorId].StableTeam;
        var old = state.TurnBattle; var actor = old.Battle.Units.Single(u => u.Id == candidate.ActorId);
        var origin = candidate.Order == SwordAiCandidateOrder.MoveThenAct ? candidate.Destination : actor.Position;
        var bodies = old.Battle.Units.Select(u => u.Id == candidate.ActorId ? new BattleUnitState(u.Id, origin, u.MovementProfile, u.HasMoved) : u);
        var staged = new AttackBattleSnapshot(new(new(old.Battle.Map, bodies, rng),
            new(old.Turns.Units, rng, old.Turns.Ticks, old.Turns.Phase)), state.Combatants);
        var formula = SwordAiFormulaProjection.EvaluateRoster(staged, new(candidate.ActorId, candidate.TargetId), weapon, simulation.Battle);
        if (!formula.Accepted) return Reject(formula.Rejection switch {
            AttackCommandRejection.OutOfRange => RosterSwordAiCandidateRejection.OutOfRange,
            AttackCommandRejection.RandomDrawCountExhausted => RosterSwordAiCandidateRejection.RandomDrawCountExhausted,
            _ => RosterSwordAiCandidateRejection.UnsupportedProfile });
        if (!formula.Outcome!.HasAccuracy) return new(AiScoringDisposition.NotValuable, RosterSwordAiCandidateRejection.None, null, null, formula.RngState);
        bool moved = turn.MovementTaken || candidate.Destination != actor.Position;
        int endCt = Math.Min(60, turn.Ct + (moved ? 0 : 20));
        var units = AiCandidateState.CreateUnits(state, candidate.ActorId, candidate.TargetId, candidate.Destination,
            formula.Outcome.TargetHp, endCt, moved, simulation.Units);
        var clockReason = AiSimulationClock.Advance(units, old.Turns.Ticks, formula.RngState, out var clock);
        if (clockReason != AiSimulationClockRejection.None) return Reject(clockReason == AiSimulationClockRejection.RandomDrawCountExhausted
            ? RosterSwordAiCandidateRejection.RandomDrawCountExhausted : RosterSwordAiCandidateRejection.TickCountExhausted);
        var enemies = simulation.Battle.Members.Where(u => u.StableTeam != team).Select(u => u.UnitId).ToImmutableHashSet();
        var outcome = new SwordAiCandidateOutcome(candidate.ActorId, candidate.TargetId, formula.Outcome.HitPercent, formula.Outcome.NormalDamage,
            endCt, clock!.Units, clock.MoveOpportunityUnitId, clock.ElapsedTicks, clock.Ticks, clock.UpkeepPicks);
        return new(AiScoringDisposition.Scored, RosterSwordAiCandidateRejection.None, outcome,
            AiCandidateScoring.ScoreFreshUnits(clock.Units, enemies, (byte)formula.Outcome.HitPercent), clock.RngState);
    }
}
