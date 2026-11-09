using System.Collections.Immutable;
using Zodiac.Framework.Random;
using Zodiac.Framework.CombatRules;
using Zodiac.Framework.Formulas;

namespace Zodiac.Framework.Ai;

public sealed record DashAiCandidate(BattleUnitId ActorId, AssetId AbilityId, BattleUnitId TargetId, TilePosition Destination, bool ActBeforeMove);
public sealed record RosterDashAiCandidateProfile(bool NormalScoring = false, bool OrdinaryHumansUnchangedEquipment = false, bool ZeroCurrentAndBaselineGolem = false);
public enum RosterDashAiCandidateRejection { None, UnknownActor, UnknownTarget, TerminalInput, UnavailableAction,
    UnsupportedProfile, UnsupportedScoring, InvalidCandidate, TargetNotEnemy, AbilityNotGranted, UnknownAbility, OutOfRange,
    RandomDrawCountExhausted, TickCountExhausted }
public sealed record DashAiCandidateOutcome(BattleUnitId ActorId, AssetId AbilityId, BattleUnitId TargetId,
    int HitPercent, int NormalDamage, int TurnEndCt, ImmutableArray<AiProjectedUnit> Units,
    BattleUnitId MoveOpportunityUnitId, ulong ElapsedTicks, ulong Ticks, ImmutableArray<AiUpkeepPick> UpkeepPicks);
public sealed record RosterDashAiCandidateResult(AiScoringDisposition Disposition, RosterDashAiCandidateRejection Rejection,
    DashAiCandidateOutcome? Outcome, AiCandidateScore? Score, RandomState RngState);

/// <summary>One isolated adjacent neutral physical hypothesis using its selected formula, then shared all-unit clock/scoring.</summary>
/// <remarks>ZOD-322; source 11464943bbdbd2a0af022d90cb7f10455cf911df. Never publishes a command or projected battle.</remarks>
internal static class RosterDashAiCandidate
{
    public static RosterDashAiCandidateResult Evaluate(AttackBattleSnapshot state, DashAiCandidate candidate,
        RosterAiSimulationProfile simulation, DashAbilityBattleProfile ability, RosterDashAiCandidateProfile profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(simulation); ArgumentNullException.ThrowIfNull(ability);
        ArgumentNullException.ThrowIfNull(profile);
        RosterDashAiCandidateResult Reject(RosterDashAiCandidateRejection r) => new(AiScoringDisposition.Rejected, r, null, null, rng);
        if (!profile.NormalScoring || !profile.OrdinaryHumansUnchangedEquipment || !profile.ZeroCurrentAndBaselineGolem)
            return Reject(RosterDashAiCandidateRejection.UnsupportedScoring);
        var admission = simulation.Validate(state, candidate.ActorId);
        if (admission != RosterAiRejection.None) return Reject(admission switch {
            RosterAiRejection.UnknownActor => RosterDashAiCandidateRejection.UnknownActor,
            RosterAiRejection.TerminalInput => RosterDashAiCandidateRejection.TerminalInput,
            RosterAiRejection.UnavailableAction => RosterDashAiCandidateRejection.UnavailableAction,
            _ => RosterDashAiCandidateRejection.UnsupportedProfile });
        if (!state.Combatants.Any(u => u.UnitId == candidate.TargetId)) return Reject(RosterDashAiCandidateRejection.UnknownTarget);
        if (state.TurnBattle.Turns.Units.Single(u => u.Id == candidate.ActorId).ActionTaken) return Reject(RosterDashAiCandidateRejection.UnavailableAction);
        if (candidate.ActorId == candidate.TargetId) return Reject(RosterDashAiCandidateRejection.InvalidCandidate);
        if (simulation.Battle.Units[candidate.ActorId].StableTeam == simulation.Battle.Units[candidate.TargetId].StableTeam)
            return Reject(RosterDashAiCandidateRejection.TargetNotEnemy);
        var setup = ValidateAbility(state, candidate.ActorId, candidate.AbilityId, ability, simulation);
        if (setup != RosterDashAiCandidateRejection.None) return Reject(setup);
        var movement = FlatRosterMovement.Evaluate(state, candidate.ActorId, simulation.Battle);
        if (!movement.Accepted || !movement.Outcome!.AiOrigins.Contains(candidate.Destination)) return Reject(RosterDashAiCandidateRejection.InvalidCandidate);
        var origin = candidate.ActBeforeMove ? state.TurnBattle.Battle.Units.Single(u => u.Id == candidate.ActorId).Position : candidate.Destination;
        var target = state.TurnBattle.Battle.Units.Single(u => u.Id == candidate.TargetId).Position;
        return AiRosterNearest.Distance(origin, target) != 1 ? Reject(RosterDashAiCandidateRejection.OutOfRange) : EvaluateFresh(state, candidate, simulation, ability.PhysicalProfile.Abilities[candidate.AbilityId], rng);
    }

