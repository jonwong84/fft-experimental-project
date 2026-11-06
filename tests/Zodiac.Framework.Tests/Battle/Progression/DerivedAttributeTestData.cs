using System.Text.Json;

namespace Zodiac.Framework.Tests;

internal static class DerivedAttributeTestData
{
    internal static JsonDocument Fixture()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(root!.FullName, "docs/fixtures/behavior/derived-attributes-contract-v1.json")));
    }
    internal static BattleStatMultipliers Multipliers(JsonElement d)
    { var a = d.GetProperty("multipliers"); return new(a[0].GetByte(), a[1].GetByte(), a[2].GetByte(), a[3].GetByte(), a[4].GetByte()); }
    internal static BattleAttributeValues Triple(JsonElement a) => new(a[0].GetByte(), a[1].GetByte(), a[2].GetByte());
    internal static Mode1AttributeProjection Expected(JsonElement e) => new(e.GetProperty("hp").GetUInt16(), e.GetProperty("maxHp").GetUInt16(),
        e.GetProperty("mp").GetUInt16(), e.GetProperty("maxMp").GetUInt16(), Triple(e.GetProperty("base")));
}
