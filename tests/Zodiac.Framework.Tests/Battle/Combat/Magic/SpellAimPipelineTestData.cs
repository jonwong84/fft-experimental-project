using System.Collections.Immutable;
using System.Text.Json;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

internal static class SpellAimPipelineTestData
{
    internal static (MagicReadyPlanResult Result, byte? Counter) Run(JsonElement d)
    {
        var mode = Enum.Parse<MagicFormulaEvaluationMode>(d.GetProperty("mode").GetString()!);
        var q = StoredSpellTestData.Request(mode); var rng = new RandomState(d.GetProperty("word").GetUInt32(), d.GetProperty("count").GetUInt64());
        (MagicReadyPlanResult, byte?) Reject() => (new(null, new(false, FaithMagicResolutionRejection.UnsupportedContext, null, rng)), null);
        var definition = q.Cast.Definition with { RawChargeTime = d.GetProperty("rawCt").GetByte(),
            Targeting = q.Cast.Definition.Targeting! with { Range = d.GetProperty("autoZero").GetBoolean() ? (byte)0 : (byte)1,
                Auto = d.GetProperty("autoZero").GetBoolean(), NoFollowTarget = d.GetProperty("noFollow").GetBoolean() } };
        var actor = q.Roster.Single(u => u.Occupant.UnitId == q.Cast.Caster.UnitId).Occupant;
        var choice = SpellTargetChoiceQuery.Evaluate(q.Map, actor, definition, q.GeometryProfile);
        if (!choice.Accepted) return Reject();
        var selected = q.Roster.Single(u => u.Occupant.UnitId.Value == "first").Occupant with { Position = new(1, 0, 0) };
        var confirmation = SpellAimCapture.Capture(choice.Outcome!, selected.Position, d.GetProperty("answer").GetByte(), selected, null, new(true, true, true));
        if (!confirmation.Accepted) return Reject();
        var start = q.Cast.Caster with { Mp = d.GetProperty("beginMp").GetUInt16(),
            Supports = new(ShortCharge: d.GetProperty("short").GetBoolean(), NonCharge: d.GetProperty("non").GetBoolean()) };
        var roster = q.Roster.Select(u => u.Occupant.UnitId.Value == "first" ? selected with { Active = d.GetProperty("beginAnchorActive").GetBoolean() } : u.Occupant).ToImmutableArray();
        var prepared = MagicChargePlanQuery.Prepare(new(confirmation.Outcome!, start, roster, new(true), new(true, true)));
        if (!prepared.Accepted) return Reject();
        var plan = prepared.Outcome!;
        for (int i = 0; i < d.GetProperty("ticks").GetInt32(); i++) plan = MagicChargePlanQuery.Tick(plan, d.GetProperty("tickBlocked").GetBoolean()).Plan;
        var current = q.Roster.Select(u =>
        {
            if (u.Occupant.UnitId == actor.UnitId) return u with { Current = q.Cast.Caster with {
                Mp = d.GetProperty("readyMp").GetUInt16(), MagicAttack = d.GetProperty("readyMa").GetByte(), Supports = new(HalfMp: d.GetProperty("readyHalf").GetBoolean()) } };
            if (u.Occupant.UnitId.Value == "first" && !d.GetProperty("readyAnchorActive").GetBoolean())
                return u with { Occupant = u.Occupant with { Active = false }, Current = null };
            if (u.Occupant.UnitId.Value == "second" && d.GetProperty("secondDead").GetBoolean()) return u with { Current = u.Current! with { Statuses = new(Dead: true) } };
            return u;
        }).ToImmutableArray();
        var ready = MagicCastTestData.Ready(mode);
        var result = MagicReadyPlanQuery.Resolve(new(plan, q.Map, current, ready.Weather, mode, ready.ResultProfile,
            ready.ReadyProfile! with { ChargeReadyKnown = false }, q.GeometryProfile, q.RosterProfile,
            new(true, d.GetProperty("knownAnchor").GetBoolean()), new(false, d.GetProperty("noEarlier").GetBoolean()),
            new(d.GetProperty("identityKnown").GetBoolean(), true, d.GetProperty("readyBlocked").GetBoolean())), rng);
        return (result, plan.Counter);
    }
}
