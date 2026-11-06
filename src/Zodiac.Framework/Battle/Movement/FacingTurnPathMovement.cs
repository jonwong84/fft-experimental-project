using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Additive confirmed-path movement over the existing facing state and receipt lifecycle.</summary>
/// <remarks>
/// Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-122/123.
/// The explicit profile also certifies no unrepresented post-move benefits, charge cancellation,
/// traps, pickups or scripted/map effects. This is not AI preview placement or a tentative UI move.
/// Existing FacingTurnBattle APIs and source-history baselines are unchanged.
/// </remarks>
public static class FacingTurnPathMovement
{
    public static PathMoveResult MoveAlongPath(FacingTurnState state, MoveUnitCommand command,
        FlatOrdinaryMovementProfile profile)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(profile);
        PathMoveResult Reject(FacingTurnRejection facing = FacingTurnRejection.None,
            TurnCycleRejection turn = TurnCycleRejection.None, FlatMovementRejection path = FlatMovementRejection.None) =>
            new(false, facing, turn, path, state, [], null);
        if (state.PendingFacing is not null) return Reject(facing: FacingTurnRejection.FacingPending);
        if (state.Revision == ulong.MaxValue) return Reject(facing: FacingTurnRejection.RevisionExhausted);

        // Stage choice/CT validation privately; nothing is published if current geometry fails.
        var turn = TurnCycle.Apply(state.Battle.TurnBattle.Turns, new(command.UnitId, TurnCommandKind.ConfirmMove));
        if (!turn.Accepted) return Reject(turn: turn.Rejection);
        var query = FlatOrdinaryMovement.Evaluate(state.Battle, command.UnitId, profile, command.Destination);
        if (!query.Accepted) return Reject(path: query.Rejection);
        var path = query.Outcome!.Path!;
        var settled = ConfirmedPathMovement.Settle(state.Battle, command, turn, path);
        ulong revision = state.Revision + 1;
        var ended = turn.Events.OfType<TurnEndedEvent>().SingleOrDefault();
        // Authorization is minted only after both tile/path-facing and clock/choice settlement.
        var receipt = ended is null ? null : new FacingCompletionReceipt(ended.UnitId, revision, settled.State, state.Lineage);
        var next = new FacingTurnState(settled.State, state.Profile, revision, receipt, state.Lineage);
        return new(true, FacingTurnRejection.None, TurnCycleRejection.None, FlatMovementRejection.None, next, settled.Events, path);
    }
}
