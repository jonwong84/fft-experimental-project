namespace Fft.Core;

/// <summary>Applies the initial, deliberately limited battle command set.</summary>
/// <remarks>
/// Movement currently uses <see cref="FlatGridMovementRange"/>. This transition demonstrates
/// immutable state and command handling; it is not a source-faithful implementation of FFT movement.
/// </remarks>
public static class BattleSimulator
{
    public static BattleTransitionResult Apply(BattleState state, MoveUnitCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        if (!state.TryGetUnit(command.UnitId, out BattleUnitState? unit, out int unitIndex))
        {
            return BattleTransitionResult.Reject(state, BattleTransitionRejection.UnknownUnit);
        }

        if (unit!.HasMoved)
        {
            return BattleTransitionResult.Reject(state, BattleTransitionRejection.UnitHasAlreadyMoved);
        }

        if (state.Units.Any(other => other.Id != unit.Id && other.Position == command.Destination))
        {
            return BattleTransitionResult.Reject(state, BattleTransitionRejection.DestinationOccupied);
        }

        IReadOnlySet<TilePosition> reachable = FlatGridMovementRange.Calculate(state.Map, unit.Position, unit.Move);
        if (!reachable.Contains(command.Destination))
        {
            return BattleTransitionResult.Reject(state, BattleTransitionRejection.DestinationNotReachable);
        }

        BattleState nextState = state.ReplaceUnit(unitIndex, unit.MoveTo(command.Destination));
        var movementEvent = new BattleMovementEvent(unit.Id, unit.Position, command.Destination);
        return BattleTransitionResult.Move(nextState, movementEvent);
    }
}
