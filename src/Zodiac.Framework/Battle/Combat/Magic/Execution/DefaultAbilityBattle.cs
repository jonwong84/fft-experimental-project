using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Authoritative immutable commands for the admitted ordinary spell encounter.</summary>
public static class DefaultAbilityBattle
{
    internal static SpellTargetingProfile GeometryProfile => new(true, true, true, true);

    public static ImmutableHashSet<AssetId> KnownSpells(DefaultAbilityBattleState state, BattleUnitId unitId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var unit = state.Units[Index(state, unitId)];
        var primary = state.Definition.Learning?.PrimarySlots(unit.Definition.RewardJobId) ?? [];
        var row = unit.LearnedAbilitySlots[unit.Definition.RewardJobId];
        return unit.Definition.LearnedSpells.Union(primary.Where(s => row.Contains(s.Index)).Select(s => s.AbilityId));
    }

    public static SpellTargetChoiceResult Choice(DefaultAbilityBattleState state, BattleUnitId actorId, AssetId spellId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var actor = state.Units.FirstOrDefault(u => u.Id == actorId);
        if (actor is null || !state.Definition.Spells.TryGetValue(spellId, out var spell))
            return new(false, SpellTargetingRejection.UnsupportedContext, null);
        return SpellTargetChoiceQuery.Evaluate(state.Definition.Map, actor.Occupant, spell, GeometryProfile);
    }

    public static DefaultAbilityBattleResult Apply(DefaultAbilityBattleState state, DefaultAbilityCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        DefaultAbilityBattleResult Reject(string reason) => new(false, state, reason, []);
        if (state.Finished)
            return Reject("The encounter has ended.");
        if (state.Revision == ulong.MaxValue)
            return Reject("The command counter is exhausted.");
        if (state.LearningReport is not null && command is not DefaultAbilityCommand.AnswerLearning)
            return Reject("Answer the learning report first.");
        var events = new List<DefaultAbilityEvent>();
        try
        {
            DefaultAbilityBattleState next;
            switch (command)
            {
                case DefaultAbilityCommand.AnswerLearning answer:
                    next = DefaultAbilityLearningExecution.Answer(state, answer, events);
                    break;
                case DefaultAbilityCommand.Advance:
                    if (state.Turns.ActiveUnitId is not null)
                        return Reject("Finish the active turn first.");
                    next = Advance(state, events);
                    break;
                case DefaultAbilityCommand.Cast cast:
                    next = DefaultAbilityCastPreparation.PrepareAndQueue(state, cast, events);
                    break;
                case DefaultAbilityCommand.Wait wait:
                    next = Consume(state, wait.UnitId, TurnCommandKind.Wait, events);
                    break;
                case DefaultAbilityCommand.Step step:
                    var actor = Active(state, step.UnitId);
                    if (actor.Definition.Move == 0 || step.Destination.Layer != 0 ||
                        Math.Abs(actor.Position.X - step.Destination.X) + Math.Abs(actor.Position.Y - step.Destination.Y) != 1 ||
                        !state.Definition.Map.TryGetTile(step.Destination, out _) || state.Units.Any(u => u.Position == step.Destination))
                        return Reject("Choose a free adjacent tile.");
                    var stepEvents = new List<DefaultAbilityEvent>();
                    next = Consume(state, step.UnitId, TurnCommandKind.ConfirmMove, stepEvents);
                    var facing = step.Destination.X > actor.Position.X ? BattleFacing.East :
                        step.Destination.X < actor.Position.X ? BattleFacing.West :
                        step.Destination.Y > actor.Position.Y ? BattleFacing.North : BattleFacing.South;
                    next = next.With(units: next.Units.SetItem(Index(next, actor.Id), actor with
                        { Position = step.Destination, Facing = facing }));
                    events.Add(new DefaultAbilityEvent.Stepped(actor.Id, step.Destination));
                    events.AddRange(stepEvents);
                    break;
                case DefaultAbilityCommand.Move move:
                    next = Walk(state, move.UnitId, move.Destination, events);
                    break;
                default:
                    return Reject("Unsupported command.");
            }
            return new(true, next.With(revision: state.Revision + 1), null, events.ToImmutableArray());
        }
        catch (DefaultAbilityExecutionException error)
        {
            return Reject(error.Message);
        }
        catch (OverflowException)
        {
            return Reject("A simulation counter is exhausted.");
        }
    }

    internal static int Index(DefaultAbilityBattleState state, BattleUnitId id)
    {
        for (int i = 0; i < state.Units.Length; i++)
            if (state.Units[i].Id == id)
                return i;
        throw new DefaultAbilityExecutionException("Unknown unit.");
    }

    internal static DefaultAbilityUnitState Active(DefaultAbilityBattleState state, BattleUnitId id)
    {
        if (state.Turns.ActiveUnitId != id)
            throw new DefaultAbilityExecutionException("That unit does not own the active turn.");
        return state.Units[Index(state, id)];
    }

