using System.Collections.Immutable;

namespace Zodiac.Framework;

internal sealed record ConfirmedPathOutcome(AttackBattleSnapshot State, ImmutableArray<AttackResultEvent> Events);

/// <summary>Shared settlement after private choice and path admission; never consumes RNG or issues a receipt itself.</summary>
internal static class ConfirmedPathMovement
{
    internal static ConfirmedPathOutcome Settle(AttackBattleSnapshot state, MoveUnitCommand command,
        TurnCycleResult turn, FlatMovementPath path)
    {
        var before = state.TurnBattle.Battle;
        var body = before.Units.Single(u => u.Id == command.UnitId);
        var actor = state.Combatants.Single(u => u.UnitId == command.UnitId);
        var bodies = before.Units.Select(unit => unit.Id == command.UnitId
            ? new BattleUnitState(unit.Id, command.Destination, unit.MovementProfile, hasMoved: true) : unit);
        var battle = new BattleState(before.Map, bodies, turn.State.RngState);
        var settled = new AttackBattleSnapshot(new(battle, turn.State), state.Combatants.Select(unit =>
            unit.UnitId == command.UnitId ? unit.WithResult(unit.Hp, path.FinalHeading, unit.KoCount) : unit));
        var events = ImmutableArray.CreateBuilder<AttackResultEvent>();
        events.Add(new AttackTurnBattleEvent(new TurnBattleMovementEvent(new(command.UnitId, body.Position, command.Destination))));
        events.Add(new VoluntaryPathCompletedEvent(command.UnitId, path, actor.Facing));
        events.AddRange(turn.Events.Select(ev => (AttackResultEvent)new AttackTurnBattleEvent(new TurnBattleClockEvent(ev))));
        return new(settled, events.ToImmutable());
    }
}
