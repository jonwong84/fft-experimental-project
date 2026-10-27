using System.Collections.Immutable;

namespace Zodiac.Framework;

public enum SwordAiCandidateOrder { MoveThenAct, ActThenMove }
public sealed record SwordAiCandidate(BattleUnitId ActorId, BattleUnitId TargetId,
    TilePosition Destination, SwordAiCandidateOrder Order);

/// <summary>Original selected-state death counter, absent from the ordinary live clock model.</summary>
public sealed record SwordAiUnitBaseline(BattleUnitId UnitId, byte DeathCounter);

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
    public ImmutableArray<SwordAiUnitBaseline> Units { get; }

    public SwordAiCandidateProfile(FlatSwordBattleProfile sword, FlatOrdinaryMovementProfile movement,
        IEnumerable<SwordAiUnitBaseline> units)
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

/// <summary>Hypothetical data only; no active turn, command receipt or executable battle snapshot.</summary>
public sealed record SwordAiProjectedUnit(BattleUnitId UnitId, TilePosition Position, BattleFacing Facing,
    int Hp, int MaxHp, AttackUnitStatus Status, byte DeathCounter, int Speed, int TieKey, int Ct,
    bool MovementTaken, bool ActionTaken);
public sealed record SwordAiUpkeepPick(BattleUnitId UnitId, int CtBefore, int CtAfter, bool Dead, uint RawRoll);
public sealed record SwordAiCandidateOutcome(BattleUnitId ActorId, BattleUnitId TargetId, int HitPercent,
    int NormalDamage, int TurnEndCt, ImmutableArray<SwordAiProjectedUnit> Units,
    BattleUnitId MoveOpportunityUnitId, ulong ElapsedTicks, ulong Ticks, ImmutableArray<SwordAiUpkeepPick> UpkeepPicks)
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
        // Stable source slot order, independent of all input collection orders.
        var units = turns.Units.OrderBy(u => u.TieKey).Select(turn =>
        {
            var combat = state.Combatants.Single(u => u.UnitId == turn.Id);
            battle.TryGetUnit(turn.Id, out var unitBody);
            bool isActor = turn.Id == actor.UnitId;
            int hp = isActor ? combat.Hp : formula.Outcome.TargetHp;
            var status = hp == 0 ? AttackUnitStatus.Dead : hp <= combat.MaxHp / 5 ? AttackUnitStatus.Critical : AttackUnitStatus.None;
            return new SwordAiProjectedUnit(turn.Id, isActor ? candidate.Destination : unitBody!.Position, combat.Facing,
                hp, combat.MaxHp, status, hp == 0 ? (byte)3 : profile.Units.Single(u => u.UnitId == turn.Id).DeathCounter,
                turn.Speed, turn.TieKey, isActor ? turnEndCt : turn.Ct,
                isActor ? moved : turn.MovementTaken, isActor || turn.ActionTaken);
        }).ToArray();

        var clockRejection = SwordAiSimulationClock.Advance(units, turns.Ticks, formula.RngState, out var clock);
        if (clockRejection != SwordAiCandidateRejection.None) return Reject(clockRejection);
        return new(SwordAiCandidateDisposition.Projected, SwordAiCandidateRejection.None,
            new(actor.UnitId, target.UnitId, formula.Outcome.HitPercent, formula.Outcome.NormalDamage, turnEndCt,
                clock!.Units, clock.MoveOpportunityUnitId, clock.ElapsedTicks, clock.Ticks, clock.UpkeepPicks), clock.RngState);
    }
}
