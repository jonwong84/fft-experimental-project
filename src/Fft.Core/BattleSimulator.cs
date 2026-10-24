namespace Fft.Core;

/// <summary>Applies the initial, deliberately limited battle command set.</summary>
/// <remarks>
/// The two-argument overload retains <see cref="FlatGridMovementRange"/> for compatibility.
/// The explicit overload can select bounded terrain-cost or adjacent-step prototypes. This transition demonstrates
/// immutable state and command handling; it is not a source-faithful implementation of FFT movement.
/// </remarks>
public static class BattleSimulator
{
    public static BattleTransitionResult Apply(BattleState state, MoveUnitCommand command) =>
        Apply(state, command, BattleMovementPolicy.FlatGridPrototype);

    public static BattleTransitionResult Apply(BattleState state, MoveUnitCommand command, BattleMovementPolicy movementPolicy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (!Enum.IsDefined(movementPolicy))
            throw new ArgumentOutOfRangeException(nameof(movementPolicy));

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

        IReadOnlySet<TilePosition> reachable = movementPolicy switch
        {
            BattleMovementPolicy.FlatGridPrototype => FlatGridMovementRange.Calculate(state.Map, unit.Position, unit.Move),
            // Conservative synthetic policy: other units block traversal as well as stopping.
            // Original allegiance, mount and standing-height rules remain unsupported.
            BattleMovementPolicy.TerrainCostPrototype => TerrainAwareMovementRange.Calculate(
                state.Map, unit.Position, unit.MovementProfile,
                state.Units.Where(other => other.Id != unit.Id).Select(other => other.Position).ToHashSet()),
            BattleMovementPolicy.OrdinaryAdjacentStepPrototype => OrdinaryAdjacentStepRange.Calculate(
                state.Map, unit.Position, unit.MovementProfile,
                state.Units.Where(other => other.Id != unit.Id).Select(other => other.Position).ToHashSet()),
            _ => throw new ArgumentOutOfRangeException(nameof(movementPolicy)),
        };
        if (!reachable.Contains(command.Destination))
        {
            return BattleTransitionResult.Reject(state, BattleTransitionRejection.DestinationNotReachable);
        }

        BattleState nextState = state.ReplaceUnit(unitIndex, unit.MoveTo(command.Destination));
        var movementEvent = new BattleMovementEvent(unit.Id, unit.Position, command.Destination);
        return BattleTransitionResult.Move(nextState, movementEvent);
    }
}
