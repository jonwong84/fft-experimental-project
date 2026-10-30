namespace Zodiac.Framework;

/// <summary>The bounded FFT-owned ordinary CT clock and confirmed turn-choice transitions.</summary>
/// <remarks>
/// Source: turn_order.c at decomp 611b504d25d4a9b03fab8023d7dbea75fe381d5d;
/// docs/behavior/ct-turn-cycle.md records the source-derived arithmetic and exclusions.
/// No statuses, charged actions, mounts, presence changes or combat effects are modeled.
/// </remarks>
public static class TurnCycle
{
    public static TurnCycleResult Advance(TurnCycleState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.ActiveUnitId is not null) return TurnCycleResult.Reject(state, TurnCycleRejection.TurnAlreadyActive);
        if (state.RngState.DrawCount == ulong.MaxValue)
            return TurnCycleResult.Reject(state, TurnCycleRejection.RandomDrawCountExhausted);

        TurnUnitState[] units = state.Units.ToArray();
        ulong ticks = state.Ticks;
        bool addCt = state.Phase == TurnCyclePhase.AddCt;
        var events = new List<TurnCycleEvent>();
        // Positive byte-range speeds guarantee readiness within at most 100 ticks.
        while (true)
        {
            if (addCt)
            {
                if (ticks == ulong.MaxValue) return TurnCycleResult.Reject(state, TurnCycleRejection.TickCountExhausted);
                ticks++;
                for (int i = 0; i < units.Length; i++)
                {
                    TurnUnitState unit = units[i];
                    units[i] = unit.Update(Math.Min(254, unit.Ct + unit.Speed), false, unit.MovementTaken, unit.ActionTaken);
                }
                events.Add(new TurnClockAdvancedEvent(ticks));
            }

            int best = -1;
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i].Ct < 100) continue;
                if (best == -1 || units[i].Ct > units[best].Ct ||
                    (units[i].Ct == units[best].Ct && units[i].TieKey < units[best].TieKey)) best = i;
            }
            if (best >= 0)
            {
                TurnUnitState selected = units[best];
                int remainder = selected.Ct % 100;
                // The source calls upkeep after CT modulo, even with no visible status effect.
                var draw = new FftBiosRandom().Next(state.RngState);
                units[best] = selected.Update(remainder, true, false, false);
                events.Add(new TurnReadyEvent(selected.Id, selected.Ct, remainder, draw.Value));
                return TurnCycleResult.Accept(new TurnCycleState(units, draw.State, ticks, TurnCyclePhase.UnitTurn), events);
            }
            addCt = true;
        }
    }

    public static TurnCycleResult Apply(TurnCycleState state, TurnCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (!Enum.IsDefined(command.Kind)) return TurnCycleResult.Reject(state, TurnCycleRejection.UnsupportedCommand);
        int index = -1;
        for (int i = 0; i < state.Units.Length; i++)
            if (state.Units[i].Id == command.UnitId) { index = i; break; }
        if (index == -1) return TurnCycleResult.Reject(state, TurnCycleRejection.UnknownUnit);
        TurnUnitState unit = state.Units[index];
        if (!unit.HasTurn) return TurnCycleResult.Reject(state, TurnCycleRejection.InactiveUnit);
        if (command.Kind == TurnCommandKind.ConfirmMove && unit.MovementTaken)
            return TurnCycleResult.Reject(state, TurnCycleRejection.MovementAlreadyTaken);
        if (command.Kind == TurnCommandKind.ConfirmAct && unit.ActionTaken)
            return TurnCycleResult.Reject(state, TurnCycleRejection.ActionAlreadyTaken);

        bool moved = unit.MovementTaken || command.Kind == TurnCommandKind.ConfirmMove;
        bool acted = unit.ActionTaken || command.Kind == TurnCommandKind.ConfirmAct;
        bool complete = command.Kind == TurnCommandKind.Wait || (moved && acted);
        var events = new List<TurnCycleEvent>();
        if (command.Kind != TurnCommandKind.Wait) events.Add(new TurnChoiceConsumedEvent(unit.Id, command.Kind));
        int ct = unit.Ct;
        if (complete)
        {
            ct = Math.Min(60, ct + (moved ? 0 : 20) + (acted ? 0 : 20));
            events.Add(new TurnEndedEvent(unit.Id, unit.Ct, ct));
        }
        TurnUnitState[] units = state.Units.ToArray();
        units[index] = unit.Update(ct, !complete, moved, acted);
        return TurnCycleResult.Accept(new TurnCycleState(units, state.RngState, state.Ticks,
            complete ? TurnCyclePhase.PickUnit : TurnCyclePhase.UnitTurn), events);
    }
}
