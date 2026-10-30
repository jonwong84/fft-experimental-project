using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public sealed record ThrowStoneAiCandidate(BattleUnitId ActorId, AssetId AbilityId, BattleUnitId TargetId,
    TilePosition Destination, bool ActBeforeMove);

/// <summary>Original slot/death counters and effect-free clock facts, not inferred from ability IDs.</summary>
/// <remarks>Certifies original UNIT_TURN/resume PICK_UNIT, TieKey slot order, no charges, reactions, mimes,
/// timed/upkeep/terrain/script effects, restrictions, mounts, immortality or formation flags.</remarks>
public sealed class ThrowStoneAiCandidateProfile
{
    public TargetedAbilityBattleProfile Ability { get; }
    public FlatOrdinaryMovementProfile Movement { get; }
    public ImmutableArray<AiUnitBaseline> Units { get; }
    public ThrowStoneAiCandidateProfile(TargetedAbilityBattleProfile ability, FlatOrdinaryMovementProfile movement,
        IEnumerable<AiUnitBaseline> units)
    {
        ArgumentNullException.ThrowIfNull(ability); ArgumentNullException.ThrowIfNull(movement); ArgumentNullException.ThrowIfNull(units);
        var baseline = units.ToImmutableArray();
        var ids = new HashSet<BattleUnitId> { movement.FirstOpponent, movement.SecondOpponent };
        if (ability.OrdinaryGroundSurface != movement.Ground || baseline.Length != 2 || baseline.Any(u => u is null) ||
            !ids.SetEquals(baseline.Select(u => u.UnitId)) || !ids.SetEquals(ability.Units.Keys))
            throw new ArgumentException("Certify the same ordinary ground and two units/grants with one baseline each.", nameof(units));
        Ability = ability; Movement = movement; Units = baseline;
    }
}
public enum ThrowStoneAiCandidateDisposition { Rejected, NotValuable, Projected }
public enum ThrowStoneAiCandidateRejection
{
    None, UnknownActor, UnknownTarget, TerminalInput, UnavailableAction, UnsupportedProfile,
    InvalidCandidate, OutOfRange, FormulaRejected, RandomDrawCountExhausted, TickCountExhausted
}
public sealed record ThrowStoneAiCandidateOutcome(BattleUnitId ActorId, AssetId AbilityId, BattleUnitId TargetId,
    int HitPercent, int NormalDamage, int TurnEndCt, ImmutableArray<AiProjectedUnit> Units,
    BattleUnitId MoveOpportunityUnitId, ulong ElapsedTicks, ulong Ticks, ImmutableArray<AiUpkeepPick> UpkeepPicks);
public sealed record ThrowStoneAiCandidateResult(ThrowStoneAiCandidateDisposition Disposition,
    ThrowStoneAiCandidateRejection Rejection, ThrowStoneAiCandidateOutcome? Outcome, FftRandomState RngState)
{
    public AttackCommandRejection FormulaRejection { get; init; }
}

