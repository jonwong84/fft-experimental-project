using System.Text.Json;

namespace Zodiac.Framework.Tests;

internal static class GrowthUnlockTestData
{
    internal static JsonDocument Fixture()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(root!.FullName, "docs/fixtures/behavior/growth-unlocks-contract-v1.json")));
    }

    internal static BattleRawStats Raw(JsonElement a) => new(a[0].GetUInt32(), a[1].GetUInt32(), a[2].GetUInt32(), a[3].GetUInt32(), a[4].GetUInt32());
}
