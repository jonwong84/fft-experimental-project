using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Authoritative immutable commands for the admitted ordinary spell encounter.</summary>
public static class OrdinaryMagicBattle
{
    internal static SpellTargetingProfile GeometryProfile => new(true, true, true, true);

    public static SpellTargetChoiceResult Choice(OrdinaryMagicBattleState state, BattleUnitId actorId, AssetId spellId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var actor = state.Units.FirstOrDefault(u => u.Id == actorId);
        if (actor is null || !state.Definition.Spells.TryGetValue(spellId, out var spell))
            return new(false, SpellTargetingRejection.UnsupportedContext, null);
        return SpellTargetChoiceQuery.Evaluate(state.Definition.Map, actor.Occupant, spell, GeometryProfile);
    }

    public static OrdinaryMagicBattleResult Apply(OrdinaryMagicBattleState state, OrdinaryMagicCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        OrdinaryMagicBattleResult Reject(string reason) => new(false, state, reason, []);
        if (state.Finished)
            return Reject("The encounter has ended.");
        if (state.Revision == ulong.MaxValue)
            return Reject("The command counter is exhausted.");
        var events = new List<OrdinaryMagicEvent>();
        try
        {
            OrdinaryMagicBattleState next;
            switch (command)
            {
                case OrdinaryMagicCommand.Advance:
                    if (state.Turns.ActiveUnitId is not null)
                        return Reject("Finish the active turn first.");
                    next = Advance(state, events);
                    break;
                case OrdinaryMagicCommand.Cast cast:
                    next = Queue(state, cast, events);
                    break;
                case OrdinaryMagicCommand.Wait wait:
                    next = Consume(state, wait.UnitId, TurnCommandKind.Wait, events);
                    break;
                case OrdinaryMagicCommand.Step step:
                    var actor = Active(state, step.UnitId);
                    if (actor.Definition.Move == 0 || step.Destination.Layer != 0 ||
                        Math.Abs(actor.Position.X - step.Destination.X) + Math.Abs(actor.Position.Y - step.Destination.Y) != 1 ||
                        !state.Definition.Map.TryGetTile(step.Destination, out _) || state.Units.Any(u => u.Position == step.Destination))
                        return Reject("Choose a free adjacent tile.");
                    var stepEvents = new List<OrdinaryMagicEvent>();
                    next = Consume(state, step.UnitId, TurnCommandKind.ConfirmMove, stepEvents);
                    var facing = step.Destination.X > actor.Position.X ? BattleFacing.East :
                        step.Destination.X < actor.Position.X ? BattleFacing.West :
                        step.Destination.Y > actor.Position.Y ? BattleFacing.North : BattleFacing.South;
                    next = next.With(units: next.Units.SetItem(Index(next, actor.Id), actor with
                        { Position = step.Destination, Facing = facing }));
                    events.Add(new OrdinaryMagicEvent.Stepped(actor.Id, step.Destination));
                    events.AddRange(stepEvents);
                    break;
                case OrdinaryMagicCommand.Move move:
                    next = Walk(state, move.UnitId, move.Destination, events);
                    break;
                default:
                    return Reject("Unsupported command.");
            }
            return new(true, next.With(revision: state.Revision + 1), null, events.ToImmutableArray());
        }
        catch (OrdinaryMagicExecutionException error)
        {
            return Reject(error.Message);
        }
        catch (OverflowException)
        {
            return Reject("A simulation counter is exhausted.");
        }
    }

    internal static int Index(OrdinaryMagicBattleState state, BattleUnitId id)
    {
        for (int i = 0; i < state.Units.Length; i++)
            if (state.Units[i].Id == id)
                return i;
        throw new OrdinaryMagicExecutionException("Unknown unit.");
    }

    private static OrdinaryMagicUnitState Active(OrdinaryMagicBattleState state, BattleUnitId id)
    {
        if (state.Turns.ActiveUnitId != id)
            throw new OrdinaryMagicExecutionException("That unit does not own the active turn.");
        return state.Units[Index(state, id)];
    }

    private static OrdinaryMagicBattleState Consume(OrdinaryMagicBattleState state, BattleUnitId id,
        TurnCommandKind kind, List<OrdinaryMagicEvent> events)
    {
        _ = Active(state, id);
        var result = TurnCycle.Apply(state.Turns, new(id, kind));
        if (!result.Accepted)
            throw new OrdinaryMagicExecutionException(kind == TurnCommandKind.ConfirmAct
                ? "The action has already been used." : "The movement has already been used.");
        if (result.State.ActiveUnitId is null)
            events.Add(new OrdinaryMagicEvent.TurnEnded(id));
        return state.With(turns: result.State, phase: result.State.ActiveUnitId is null
            ? OrdinaryMagicClockPhase.PickUnit : OrdinaryMagicClockPhase.UnitTurn);
    }

    private static OrdinaryMagicBattleState Walk(OrdinaryMagicBattleState state, BattleUnitId id, TilePosition destination,
        List<OrdinaryMagicEvent> events)
    {
        var actor = Active(state, id);
        var query = OrdinaryMagicMovement.Query(state, id, destination);
        if (!query.Accepted)
            throw new OrdinaryMagicExecutionException("Choose a free tile in the supported movement range.");
        var path = query.Outcome!.Path!;
        var turnEvents = new List<OrdinaryMagicEvent>();
        var next = Consume(state, id, TurnCommandKind.ConfirmMove, turnEvents);
        next = next.With(units: next.Units.SetItem(Index(next, id), actor with
            { Position = destination, Facing = path.FinalHeading }));
        events.Add(new OrdinaryMagicEvent.Moved(id, actor.Position, destination, actor.Facing, path));
        events.AddRange(turnEvents);
        // These are ordinary Black/White Magic casts, never the movement-canceled Charge skillset.
        return next;
    }