/// <summary>One fresh supplied Throw Stone candidate followed by the shared hypothetical clock; no scoring or authority.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-185/187. Baseline geometry is checked before any projected KO.</remarks>
public static class ThrowStoneAiCandidateProjection
{
    public static ThrowStoneAiCandidateResult Evaluate(AttackBattleSnapshot state, ThrowStoneAiCandidate candidate,
        ThrowStoneAiCandidateProfile profile, FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(candidate); ArgumentNullException.ThrowIfNull(profile);
        ThrowStoneAiCandidateResult Reject(ThrowStoneAiCandidateRejection reason, AttackCommandRejection formula = AttackCommandRejection.None) =>
            new(ThrowStoneAiCandidateDisposition.Rejected, reason, null, decisionRng) { FormulaRejection = formula };
        var actor = state.Combatants.FirstOrDefault(u => u.UnitId == candidate.ActorId);
        if (actor is null) return Reject(ThrowStoneAiCandidateRejection.UnknownActor);
        var target = state.Combatants.FirstOrDefault(u => u.UnitId == candidate.TargetId);
        if (target is null) return Reject(ThrowStoneAiCandidateRejection.UnknownTarget);
        if (state.Combatants.Any(u => u.Hp == 0)) return Reject(ThrowStoneAiCandidateRejection.TerminalInput);
        var turns = state.TurnBattle.Turns; var turn = turns.Units.Single(u => u.Id == actor.UnitId);
        if (turns.Phase != TurnCyclePhase.UnitTurn || turns.ActiveUnitId != actor.UnitId || turn.ActionTaken)
            return Reject(ThrowStoneAiCandidateRejection.UnavailableAction);
        if (actor.UnitId == target.UnitId) return Reject(ThrowStoneAiCandidateRejection.InvalidCandidate);
        if (!profile.Units.Select(u => u.UnitId).ToHashSet().SetEquals(state.Combatants.Select(u => u.UnitId)))
            return Reject(ThrowStoneAiCandidateRejection.UnsupportedProfile);
        var movement = FlatOrdinaryMovement.Evaluate(state, actor.UnitId, profile.Movement);
        if (!movement.Accepted) return Reject(ThrowStoneAiCandidateRejection.UnsupportedProfile);
        if (!movement.Outcome!.AiOrigins.Contains(candidate.Destination)) return Reject(ThrowStoneAiCandidateRejection.InvalidCandidate);
        var battle = state.TurnBattle.Battle; battle.TryGetUnit(actor.UnitId, out var body);
        var origin = candidate.ActBeforeMove ? body!.Position : candidate.Destination;
        var bodies = battle.Units.Select(u => u.Id == actor.UnitId ? new BattleUnitState(u.Id, origin, u.MovementProfile, u.HasMoved) : u);
        var staged = new AttackBattleSnapshot(new(new(battle.Map, bodies, decisionRng),
            new(turns.Units, decisionRng, turns.Ticks, turns.Phase)), state.Combatants);
        var formula = ThrowStoneAiFormulaProjection.Evaluate(staged, new(actor.UnitId, candidate.AbilityId, target.UnitId), profile.Ability, decisionRng);
        if (!formula.Accepted)
            return Reject(formula.Rejection switch
            {
                AttackCommandRejection.OutOfRange => ThrowStoneAiCandidateRejection.OutOfRange,
                AttackCommandRejection.RandomDrawCountExhausted => ThrowStoneAiCandidateRejection.RandomDrawCountExhausted,
                _ => ThrowStoneAiCandidateRejection.FormulaRejected
            }, formula.Rejection);
        if (!formula.Outcome!.HasAccuracy)
            return new(ThrowStoneAiCandidateDisposition.NotValuable, ThrowStoneAiCandidateRejection.None, null, formula.RngState);
        bool moved = turn.MovementTaken || candidate.Destination != body!.Position;
        int endCt = Math.Min(60, turn.Ct + (moved ? 0 : 20));
        var units = AiCandidateState.CreateUnits(state, actor.UnitId, candidate.Destination, formula.Outcome.TargetHp, endCt, moved, profile.Units);
        var invalid = AiSimulationClock.Advance(units, turns.Ticks, formula.RngState, out var clock);
        if (invalid != AiSimulationClockRejection.None)
            return Reject(invalid == AiSimulationClockRejection.RandomDrawCountExhausted
                ? ThrowStoneAiCandidateRejection.RandomDrawCountExhausted : ThrowStoneAiCandidateRejection.TickCountExhausted);
        return new(ThrowStoneAiCandidateDisposition.Projected, ThrowStoneAiCandidateRejection.None,
            new(actor.UnitId, candidate.AbilityId, target.UnitId, formula.Outcome.HitPercent, formula.Outcome.NormalDamage,
                endCt, clock!.Units, clock.MoveOpportunityUnitId, clock.ElapsedTicks, clock.Ticks, clock.UpkeepPicks), clock.RngState);
    }
}
