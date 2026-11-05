using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Opaque ordinary roster session. Only this command owner creates production instances.</summary>
public sealed class RosterFacingTurnState
{
    public AttackBattleSnapshot Battle { get; }
    public FlatRosterBattleProfile Profile { get; }
    public ulong Revision { get; }
    public FacingCompletionReceipt? PendingFacing { get; }
    internal object Lineage { get; }
    internal RosterFacingTurnState(AttackBattleSnapshot battle, FlatRosterBattleProfile profile, ulong revision,
        FacingCompletionReceipt? pending, object lineage)
    { Battle = battle; Profile = profile; Revision = revision; PendingFacing = pending; Lineage = lineage; }
}

public sealed record RosterFacingResult(bool Accepted, FacingTurnRejection FacingRejection,
    AttackCommandRejection AttackRejection, TurnCycleRejection TurnRejection, FlatMovementRejection PathRejection,
    RosterFacingTurnState State, ImmutableArray<AttackResultEvent> Events, FlatMovementPath? Path = null);

/// <summary>Actual roster commands and explicit fixed/kept facing; no automatic enemy-facing policy or AI plan input.</summary>
public static class RosterFacingTurnBattle
{
    public static RosterFacingTurnState Begin(AttackBattleSnapshot battle, FlatRosterBattleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(battle); ArgumentNullException.ThrowIfNull(profile);
        profile.ValidateState(battle); battle.ValidateInitialSetup();
        var turns = battle.TurnBattle.Turns;
        if (turns.Phase != TurnCyclePhase.AddCt || turns.Ticks != 0 || turns.Units.Any(u => u.HasTurn || u.MovementTaken || u.ActionTaken))
            throw new NotSupportedException("Roster facing sessions require fresh AddCt state with no prior ticks or choices.");
        return new(battle, profile, 0, null, new object());
    }

    public static RosterFacingResult Advance(RosterFacingTurnState state) => Apply(state, () => AttackBattleSimulator.Advance(state.Battle));
    public static RosterFacingResult Attack(RosterFacingTurnState state, AttackUnitCommand command, BoundedSwordWeapon? weapon) =>
        Apply(state, () => AttackBattleSimulator.AttackRoster(state.Battle, command, weapon, state.Profile));

    /// <summary>Actual granted self PA+1 through the existing complete-roster kernel and session publisher; no decision RNG or AI authority.</summary>
    public static RosterFacingResult UseImmediateAbility(RosterFacingTurnState state, UseImmediateAbilityCommand command,
        ImmediateAbilityBattleProfile profile) => Apply(state, () => ImmediateAbilityBattleSimulator.Apply(state.Battle, command, profile));

    /// <summary>Actual roster Stone with live interception and complete-occupancy knockback through the same session publisher.</summary>
    public static RosterFacingResult UseTargetedAbility(RosterFacingTurnState state, UseTargetedAbilityCommand command,
        TargetedAbilityBattleProfile profile, FlatRosterProjectileHeights heights) =>
        Apply(state, () => TargetedAbilityBattleSimulator.ApplyRoster(state.Battle, command, profile, state.Profile, heights));

    public static RosterFacingResult UseDashAbility(RosterFacingTurnState state, UseTargetedAbilityCommand command, DashAbilityBattleProfile profile) =>
        Apply(state, () => DashAbilityBattleSimulator.ApplyRoster(state.Battle, command, profile, state.Profile));