    private static OrdinaryMagicBattleState Queue(OrdinaryMagicBattleState state,
        OrdinaryMagicCommand.Cast command, List<OrdinaryMagicEvent> events)
    {
        var actor = Active(state, command.UnitId);
        if (!state.Definition.Spells.TryGetValue(command.SpellId, out var spell) ||
            !actor.Definition.LearnedSpells.Contains(command.SpellId))
            throw new OrdinaryMagicExecutionException("That spell is not learned.");
        if (state.Pending.ContainsKey(actor.Id))
            throw new OrdinaryMagicExecutionException("That unit is already charging a spell.");
        var resource = MagicResources.EvaluateMp(new(new(actor.Current.Mp, false, false, false, false, false, MagicActionRestriction.None),
            new(spell.OriginalAbilityId, spell.MpCost, MagicResourceMenu.Default, spell.AffectedBySilence),
            MagicFormulaEvaluationMode.Preview, new(true)));
        if (resource.Outcome is not { Usable: true })
            throw new OrdinaryMagicExecutionException("Not enough MP.");
        var choice = Choice(state, actor.Id, spell.Id);
        if (!choice.Accepted || choice.Outcome is null || command.FollowUnit && choice.Outcome.Mode != SpellTargetChoiceMode.UnitOrTile)
            throw new OrdinaryMagicExecutionException("That targeting choice is unavailable.");
        var occupant = state.Units.FirstOrDefault(u => u.Position == command.Aim)?.Occupant;
        var selection = SpellAimCapture.Capture(choice.Outcome, command.Aim, command.FollowUnit ? (byte)6 : (byte)5,
            occupant, null, new(true, true, true));
        if (!selection.Accepted)
            throw new OrdinaryMagicExecutionException("Choose a tile in spell range.");
        var plan = MagicChargePlanQuery.Prepare(new(selection.Outcome!, actor.Current,
            state.Units.Select(u => u.Occupant).ToImmutableArray(), new(true), new(true, true)));
        if (!plan.Accepted)
            throw new OrdinaryMagicExecutionException("The spell could not be prepared.");
        var next = Consume(state, actor.Id, TurnCommandKind.ConfirmAct, events);
        var pending = plan.Outcome!;
        actor = actor with { Current = actor.Current with { Statuses = new(Charging: pending.Counter != 0) } };
        next = next.With(units: next.Units.SetItem(Index(next, actor.Id), actor), pending: next.Pending.Add(actor.Id, pending));
        events.Add(new OrdinaryMagicEvent.Queued(actor.Id, spell.Id, pending.Counter));
        return pending.Counter == 0 ? OrdinaryMagicCastExecution.Resolve(next, actor.Id, events) : next;
    }

    private static OrdinaryMagicBattleState Advance(OrdinaryMagicBattleState state, List<OrdinaryMagicEvent> events)
    {
        var next = state;
        for (int guard = 0; guard < 2000; guard++)
        {
            switch (next.ClockPhase)
            {
                case OrdinaryMagicClockPhase.AddCt:
                    ulong tick = checked(next.Turns.Ticks + 1);
                    var added = next.Turns.Units.Select(u => u.Update(Math.Min(254, u.Ct + u.Speed), false,
                        u.MovementTaken, u.ActionTaken)).ToArray();
                    next = next.With(turns: new(added, next.Turns.RngState, tick, TurnCyclePhase.PickUnit),
                        phase: OrdinaryMagicClockPhase.PickUnit);
                    events.Add(new OrdinaryMagicEvent.Tick(tick));
                    break;
                case OrdinaryMagicClockPhase.PickUnit:
                    var selected = next.Turns.Units.Where(u => u.Ct >= 100)
                        .OrderByDescending(u => u.Ct).ThenBy(u => u.TieKey).FirstOrDefault();
                    if (selected is not null)
                    {
                        var draw = new RandomStateGenerator().Next(next.Turns.RngState);
                        var ready = next.Turns.Units.Select(u => u.Id == selected.Id
                            ? u.Update(u.Ct % 100, true, false, false) : u).ToArray();
                        next = next.With(turns: new(ready, draw.State, next.Turns.Ticks, TurnCyclePhase.UnitTurn),
                            phase: OrdinaryMagicClockPhase.UnitTurn);
                        events.Add(new OrdinaryMagicEvent.TurnReady(selected.Id));
                        return next;
                    }
                    next = next.With(phase: OrdinaryMagicClockPhase.TickCharges);
                    break;
                case OrdinaryMagicClockPhase.TickCharges:
                    var plans = next.Pending.ToBuilder();
                    foreach (var unit in next.Units)
                        if (plans.TryGetValue(unit.Id, out var plan))
                            plans[unit.Id] = MagicChargePlanQuery.Tick(plan, false).Plan;
                    next = next.With(pending: plans.ToImmutable(), phase: OrdinaryMagicClockPhase.ResolveCharges);
                    break;
                case OrdinaryMagicClockPhase.ResolveCharges:
                    var caster = next.Units.FirstOrDefault(u => next.Pending.TryGetValue(u.Id, out var p) && p.Counter == 0);
                    if (caster is not null)
                        return OrdinaryMagicCastExecution.Resolve(next, caster.Id, events);
                    // No timed status/terrain/Mime work is admitted in this encounter.
                    next = next.With(phase: OrdinaryMagicClockPhase.AddCt);
                    break;
                default:
                    throw new OrdinaryMagicExecutionException("Finish the active turn first.");
            }
        }
        throw new OrdinaryMagicExecutionException("No event became ready within the ordinary clock bound.");
    }
}

internal sealed class OrdinaryMagicExecutionException(string message) : Exception(message);
