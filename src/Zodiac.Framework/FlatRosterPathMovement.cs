using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record RosterPathMoveResult(bool Accepted, TurnCycleRejection TurnRejection,
    FlatMovementRejection PathRejection, AttackBattleSnapshot State, ImmutableArray<AttackResultEvent> Events, FlatMovementPath? Path);

/// <summary>Atomic confirmed roster walk, including actual final heading and choice/CT settlement.</summary>
public static class FlatRosterPathMovement
{
    public static RosterPathMoveResult Move(AttackBattleSnapshot state, MoveUnitCommand command, FlatRosterBattleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile);
        var turn = TurnCycle.Apply(state.TurnBattle.Turns, new(command.UnitId, TurnCommandKind.ConfirmMove));
        if (!turn.Accepted) return new(false, turn.Rejection, FlatMovementRejection.None, state, [], null);
        var query = FlatRosterMovement.Evaluate(state, command.UnitId, profile, command.Destination);
        if (!query.Accepted) return new(false, TurnCycleRejection.None, query.Rejection, state, [], null);
        var path = query.Outcome!.Path!;
        var settled = ConfirmedPathMovement.Settle(state, command, turn, path);
        return new(true, TurnCycleRejection.None, FlatMovementRejection.None, settled.State, settled.Events, path);
    }
}
