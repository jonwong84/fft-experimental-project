using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Composes the existing movement snapshot with the bounded ordinary turn clock.</summary>
public sealed class TurnBattleState
{
    public BattleState Battle { get; }
    public TurnCycleState Turns { get; }

    public TurnBattleState(BattleState battle, TurnCycleState turns)
    {
        ArgumentNullException.ThrowIfNull(battle);
        ArgumentNullException.ThrowIfNull(turns);
        if (battle.RngState is null || battle.RngState != turns.RngState)
            throw new ArgumentException("Battle and turn snapshots must carry the same explicit RNG state.");
        if (battle.Units.Count != turns.Units.Length)
            throw new ArgumentException("Battle and turn snapshots must contain the same unit IDs.");
        foreach (TurnUnitState unit in turns.Units)
        {
            if (!battle.TryGetUnit(unit.Id, out BattleUnitState? movement) || movement!.HasMoved != unit.MovementTaken)
                throw new ArgumentException($"Turn unit '{unit.Id}' is missing or has inconsistent movement availability.");
        }
        Battle = battle;
        Turns = turns;
    }
}

public abstract record TurnBattleEvent;
public sealed record TurnBattleMovementEvent(BattleMovementEvent Movement) : TurnBattleEvent;
public sealed record TurnBattleClockEvent(TurnCycleEvent Event) : TurnBattleEvent;

/// <summary>Rejections preserve the original composed state and emit no events.</summary>
public sealed record TurnBattleResult(bool Accepted, TurnCycleRejection TurnRejection,
    BattleTransitionRejection MovementRejection, TurnBattleState State, ImmutableArray<TurnBattleEvent> Events);

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
