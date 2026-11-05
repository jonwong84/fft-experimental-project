using System.Text.Json;
using Xunit;

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
