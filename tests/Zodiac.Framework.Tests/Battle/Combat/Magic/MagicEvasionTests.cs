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
}

[Trait("Ruleset", "OriginalFft")]
public sealed class MagicEvasionTests
{
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
