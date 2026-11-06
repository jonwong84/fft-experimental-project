using System.Text.Json;

namespace Zodiac.Framework.Tests;

internal static class MagicPublicationTestData
{
    internal static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Fixtures", "magic-publication-contract-v1.json")));
    internal static MagicVitalInput Vital(JsonElement d) => new(d.GetProperty("hp").GetUInt16(), d.GetProperty("maxHp").GetUInt16(),
        d.GetProperty("mp").GetUInt16(), d.GetProperty("maxMp").GetUInt16(), d.GetProperty("hit").GetBoolean(),
        d.GetProperty("damage").GetInt16(), d.GetProperty("healing").GetInt16(), d.GetProperty("mpDamage").GetInt16(), d.GetProperty("mpHealing").GetInt16());
}
