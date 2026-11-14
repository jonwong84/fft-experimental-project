using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TestSupport;
using Zodiac.Content.BattleSetup;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class SharedElementSerializationTests
{
    // Frozen pre-refactor wire representation; this enum exists only to compare the original JSON contract.
    [Flags]
    private enum LegacyElements : byte
    {
        None = 0, Dark = 1, Holy = 2, Water = 4, Earth = 8,
        Wind = 16, Ice = 32, Lightning = 64, Fire = 128
    }
    private static readonly JsonSerializerOptions LegacyOptions = new()
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    [Fact]
    public void Every_original_element_set_retains_legacy_prepared_names_and_property_order()
    {
        var document = AuthoredDefaultAbility.Document();
        for (int value = 0; value < 256; value++)
        {
            var elements = OriginalElementEncoding.FromByte((byte)value);
            document.Spells[0] = document.Spells[0] with { Formula = document.Spells[0].Formula with { Elements = elements } };
            string json = PreparedDefaultAbilityBattle.Create(document).ToJson();
            using var parsed = JsonDocument.Parse(json);
            var actual = parsed.RootElement.GetProperty("spells")[0].GetProperty("formula").GetProperty("elements");
            Assert.Equal(JsonSerializer.Serialize((LegacyElements)value, LegacyOptions), actual.GetRawText());
            Assert.Equal(json, PreparedDefaultAbilityBattle.ReadJson(json).ToJson());
            if (value == 0)
            {
                Assert.Equal(new[] { "kind", "power", "elements", "secondaryEvadeable" },
                    parsed.RootElement.GetProperty("spells")[0].GetProperty("formula").EnumerateObject().Select(p => p.Name));
            }
        }
    }

    [Fact]
    public void Legacy_reading_preserves_case_combination_and_neutral_name_rules()
    {
        var package = PreparedDefaultAbilityBattle.Create(AuthoredDefaultAbility.Document());
        foreach (string names in new[] { "fire", "  Fire, Ice  ", "Fire, Fire", "None, Fire" })
        {
            var json = JsonNode.Parse(package.ToJson())!;
            json["spells"]![0]!["formula"]!["elements"] = names;
            var expected = JsonSerializer.Deserialize<LegacyElements>(JsonSerializer.Serialize(names), LegacyOptions);
            var actual = PreparedDefaultAbilityBattle.ReadJson(json.ToJsonString());
            Assert.Equal(OriginalElementEncoding.FromByte((byte)expected), actual.Definition.Spells.Values.Single().Spell.Elements);
        }
    }

    [Fact]
    public void Prepared_legacy_schema_rejects_custom_unknown_and_nonstring_elements()
    {
        var document = AuthoredDefaultAbility.Document();
        var package = PreparedDefaultAbilityBattle.Create(document);
        foreach (JsonNode? value in new JsonNode?[] { JsonValue.Create("Gravity"), JsonValue.Create("Fire, Poison"),
            JsonValue.Create("255"), JsonValue.Create(""), JsonValue.Create(128), new JsonObject { ["fire"] = true }, null })
        {
            var json = JsonNode.Parse(package.ToJson())!;
            json["spells"]![0]!["formula"]!["elements"] = value;
            Assert.Throws<InvalidDataException>(() => PreparedDefaultAbilityBattle.ReadJson(json.ToJsonString()));
        }
        document.Spells[0] = document.Spells[0] with { Formula = document.Spells[0].Formula with { Elements = new() { Restore = true } } };
        Assert.Throws<NotSupportedException>(() => PreparedDefaultAbilityBattle.Create(document));
    }
}
