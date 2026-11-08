using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Copied ordinary scheduling facts after the hypothetical ACT is marked, before turn_end.</summary>
public sealed record OrdinarySpellAiClockUnit(BattleUnitId UnitId, int SourceOrder, int Speed, int Ct,
    bool Dead = false, bool MovementTaken = false, bool ActionTaken = false);
public enum OrdinarySpellAiClockEventKind { UnitReady, AbilityReady }
public sealed record OrdinarySpellAiClockEvent(OrdinarySpellAiClockEventKind Kind, BattleUnitId UnitId, ulong Tick);
public enum OrdinarySpellAiClockRejection { None, UnsupportedContext, RandomDrawCountExhausted, TickCountExhausted }
public sealed record OrdinarySpellAiClockOutcome(ImmutableArray<OrdinarySpellAiClockUnit> Units,
    ImmutableHashSet<BattleUnitId> HadMoveOpportunity, ulong Ticks, ulong ElapsedTicks, byte RemainingCharge,
    ImmutableArray<OrdinarySpellAiClockEvent> Events, ImmutableArray<AiUpkeepPick> UpkeepPicks, bool EventLimitReached);
public sealed record OrdinarySpellAiClockResult(bool Accepted, OrdinarySpellAiClockRejection Rejection,
    OrdinarySpellAiClockOutcome? Outcome, RandomState RngState);

/// <summary>Effect-free source phases for one new ordinary charge, or one event after an immediate hit.</summary>
public static class OrdinarySpellAiClock
{
    private enum Phase { PickUnit, TickCharge, AddCt }

    public static OrdinarySpellAiClockResult Run(ImmutableArray<OrdinarySpellAiClockUnit> input, BattleUnitId actorId,
        byte chargeCounter, ulong initialTicks, RandomState entryRng)
        => RunCore(input, actorId, chargeCounter, initialTicks, entryRng, moveOnly: false);

    /// <summary>One effect-free event after turn_end, retaining the unused-ACT refund.</summary>
    internal static OrdinarySpellAiClockResult RunMoveOnly(ImmutableArray<OrdinarySpellAiClockUnit> input,
        BattleUnitId actorId, ulong initialTicks, RandomState entryRng)
        => RunCore(input, actorId, 0, initialTicks, entryRng, moveOnly: true);

    private static OrdinarySpellAiClockResult RunCore(ImmutableArray<OrdinarySpellAiClockUnit> input, BattleUnitId actorId,
        byte chargeCounter, ulong initialTicks, RandomState entryRng, bool moveOnly)
    {
        OrdinarySpellAiClockResult Reject(OrdinarySpellAiClockRejection reason) => new(false, reason, null, entryRng);
        if (input.IsDefaultOrEmpty || input.Length > 21 || chargeCounter > 127 ||
            input.Any(u => u is null || string.IsNullOrWhiteSpace(u.UnitId.Value) || u.SourceOrder is < 0 or > 20 ||
                u.Speed is < 1 or > 50 || u.Ct is < 0 or > 254) ||
            input.Select(u => u.UnitId).Distinct().Count() != input.Length ||
            input.Select(u => u.SourceOrder).Distinct().Count() != input.Length)
            return Reject(OrdinarySpellAiClockRejection.UnsupportedContext);
        var units = input.OrderBy(u => u.SourceOrder).ToArray();
        int actorIndex = Array.FindIndex(units, u => u.UnitId == actorId);
        if (actorIndex < 0 || units[actorIndex].Dead || units[actorIndex].Ct >= 100 ||
            !moveOnly && !units[actorIndex].ActionTaken)
            return Reject(OrdinarySpellAiClockRejection.UnsupportedContext);
        var actor = units[actorIndex];
        units[actorIndex] = actor with { Ct = Math.Min(60, actor.Ct + (actor.MovementTaken ? 0 : 20) +
            (actor.ActionTaken ? 0 : 20)) };
        ulong ticks = initialTicks;
        var rng = entryRng;
        byte remaining = chargeCounter;
        bool charged = chargeCounter != 0;
        var phase = Phase.PickUnit;
        var opportunities = ImmutableHashSet.CreateBuilder<BattleUnitId>();
        var events = ImmutableArray.CreateBuilder<OrdinarySpellAiClockEvent>();
        var upkeep = ImmutableArray.CreateBuilder<AiUpkeepPick>();
        OrdinarySpellAiClockResult Complete(bool limit) => new(true, OrdinarySpellAiClockRejection.None,
            new(units.ToImmutableArray(), opportunities.ToImmutable(), ticks, ticks - initialTicks, remaining,
                events.ToImmutable(), upkeep.ToImmutable(), limit), rng);
        try
        {
            // Source increments before checking256, so there are at most255 clock calls/events.
            for (int call = 1; call < 256; call++)
            {
                bool returned = false;
                while (!returned)
                {
                    switch (phase)
                    {
                        case Phase.PickUnit:
                            int best = -1;
                            for (int i = 0; i < units.Length; i++)
                                if (units[i].Ct >= 100 && (best < 0 || units[i].Ct > units[best].Ct)) best = i;
                            if (best < 0) { phase = Phase.TickCharge; break; }
                            var selected = units[best];
                            int remainder = selected.Ct % 100;
                            var draw = new RandomStateGenerator().Next(rng); rng = draw.State;
                            upkeep.Add(new(selected.UnitId, selected.Ct, remainder, selected.Dead, draw.Value));
                            units[best] = selected with { Ct = remainder };
                            if (selected.Dead) continue; // AI upkeep does not decay a plain corpse's counter.
                            units[best] = units[best] with { MovementTaken = false, ActionTaken = false };
                            opportunities.Add(selected.UnitId);
                            events.Add(new(OrdinarySpellAiClockEventKind.UnitReady, selected.UnitId, ticks));
                            // The source directly clears has_turn. Never issue Wait/refund here.
                            returned = true;
                            break;
                        case Phase.TickCharge:
                            if (charged && remaining > 0)
                            {
                                remaining = MagicCharge.Tick(remaining, false).Counter;
                                if (remaining == 0)
                                {
                                    events.Add(new(OrdinarySpellAiClockEventKind.AbilityReady, actorId, ticks));
                                    return Complete(false);
                                }
                            }
                            phase = Phase.AddCt;
                            break;
                        case Phase.AddCt:
                            if (ticks == ulong.MaxValue) return Reject(OrdinarySpellAiClockRejection.TickCountExhausted);
                            ticks++;
                            for (int i = 0; i < units.Length; i++) units[i] = units[i] with { Ct = Math.Min(254, units[i].Ct + units[i].Speed) };
                            phase = Phase.PickUnit;
                            break;
                    }
                }
                if (!charged) return Complete(false); // Even an immediate action runs one event.
            }
            return Complete(true);
        }
        catch (OverflowException) { return Reject(OrdinarySpellAiClockRejection.RandomDrawCountExhausted); }
    }
}
