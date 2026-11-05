using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>A supplied post-usefulness self cast. OpponentId identifies the hostile scorer, never the effect target.</summary>
public sealed record AccumulateAiCandidate(BattleUnitId ActorId, AssetId AbilityId, BattleUnitId OpponentId,
    TilePosition Destination, bool ActBeforeMove, bool SelfActiveAndUseful = false);

/// <summary>Explicit zero-equipment-bonus grants, original slot/death counters and ordinary effect-free clock facts.</summary>
/// <remarks>Certifies UNIT_TURN/resume PICK_UNIT and no charges, reactions, mimes, pickups, timed/upkeep/script,
/// mount, immortality, formation or other unrepresented effects. No team/control policy is inferred.</remarks>
public sealed class AccumulateAiCandidateProfile
{
    public ImmediateAbilityBattleProfile Ability { get; }
    public FlatOrdinaryMovementProfile Movement { get; }
    public ImmutableArray<AiUnitBaseline> Units { get; }

    public AccumulateAiCandidateProfile(ImmediateAbilityBattleProfile ability, FlatOrdinaryMovementProfile movement,
        IEnumerable<AiUnitBaseline> units)
    {
        ArgumentNullException.ThrowIfNull(ability); ArgumentNullException.ThrowIfNull(movement); ArgumentNullException.ThrowIfNull(units);
        var baseline = units.ToImmutableArray();
        var ids = new HashSet<BattleUnitId> { movement.FirstOpponent, movement.SecondOpponent };
        if (baseline.Length != 2 || baseline.Any(u => u is null) || !ids.SetEquals(baseline.Select(u => u.UnitId)) ||
            !ids.SetEquals(ability.Units.Keys))
            throw new ArgumentException("Certify the same two opponents/grants with one source baseline each.", nameof(units));
        Ability = ability; Movement = movement; Units = baseline;
    }
}

public enum AccumulateAiCandidateRejection
{
    None, UnknownActor, UnknownOpponent, TerminalInput, UnavailableAction, InvalidCandidate,
    UnsupportedProfile, UnknownAbility, AbilityNotGranted, SelfNotUseful, RandomDrawCountExhausted, TickCountExhausted
}
/// <summary>Diagnostic PA and hypothetical clock units, not a live battle snapshot or command receipt.</summary>
public sealed record AccumulateAiCandidateOutcome(AccumulateAiCandidate Candidate, int PhysicalAttackBefore,
    int PhysicalAttackAfter, int TurnEndCt, ImmutableArray<AiProjectedUnit> Units,
    BattleUnitId MoveOpportunityUnitId, ulong ElapsedTicks, ulong Ticks, ImmutableArray<AiUpkeepPick> UpkeepPicks);
public sealed record AccumulateAiCandidateResult(bool Accepted, AccumulateAiCandidateRejection Rejection,
    AccumulateAiCandidateOutcome? Outcome, RandomState RngState);

/// <summary>One fresh post-usefulness self candidate, with zero formula draws and the shared hypothetical clock.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-229/230. No usefulness coin, enumeration or authority.</remarks>
public static class AccumulateAiCandidateProjection
{
    public static AccumulateAiCandidateResult Evaluate(AttackBattleSnapshot state, AccumulateAiCandidate candidate,
        AccumulateAiCandidateProfile profile, RandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(candidate); ArgumentNullException.ThrowIfNull(profile);
        AccumulateAiCandidateResult Reject(AccumulateAiCandidateRejection reason) => new(false, reason, null, decisionRng);
        var invalid = Validate(state, candidate, profile);
        if (invalid != AccumulateAiCandidateRejection.None) return Reject(invalid);
        if (!candidate.SelfActiveAndUseful) return Reject(AccumulateAiCandidateRejection.SelfNotUseful);
        var effect = AiSelfStatProjection.Evaluate(state, candidate.ActorId, candidate.Destination, profile.Units, decisionRng);
        var projection = effect.Projection;
        if (projection.Rejection != AiSimulationClockRejection.None)
            return Reject(projection.Rejection == AiSimulationClockRejection.RandomDrawCountExhausted
                ? AccumulateAiCandidateRejection.RandomDrawCountExhausted : AccumulateAiCandidateRejection.TickCountExhausted);
        var clock = projection.Clock!;
        return new(true, AccumulateAiCandidateRejection.None,
            new(candidate, effect.Before, effect.After, projection.TurnEndCt, clock.Units, clock.MoveOpportunityUnitId,
                clock.ElapsedTicks, clock.Ticks, clock.UpkeepPicks), clock.RngState);
    }

    // Pure family admission also serves fresh preparation, independently of live ACT execution and usefulness.
    internal static AccumulateAiCandidateRejection Validate(AttackBattleSnapshot state, AccumulateAiCandidate candidate,
        AccumulateAiCandidateProfile profile, bool alreadyActed = false)
    {
        var invalid = ValidateState(state, candidate.ActorId, candidate.OpponentId, profile, alreadyActed);
        if (invalid != AccumulateAiCandidateRejection.None) return invalid;
        if (!profile.Ability.Abilities.ContainsKey(candidate.AbilityId)) return AccumulateAiCandidateRejection.UnknownAbility;
        if (!profile.Ability.Units[candidate.ActorId].Abilities.Contains(candidate.AbilityId)) return AccumulateAiCandidateRejection.AbilityNotGranted;
        var movement = FlatOrdinaryMovement.Evaluate(state, candidate.ActorId, profile.Movement);
        if (!movement.Accepted) return AccumulateAiCandidateRejection.UnsupportedProfile;
        return movement.Outcome!.AiOrigins.Contains(candidate.Destination)
            ? AccumulateAiCandidateRejection.None : AccumulateAiCandidateRejection.InvalidCandidate;
    }

    // Shared state admission does not pretend an absent action is being invoked.
    internal static AccumulateAiCandidateRejection ValidateState(AttackBattleSnapshot state, BattleUnitId actorId,
        BattleUnitId opponentId, AccumulateAiCandidateProfile profile, bool alreadyActed)
    {
        var actor = state.Combatants.FirstOrDefault(u => u.UnitId == actorId);
        if (actor is null) return AccumulateAiCandidateRejection.UnknownActor;
        var opponent = state.Combatants.FirstOrDefault(u => u.UnitId == opponentId);
        if (opponent is null) return AccumulateAiCandidateRejection.UnknownOpponent;
        if (actor.UnitId == opponent.UnitId) return AccumulateAiCandidateRejection.InvalidCandidate;
        if (state.Combatants.Any(u => u.Hp == 0)) return AccumulateAiCandidateRejection.TerminalInput;
        var turns = state.TurnBattle.Turns;
        var turn = turns.Units.Single(u => u.Id == actor.UnitId);
        if (turns.Phase != TurnCyclePhase.UnitTurn || turns.ActiveUnitId != actor.UnitId ||
            turn.ActionTaken != alreadyActed || alreadyActed && turn.MovementTaken)
            return AccumulateAiCandidateRejection.UnavailableAction;
        if (!profile.Units.Select(u => u.UnitId).ToHashSet().SetEquals(state.Combatants.Select(u => u.UnitId)) ||
            state.Combatants.Any(u => u.PhysicalAttack is < 1 or > 99)) return AccumulateAiCandidateRejection.UnsupportedProfile;
        return AccumulateAiCandidateRejection.None;
    }
}
