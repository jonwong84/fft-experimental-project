using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>A confirmed voluntary walk; final facing is carried by Path, even when unchanged.</summary>
public sealed record VoluntaryPathCompletedEvent(BattleUnitId ActorId, FlatMovementPath Path,
    BattleFacing PreviousFacing) : AttackResultEvent;

/// <summary>Rejections retain the identical wrapper, empty events and no partial path.</summary>
public sealed record PathMoveResult(bool Accepted, FacingTurnRejection FacingRejection,
    TurnCycleRejection TurnRejection, FlatMovementRejection PathRejection, FacingTurnState State,
    ImmutableArray<AttackResultEvent> Events, FlatMovementPath? Path);

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
        var before = state.Battle.TurnBattle.Battle;
        before.TryGetUnit(command.UnitId, out var body);
        var actor = state.Battle.Combatants.Single(unit => unit.UnitId == command.UnitId);
        var bodies = before.Units.Select(unit => unit.Id == command.UnitId
            ? new BattleUnitState(unit.Id, command.Destination, unit.MovementProfile, hasMoved: true) : unit);
        var battle = new BattleState(before.Map, bodies, turn.State.RngState);
        var settled = new AttackBattleSnapshot(new(battle, turn.State), state.Battle.Combatants.Select(unit =>
            unit.UnitId == command.UnitId ? unit.WithResult(unit.Hp, path.FinalHeading, unit.KoCount) : unit));

        var events = ImmutableArray.CreateBuilder<AttackResultEvent>();
        events.Add(new AttackTurnBattleEvent(new TurnBattleMovementEvent(new(command.UnitId, body!.Position, command.Destination))));
        events.Add(new VoluntaryPathCompletedEvent(command.UnitId, path, actor.Facing));
        events.AddRange(turn.Events.Select(ev => (AttackResultEvent)new AttackTurnBattleEvent(new TurnBattleClockEvent(ev))));
        ulong revision = state.Revision + 1;
        var ended = turn.Events.OfType<TurnEndedEvent>().SingleOrDefault();
        // Authorization is minted only after both tile/path-facing and clock/choice settlement.
        var receipt = ended is null ? null : new FacingCompletionReceipt(ended.UnitId, revision, settled, state.Lineage);
        var next = new FacingTurnState(settled, state.Profile, revision, receipt, state.Lineage);
        return new(true, FacingTurnRejection.None, TurnCycleRejection.None, FlatMovementRejection.None, next, events.ToImmutable(), path);
    }
}
