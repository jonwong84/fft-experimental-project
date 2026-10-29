using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public enum SwordAiCandidateOrder { MoveThenAct, ActThenMove }
public sealed record SwordAiCandidate(BattleUnitId ActorId, BattleUnitId TargetId,
    TilePosition Destination, SwordAiCandidateOrder Order);

/// <summary>Explicit opt-in certificate for the ZOD-124 ordinary candidate and effect-free clock boundary.</summary>
/// <remarks>
/// The caller certifies original UNIT_TURN/resume PICK_UNIT/current actor, original slot order in TieKey,
/// no charges, mimes, timed/upkeep/terrain/script effects, restrictions, mounts, immortality or formation flags.
/// Existing ordinary sword and movement certificates apply. Unrepresented flags are never inferred from IDs.
/// This certificate cannot certify arbitrary source states or an authored host's terminal policy.
/// </remarks>
public sealed class SwordAiCandidateProfile
{
    public FlatSwordBattleProfile Sword { get; }
    public FlatOrdinaryMovementProfile Movement { get; }
    public ImmutableArray<AiUnitBaseline> Units { get; }

    public SwordAiCandidateProfile(FlatSwordBattleProfile sword, FlatOrdinaryMovementProfile movement,
        IEnumerable<AiUnitBaseline> units)
    {
        ArgumentNullException.ThrowIfNull(sword);
        ArgumentNullException.ThrowIfNull(movement);
        ArgumentNullException.ThrowIfNull(units);
        var baseline = units.ToImmutableArray();
        if (sword.OrdinaryGroundSurface != movement.Ground || baseline.Length != 2 ||
            baseline.Any(u => u is null) ||
            !baseline.Select(u => u.UnitId).ToHashSet().SetEquals([movement.FirstOpponent, movement.SecondOpponent]))
            throw new ArgumentException("Certify the same ordinary ground and two opponents with one baseline each.", nameof(units));
        Sword = sword; Movement = movement; Units = baseline;
    }
}

public enum SwordAiCandidateDisposition { Rejected, NotValuable, Projected }
public enum SwordAiCandidateRejection
{
    None, UnknownActor, UnknownTarget, TerminalInput, UnavailableAction, UnsupportedProfile,
    InvalidCandidate, OutOfRange, RandomDrawCountExhausted, TickCountExhausted
}

public sealed record SwordAiCandidateOutcome(BattleUnitId ActorId, BattleUnitId TargetId, int HitPercent,
    int NormalDamage, int TurnEndCt, ImmutableArray<AiProjectedUnit> Units,
    BattleUnitId MoveOpportunityUnitId, ulong ElapsedTicks, ulong Ticks, ImmutableArray<AiUpkeepPick> UpkeepPicks)
{
    public bool TargetKnockedOut => Units.Single(u => u.UnitId == TargetId).Status == AttackUnitStatus.Dead;
}

/// <summary>Rejected returns the incoming decision RNG. NotValuable retains four formula draws but has no outcome.</summary>
/// <remarks>Discard hypothetical data freely; the decision owner must explicitly carry supported evaluation RNG.</remarks>
public sealed record SwordAiCandidateResult(SwordAiCandidateDisposition Disposition, SwordAiCandidateRejection Rejection,
    SwordAiCandidateOutcome? Outcome, FftRandomState RngState);

