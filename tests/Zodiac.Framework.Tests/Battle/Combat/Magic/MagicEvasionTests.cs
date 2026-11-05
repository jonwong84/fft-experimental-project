using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

internal static class MagicEvasionTestData
{
    internal static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Fixtures", "magic-evasion-contract-v1.json")));
    internal static MagicEvadeEquipmentInput Equipment(JsonElement d)
    {
        MagicEvadeItem Item(string key)
        {
            var s = d.GetProperty(key);
            return new(s.GetProperty("item").GetByte(), s.GetProperty("evade").GetByte(), s.GetProperty("qualifies").GetBoolean());
        }
        return new(d.GetProperty("monster").GetBoolean(), Item("accessory"), Item("right"), Item("left"),
            d.GetProperty("rightWeapon").GetByte(), d.GetProperty("leftWeapon").GetByte());
    }
    internal static MagicEvadeValues Values(JsonElement d) => new(d[0].GetByte(), d[1].GetByte(), d[2].GetByte());
    internal static MagicEvadeItems Items(JsonElement d) => new(d[0].GetByte(), d[1].GetByte(), d[2].GetByte());
    internal static MagicEvadeModifiers Modifiers(JsonElement d) => new(d.GetProperty("unrestricted").GetBoolean(),
        d.GetProperty("transparent").GetBoolean(), d.GetProperty("abandon").GetBoolean(), d.GetProperty("dontAct").GetBoolean(),
        d.GetProperty("sleep").GetBoolean(), d.GetProperty("stop").GetBoolean(), d.GetProperty("confusion").GetBoolean(),
        d.GetProperty("charging").GetBoolean(), d.GetProperty("performing").GetBoolean(), d.GetProperty("defending").GetBoolean());
}

[Trait("Ruleset", "OriginalFft")]
public sealed class MagicEvasionTests
{
    [Fact]
    public void Twenty_four_ordered_tapes_preserve_modes_labels_accuracy_and_atomic_counter_behavior()
    {
        using var doc = MagicEvasionTestData.Fixture();
        foreach (var row in doc.RootElement.GetProperty("rolls").EnumerateArray())
        {
            var prepared = MagicEvasion.Prepare(MagicEvasion.Load(MagicEvasionTestData.Equipment(row.GetProperty("equipment"))),
                MagicEvasionTestData.Modifiers(row.GetProperty("modifiers")));
            var before = new RandomState(row.GetProperty("word").GetUInt32(), row.GetProperty("count").GetUInt64());
            var r = MagicEvasion.Resolve(prepared, row.GetProperty("evadeable").GetBoolean(),
                Enum.Parse<MagicFormulaEvaluationMode>(row.GetProperty("mode").GetString()!), row.GetProperty("accuracy").GetUInt16(), before);
            var e = row.GetProperty("expected");
            Assert.Equal(e.GetProperty("accepted").GetBoolean(), r.Accepted);
            Assert.Equal(Enum.Parse<MagicEvasionRejection>(e.GetProperty("rejection").GetString()!), r.Rejection);
            Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(), e.GetProperty("count").GetUInt64()), r.RngState);
            if (!r.Accepted) { Assert.Null(r.Outcome); Assert.Equal(before, r.RngState); continue; }
            var o = e.GetProperty("outcome");
            Assert.Equal(new MagicEvasionOutcome(o.GetProperty("hit").GetBoolean(), o.GetProperty("accuracy").GetUInt16(),
                (MagicResultMissKind)o.GetProperty("miss").GetByte(), o.GetProperty("itemLost").GetByte(),
                o.GetProperty("abandon").GetBoolean()), r.Outcome);
        }
    }

    [Fact]
    [Trait("Contract", "QueryIsolation")]
    public void Evasion_replay_is_deterministic_and_does_not_change_prepared_work()
    {
        var work = MagicEvasion.Prepare(MagicEvasion.Load(new(false, new(10, 10, true), new(20, 20, true), new(30, 30, true))), new(true));
        var copy = work with { }; var before = new RandomState(57, 9);
        var first = MagicEvasion.Resolve(work, true, MagicFormulaEvaluationMode.Execute, 100, before);
        Assert.Equal(first, MagicEvasion.Resolve(work, true, MagicFormulaEvaluationMode.Execute, 100, before));
        Assert.Equal(copy, work); Assert.Equal(new RandomState(57, 9), before);
    }

    [Fact]
    public void Missing_work_and_unknown_modes_throw_even_when_checks_are_disabled()
    {
        Assert.Throws<ArgumentNullException>(() => MagicEvasion.Resolve(null!, false, MagicFormulaEvaluationMode.Execute, 100, new(1, 0)));
        var work = MagicEvasion.Prepare(MagicEvasion.Load(new(false, new(255, 0, false), new(255, 0, false), new(255, 0, false))), new(true));
        Assert.Throws<ArgumentOutOfRangeException>(() => MagicEvasion.Resolve(work, false, (MagicFormulaEvaluationMode)255, 100, new(1, 0)));
    }

    [Fact]
    public void Fifteen_literal_preparations_preserve_magic_order_base_hit_and_abandon_diagnostics()
    {
        using var doc = MagicEvasionTestData.Fixture();
        foreach (var row in doc.RootElement.GetProperty("preparations").EnumerateArray())
        {
            var contributions = MagicEvasion.Load(MagicEvasionTestData.Equipment(row.GetProperty("equipment")));
            var before = contributions with { };
            var r = MagicEvasion.Prepare(contributions, MagicEvasionTestData.Modifiers(row.GetProperty("modifiers")));
            var e = row.GetProperty("expected");
            Assert.Equal(e.GetProperty("base").GetByte(), r.BaseHit);
            Assert.Equal(MagicEvasionTestData.Values(e.GetProperty("evades")), r.Evades);
            Assert.Equal(e.GetProperty("abandon").GetBoolean(), r.AbandonTriggered);
            Assert.Equal(contributions.Items, r.Items); Assert.Equal(before, contributions);
            Assert.Equal((byte)0, r.Evades.Class);
        }
    }

    [Fact]
    public void Missing_preparation_inputs_throw()
    {
        Assert.Throws<ArgumentNullException>(() => MagicEvasion.Prepare(null!, new(true)));
        var empty = MagicEvasion.Load(new(false, new(255, 0, false), new(255, 0, false), new(255, 0, false)));
        Assert.Throws<ArgumentNullException>(() => MagicEvasion.Prepare(empty, null!));
    }

    [Fact]
    public void Eight_literal_equipment_descriptions_preserve_type_monster_and_item_label_rules()
    {
        using var doc = MagicEvasionTestData.Fixture();
        foreach (var row in doc.RootElement.GetProperty("loads").EnumerateArray())
        {
            var input = MagicEvasionTestData.Equipment(row.GetProperty("input"));
            var before = input with { }; var r = MagicEvasion.Load(input); var e = row.GetProperty("expected");
            Assert.Equal(MagicEvasionTestData.Values(e.GetProperty("evades")), r.Evades);
            Assert.Equal((byte)0, r.Evades.Class);
            Assert.Equal(MagicEvasionTestData.Items(e.GetProperty("items")), r.Items);
            Assert.Equal(before, input);
        }
    }

    [Fact]
    public void Missing_equipment_input_throws() => Assert.Throws<ArgumentNullException>(() => MagicEvasion.Load(null!));
}
