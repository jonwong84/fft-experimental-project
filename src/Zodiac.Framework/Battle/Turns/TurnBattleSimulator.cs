using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Turn-gated movement, confirmed synthetic action consumption and Wait; no combat effects.</summary>
public static class TurnBattleSimulator
{
    public static TurnBattleResult Advance(TurnBattleState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        TurnCycleResult turn = TurnCycle.Advance(state.Turns);
        return Finish(state, turn);
    }

    public static TurnBattleResult Move(TurnBattleState state, MoveUnitCommand command,
        BattleMovementPolicy movementPolicy = BattleMovementPolicy.FlatGridPrototype)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (!Enum.IsDefined(movementPolicy)) throw new ArgumentOutOfRangeException(nameof(movementPolicy));
        // Both calculations are pure. Publish neither snapshot unless both validations succeed.
        TurnCycleResult turn = TurnCycle.Apply(state.Turns, new(command.UnitId, TurnCommandKind.ConfirmMove));
        if (!turn.Accepted) return Reject(state, turn.Rejection);
        BattleTransitionResult movement = BattleSimulator.Apply(state.Battle, command, movementPolicy);
        if (!movement.Accepted) return Reject(state, TurnCycleRejection.None, movement.Rejection);
        TurnBattleState next = Synchronize(movement.State, turn.State);
        return new(true, TurnCycleRejection.None, BattleTransitionRejection.None, next,
            movement.Events.Select(ev => (TurnBattleEvent)new TurnBattleMovementEvent(ev))
                .Concat(turn.Events.Select(ev => (TurnBattleEvent)new TurnBattleClockEvent(ev))).ToImmutableArray());
    }

    /// <summary>Consumes the confirmed action choice only; does not execute an attack, ability or preview.</summary>
    public static TurnBattleResult ConfirmAct(TurnBattleState state, BattleUnitId unitId)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Finish(state, TurnCycle.Apply(state.Turns, new(unitId, TurnCommandKind.ConfirmAct)));
    }

    public static TurnBattleResult Wait(TurnBattleState state, BattleUnitId unitId)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Finish(state, TurnCycle.Apply(state.Turns, new(unitId, TurnCommandKind.Wait)));
    }

    private static TurnBattleResult Finish(TurnBattleState before, TurnCycleResult turn) => !turn.Accepted
        ? Reject(before, turn.Rejection)
        : new(true, TurnCycleRejection.None, BattleTransitionRejection.None, Synchronize(before.Battle, turn.State),
            turn.Events.Select(ev => (TurnBattleEvent)new TurnBattleClockEvent(ev)).ToImmutableArray());

    private static TurnBattleResult Reject(TurnBattleState state, TurnCycleRejection turn,
        BattleTransitionRejection movement = BattleTransitionRejection.None) => new(false, turn, movement, state, []);

    private static TurnBattleState Synchronize(BattleState battle, TurnCycleState turns)
    {
        Dictionary<BattleUnitId, bool> moved = turns.Units.ToDictionary(unit => unit.Id, unit => unit.MovementTaken);
        // Readiness clears the selected unit's flags; positions and immutable movement profiles are preserved.
        var units = battle.Units.Select(unit => new BattleUnitState(unit.Id, unit.Position, unit.MovementProfile, moved[unit.Id]));
        return new(new BattleState(battle.Map, units, turns.RngState), turns);
    }
}