    internal static RosterDashAiCandidateRejection ValidateAbility(AttackBattleSnapshot state, BattleUnitId actor,
        AssetId id, DashAbilityBattleProfile ability, RosterAiSimulationProfile simulation) =>
        DashAbilityBattleSimulator.ValidateAbilitySetup(state, actor, id, ability, simulation.Battle) switch {
            AttackCommandRejection.None => RosterDashAiCandidateRejection.None,
            AttackCommandRejection.UnknownAbility => RosterDashAiCandidateRejection.UnknownAbility,
            AttackCommandRejection.AbilityNotGranted => RosterDashAiCandidateRejection.AbilityNotGranted,
            _ => RosterDashAiCandidateRejection.UnsupportedProfile };

    // Only owning fresh entry/candidate admission can enter here. Formula has no target-facing/trajectory dependency.
    internal static RosterDashAiCandidateResult EvaluateFresh(AttackBattleSnapshot state, DashAiCandidate candidate,
        RosterAiSimulationProfile simulation, PhysicalAbilityDefinition ability, RandomState rng)
    {
        var actor = state.Combatants.Single(u => u.UnitId == candidate.ActorId);
        var target = state.Combatants.Single(u => u.UnitId == candidate.TargetId);
        var origin = candidate.ActBeforeMove ? state.TurnBattle.Battle.Units.Single(u => u.Id == actor.UnitId).Position : candidate.Destination;
        var to = state.TurnBattle.Battle.Units.Single(u => u.Id == target.UnitId).Position;
        var formula = PhysicalDamageRules.Calculate(ability, actor.PhysicalAttack, actor.Brave, target.Brave,
            target.ClassPhysicalEvade, (long)to.X-origin.X, (long)to.Y-origin.Y, target.Facing, AbilityFormulaMode.Ai, rng);
        if (!formula.Accepted) return new(AiScoringDisposition.Rejected, RosterDashAiCandidateRejection.RandomDrawCountExhausted, null, null, rng);
        if (!formula.Outcome!.Hit) return new(AiScoringDisposition.NotValuable, RosterDashAiCandidateRejection.None, null, null, formula.RngState);
        int damage = formula.Outcome!.Damage;
        var reason = AiDamageHypotheses.Complete(state, candidate.ActorId, candidate.TargetId, candidate.Destination,
            Math.Max(0, target.Hp - damage), checked((byte)formula.Outcome.Accuracy), simulation, formula.RngState, out var completed);
        if (reason != AiSimulationClockRejection.None) return new(AiScoringDisposition.Rejected,
            reason == AiSimulationClockRejection.RandomDrawCountExhausted ? RosterDashAiCandidateRejection.RandomDrawCountExhausted : RosterDashAiCandidateRejection.TickCountExhausted, null, null, rng);
        var c = completed!.Clock;
        return new(AiScoringDisposition.Scored, RosterDashAiCandidateRejection.None,
            new(candidate.ActorId, candidate.AbilityId, candidate.TargetId, formula.Outcome.Accuracy, damage, completed.TurnEndCt, c.Units, c.MoveOpportunityUnitId, c.ElapsedTicks, c.Ticks, c.UpkeepPicks),
            completed.Score, c.RngState);
    }
}