/// <summary>Pure projection of one supplied ordinary sword candidate; no enumeration, score or live decision execution.</summary>
/// <remarks>Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-124/125.</remarks>
public static class SwordAiCandidateProjection
{
    public static SwordAiCandidateResult Evaluate(AttackBattleSnapshot state, SwordAiCandidate candidate,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile profile, FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(profile);
        SwordAiCandidateResult Reject(SwordAiCandidateRejection reason) =>
            new(SwordAiCandidateDisposition.Rejected, reason, null, decisionRng);

        var actor = state.Combatants.FirstOrDefault(u => u.UnitId == candidate.ActorId);
        if (actor is null) return Reject(SwordAiCandidateRejection.UnknownActor);
        var target = state.Combatants.FirstOrDefault(u => u.UnitId == candidate.TargetId);
        if (target is null) return Reject(SwordAiCandidateRejection.UnknownTarget);
        if (state.Combatants.Any(u => u.Hp == 0)) return Reject(SwordAiCandidateRejection.TerminalInput);
        var turns = state.TurnBattle.Turns;
        var actorTurn = turns.Units.Single(u => u.Id == actor.UnitId);
        if (turns.Phase != TurnCyclePhase.UnitTurn || turns.ActiveUnitId != actor.UnitId || actorTurn.ActionTaken)
            return Reject(SwordAiCandidateRejection.UnavailableAction);
        if (actor.UnitId == target.UnitId || !Enum.IsDefined(candidate.Order))
            return Reject(SwordAiCandidateRejection.InvalidCandidate);
        if (!profile.Units.Select(u => u.UnitId).ToHashSet().SetEquals(state.Combatants.Select(u => u.UnitId)))
            return Reject(SwordAiCandidateRejection.UnsupportedProfile);
        try { profile.Sword.ValidateState(state); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        { return Reject(SwordAiCandidateRejection.UnsupportedProfile); }

        // Certify origins on the initial living state. Never repeat this query after a projected KO.
        var movement = FlatOrdinaryMovement.Evaluate(state, actor.UnitId, profile.Movement);
        if (!movement.Accepted) return Reject(SwordAiCandidateRejection.UnsupportedProfile);
        if (!movement.Outcome!.AiOrigins.Contains(candidate.Destination))
            return Reject(SwordAiCandidateRejection.InvalidCandidate);
        var battle = state.TurnBattle.Battle;
        battle.TryGetUnit(actor.UnitId, out var body);
        var origin = candidate.Order == SwordAiCandidateOrder.MoveThenAct ? candidate.Destination : body!.Position;

        // A private active snapshot supplies the correct formula origin and explicit decision stream.
        // Facing and original choice flags are retained; no authoritative Move/Attack API is invoked.
        var stagedBodies = battle.Units.Select(u => u.Id == actor.UnitId
            ? new BattleUnitState(u.Id, origin, u.MovementProfile, u.HasMoved) : u);
        var staged = new AttackBattleSnapshot(new TurnBattleState(new BattleState(battle.Map, stagedBodies, decisionRng),
            new TurnCycleState(turns.Units, decisionRng, turns.Ticks, turns.Phase)), state.Combatants);
        var formula = SwordAiFormulaProjection.Evaluate(staged, new(actor.UnitId, target.UnitId), weapon, profile.Sword);
        if (!formula.Accepted)
            return Reject(formula.Rejection switch
            {
                AttackCommandRejection.OutOfRange => SwordAiCandidateRejection.OutOfRange,
                AttackCommandRejection.RandomDrawCountExhausted => SwordAiCandidateRejection.RandomDrawCountExhausted,
                _ => SwordAiCandidateRejection.UnsupportedProfile
            });
        if (!formula.Outcome!.HasAccuracy)
            return new(SwordAiCandidateDisposition.NotValuable, SwordAiCandidateRejection.None, null, formula.RngState);

        bool moved = actorTurn.MovementTaken || candidate.Destination != body!.Position;
        int turnEndCt = Math.Min(60, actorTurn.Ct + (moved ? 0 : 20)); // Act always consumed here.
        var units = AiCandidateState.CreateUnits(state, actor.UnitId, candidate.Destination,
            formula.Outcome.TargetHp, turnEndCt, moved, profile.Units);

        var clockRejection = AiSimulationClock.Advance(units, turns.Ticks, formula.RngState, out var clock);
        if (clockRejection != AiSimulationClockRejection.None)
            return Reject(clockRejection == AiSimulationClockRejection.RandomDrawCountExhausted
                ? SwordAiCandidateRejection.RandomDrawCountExhausted : SwordAiCandidateRejection.TickCountExhausted);
        return new(SwordAiCandidateDisposition.Projected, SwordAiCandidateRejection.None,
            new(actor.UnitId, target.UnitId, formula.Outcome.HitPercent, formula.Outcome.NormalDamage, turnEndCt,
                clock!.Units, clock.MoveOpportunityUnitId, clock.ElapsedTicks, clock.Ticks, clock.UpkeepPicks), clock.RngState);
    }
}