    internal static DefaultAbilityBattleState Consume(DefaultAbilityBattleState state, BattleUnitId id,
        TurnCommandKind kind, List<DefaultAbilityEvent> events)
    {
        _ = Active(state, id);
        var result = TurnCycle.Apply(state.Turns, new(id, kind));
        if (!result.Accepted)
            throw new DefaultAbilityExecutionException(kind == TurnCommandKind.ConfirmAct
                ? "The action has already been used." : "The movement has already been used.");
        if (result.State.ActiveUnitId is null)
            events.Add(new DefaultAbilityEvent.TurnEnded(id));
        return state.With(turns: result.State, phase: result.State.ActiveUnitId is null
            ? DefaultAbilityClockPhase.PickUnit : DefaultAbilityClockPhase.UnitTurn);
    }

    private static DefaultAbilityBattleState Walk(DefaultAbilityBattleState state, BattleUnitId id, TilePosition destination,
        List<DefaultAbilityEvent> events)
    {
        var actor = Active(state, id);
        var query = DefaultAbilityMovement.Query(state, id, destination);
        if (!query.Accepted)
            throw new DefaultAbilityExecutionException("Choose a free tile in the supported movement range.");
        var path = query.Outcome!.Path!;
        var turnEvents = new List<DefaultAbilityEvent>();
        var next = Consume(state, id, TurnCommandKind.ConfirmMove, turnEvents);
        next = next.With(units: next.Units.SetItem(Index(next, id), actor with
            { Position = destination, Facing = path.FinalHeading }));
        events.Add(new DefaultAbilityEvent.Moved(id, actor.Position, destination, actor.Facing, path));
        events.AddRange(turnEvents);
        // These are ordinary Black/White Magic casts, never the movement-canceled Charge skillset.
        return next;
    }

    private static DefaultAbilityBattleState Advance(DefaultAbilityBattleState state, List<DefaultAbilityEvent> events)
    {
        var next = state;
        for (int guard = 0; guard < 2000; guard++)
        {
            switch (next.ClockPhase)
            {
                case DefaultAbilityClockPhase.AddCt:
                    ulong tick = checked(next.Turns.Ticks + 1);
                    var added = next.Turns.Units.Select(u => u.Update(Math.Min(254, u.Ct + u.Speed), false,
                        u.MovementTaken, u.ActionTaken)).ToArray();
                    next = next.With(turns: new(added, next.Turns.RngState, tick, TurnCyclePhase.PickUnit),
                        phase: DefaultAbilityClockPhase.PickUnit);
                    events.Add(new DefaultAbilityEvent.Tick(tick));
                    break;
                case DefaultAbilityClockPhase.PickUnit:
                    var selected = next.Turns.Units.Where(u => u.Ct >= 100)
                        .OrderByDescending(u => u.Ct).ThenBy(u => u.TieKey).FirstOrDefault();
                    if (selected is not null)
                    {
                        var draw = new RandomStateGenerator().Next(next.Turns.RngState);
                        var ready = next.Turns.Units.Select(u => u.Id == selected.Id
                            ? u.Update(u.Ct % 100, true, false, false) : u).ToArray();
                        next = next.With(turns: new(ready, draw.State, next.Turns.Ticks, TurnCyclePhase.UnitTurn),
                            phase: DefaultAbilityClockPhase.UnitTurn);
                        events.Add(new DefaultAbilityEvent.TurnReady(selected.Id));
                        return next;
                    }
                    next = next.With(phase: DefaultAbilityClockPhase.TickCharges);
                    break;
                case DefaultAbilityClockPhase.TickCharges:
                    var plans = next.Pending.ToBuilder();
                    foreach (var unit in next.Units)
                        if (plans.TryGetValue(unit.Id, out var plan))
                            plans[unit.Id] = MagicChargePlanQuery.Tick(plan, false).Plan;
                    next = next.With(pending: plans.ToImmutable(), phase: DefaultAbilityClockPhase.ResolveCharges);
                    break;
                case DefaultAbilityClockPhase.ResolveCharges:
                    var caster = next.Units.FirstOrDefault(u => next.Pending.TryGetValue(u.Id, out var p) && p.Counter == 0);
                    if (caster is not null)
                        return DefaultAbilityCastExecution.Resolve(next, caster.Id, events);
                    // No timed status/terrain/Mime work is admitted in this encounter.
                    next = next.With(phase: DefaultAbilityClockPhase.AddCt);
                    break;
                default:
                    throw new DefaultAbilityExecutionException("Finish the active turn first.");
            }
        }
        throw new DefaultAbilityExecutionException("No event became ready within the ordinary clock bound.");
    }
}

internal sealed class DefaultAbilityExecutionException(string message) : Exception(message);