    public static RosterFacingResult Move(RosterFacingTurnState state, MoveUnitCommand command)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(command);
        var guard = Guard(state); if (guard != FacingTurnRejection.None) return Reject(state, guard);
        var move = FlatRosterPathMovement.Move(state.Battle, command, state.Profile);
        return !move.Accepted ? new(false, FacingTurnRejection.None, AttackCommandRejection.None, move.TurnRejection, move.PathRejection, state, [])
            : Publish(state, move.State, move.Events, move.Path);
    }

    /// <summary>Null facing keeps the actual current heading; an enum value selects an explicit cardinal direction.</summary>
    public static RosterFacingResult WaitWithFacing(RosterFacingTurnState state, BattleUnitId actorId, BattleFacing? facing = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var guard = FacingReceiptRules.ValidateWait(state.Battle, state.PendingFacing, state.Revision, actorId);
        if (guard != FacingTurnRejection.None) return Reject(state, guard);
        if (!ValidFacing(state, actorId, facing)) return Reject(state, FacingTurnRejection.UnsupportedFacing);
        var wait = AttackBattleSimulator.Wait(state.Battle, actorId);
        if (!wait.Accepted) return RejectedTransition(state, wait);
        return Settle(state, wait.State, actorId, facing, wait.Events);
    }

    public static RosterFacingResult FinishFacing(RosterFacingTurnState state, BattleUnitId actorId,
        FacingCompletionReceipt? receipt, BattleFacing? facing = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var guard = FacingReceiptRules.ValidateCompletion(state.Battle, state.PendingFacing, state.Revision, state.Lineage, actorId, receipt);
        if (guard != FacingTurnRejection.None) return Reject(state, guard);
        return ValidFacing(state, actorId, facing) ? Settle(state, state.Battle, actorId, facing, [])
            : Reject(state, FacingTurnRejection.UnsupportedFacing);
    }

    private static bool ValidFacing(RosterFacingTurnState state, BattleUnitId actor, BattleFacing? facing) =>
        state.Battle.Combatants.Any(u => u.UnitId == actor && u.Hp > 0) && (facing is null || Enum.IsDefined(facing.Value));
    private static FacingTurnRejection Guard(RosterFacingTurnState state) => state.PendingFacing is not null ? FacingTurnRejection.FacingPending
        : state.Revision == ulong.MaxValue ? FacingTurnRejection.RevisionExhausted : FacingTurnRejection.None;
    private static RosterFacingResult Apply(RosterFacingTurnState state, Func<AttackBattleResult> apply)
    {
        ArgumentNullException.ThrowIfNull(state); var guard = Guard(state);
        if (guard != FacingTurnRejection.None) return Reject(state, guard);
        var result = apply(); return result.Accepted ? Publish(state, result.State, result.Events) : RejectedTransition(state, result);
    }

    private static RosterFacingResult Publish(RosterFacingTurnState before, AttackBattleSnapshot battle,
        ImmutableArray<AttackResultEvent> events, FlatMovementPath? path = null)
    {
        ulong revision = before.Revision + 1;
        var ended = events.OfType<AttackTurnBattleEvent>().Select(e => e.Event).OfType<TurnBattleClockEvent>()
            .Select(e => e.Event).OfType<TurnEndedEvent>().SingleOrDefault();
        var receipt = ended is null ? null : new FacingCompletionReceipt(ended.UnitId, revision, battle, before.Lineage);
        return new(true, FacingTurnRejection.None, AttackCommandRejection.None, TurnCycleRejection.None, FlatMovementRejection.None,
            new(battle, before.Profile, revision, receipt, before.Lineage), events, path);
    }

    private static RosterFacingResult Settle(RosterFacingTurnState before, AttackBattleSnapshot battle, BattleUnitId actorId,
        BattleFacing? facing, ImmutableArray<AttackResultEvent> events)
    {
        var actor = battle.Combatants.Single(u => u.UnitId == actorId); var settledFacing = facing ?? actor.Facing;
        var next = new AttackBattleSnapshot(battle.TurnBattle, battle.Combatants.Select(u => u.UnitId == actorId
            ? u.WithResult(u.Hp, settledFacing, u.KoCount) : u));
        return new(true, FacingTurnRejection.None, AttackCommandRejection.None, TurnCycleRejection.None, FlatMovementRejection.None,
            new(next, before.Profile, before.Revision + 1, null, before.Lineage), events.Add(new FacingSettledEvent(actorId, actor.Facing, settledFacing)));
    }

    private static RosterFacingResult Reject(RosterFacingTurnState state, FacingTurnRejection reason) =>
        new(false, reason, AttackCommandRejection.None, TurnCycleRejection.None, FlatMovementRejection.None, state, []);
    private static RosterFacingResult RejectedTransition(RosterFacingTurnState state, AttackBattleResult result) =>
        new(false, FacingTurnRejection.None, result.AttackRejection, result.TurnRejection, FlatMovementRejection.None, state, []);
}
