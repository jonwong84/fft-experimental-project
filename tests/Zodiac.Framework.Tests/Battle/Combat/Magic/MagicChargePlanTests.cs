using System.Collections.Immutable;
using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class MagicChargePlanTests
{
    internal static MagicChargePlanRequest Request(byte type = 6)
    {
        var q = StoredSpellTestData.Request();
        return new(SpellCastPhaseTestData.Confirm(type: type), q.Cast.Caster,
            q.Roster.Select(u => u.Occupant).ToImmutableArray(), new(true), new(true, true));
    }

    [Fact]
    public void Preparation_checks_active_identity_without_spending_mp_or_repeating_aim_geometry()
    {
        var q = Request(); var anchor = q.Roster.Single(u => u.UnitId.Value == "first");
        var p = MagicChargePlanQuery.Prepare(q with { Caster = q.Caster with { Mp = 0 } });
        Assert.True(p.Accepted); Assert.Equal((byte)9, p.Outcome!.Counter); Assert.Same(q.Selection.Definition, p.Outcome.Definition);
        Assert.Equal(q.Selection.Aim, p.Outcome.Aim); Assert.Equal((ushort)30, q.Caster.Mp);
        Assert.False(MagicChargePlanQuery.Prepare(q with { Roster = q.Roster.Replace(anchor, anchor with { Active = false }) }).Accepted);
        Assert.True(MagicChargePlanQuery.Prepare(q with { Roster = q.Roster.Replace(anchor, anchor with { Jump = true, Position = new(999, 999, 0) }) }).Accepted);
        foreach (byte type in new byte[] { 5, 7 })
        {
            var tile = Request(type); Assert.True(MagicChargePlanQuery.Prepare(tile with { Roster = tile.Roster.Remove(anchor) }).Accepted);
        }
        Assert.False(MagicChargePlanQuery.Prepare(q with { Profile = null }).Accepted);
        Assert.False(MagicChargePlanQuery.Prepare(q with { Profile = new(true, false) }).Accepted);
        Assert.False(MagicChargePlanQuery.Prepare(q with { Roster = q.Roster.Add(anchor) }).Accepted);
        var actor = q.Roster.Single(u => u.UnitId == q.Caster.UnitId);
        Assert.False(MagicChargePlanQuery.Prepare(q with { Roster = q.Roster.Replace(actor, actor with { Active = false }) }).Accepted);
        Assert.False(MagicChargePlanQuery.Prepare(q with { Roster = q.Roster.Remove(actor) }).Accepted);
        Assert.False(MagicChargePlanQuery.Prepare(q with { Caster = q.Caster with { IsMime = true } }).Accepted);
        Assert.False(MagicChargePlanQuery.Prepare(q with { Caster = q.Caster with { UnitId = new("other") } }).Accepted);
    }

    [Fact]
    public void Plans_reuse_literal_source_charge_setups_and_keep_current_supports_separate_from_mp()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "magic-resource-contract-v1.json")));
        var q = Request();
        foreach (var row in doc.RootElement.GetProperty("setup").EnumerateArray())
        {
            if (row.GetProperty("persevere").GetBoolean()) continue;
            var d = q.Selection.Definition with { RawChargeTime = row.GetProperty("raw").GetByte() };
            var selection = SpellCastPhaseTestData.Confirm(d);
            var result = MagicChargePlanQuery.Prepare(q with { Selection = selection, Caster = q.Caster with { Mp = 0,
                Supports = new(ShortCharge: row.GetProperty("short").GetBoolean(), NonCharge: row.GetProperty("non").GetBoolean()) } });
            Assert.True(result.Accepted); Assert.Equal(row.GetProperty("expected").GetByte(), result.Outcome!.Counter);
        }
        var performance = SpellCastPhaseTestData.Confirm(q.Selection.Definition with { Persevere = true });
        Assert.False(MagicChargePlanQuery.Prepare(q with { Selection = performance }).Accepted);
    }

    [Fact]
    public void One_counter_step_preserves_blocking_zero_readiness_and_immutable_plan_replay()
    {
        var plan = MagicChargePlanQuery.Prepare(Request()).Outcome!;
        var blocked = MagicChargePlanQuery.Tick(plan, true); Assert.Same(plan, blocked.Plan); Assert.False(blocked.Ready); Assert.False(blocked.Changed);
        var first = MagicChargePlanQuery.Tick(plan, false); var again = MagicChargePlanQuery.Tick(plan, false);
        Assert.Equal((byte)8, first.Plan.Counter); Assert.Equal(first.Plan.Counter, again.Plan.Counter); Assert.Equal((byte)9, plan.Counter);
        for (int i = 0; i < 9; i++) plan = MagicChargePlanQuery.Tick(plan, false).Plan;
        Assert.Equal((byte)0, plan.Counter); Assert.True(MagicChargePlanQuery.Tick(plan, false).Ready);
        Assert.False(MagicChargePlanQuery.Tick(plan, true).Ready); Assert.Same(first.Plan.Definition, plan.Definition);
    }
}
