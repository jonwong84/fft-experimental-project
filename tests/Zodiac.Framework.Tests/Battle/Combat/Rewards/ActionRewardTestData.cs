using System.Text.Json;

namespace Zodiac.Framework.Tests;

internal static class ActionRewardTestData
{
    internal static JsonDocument Fixture()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(root!.FullName,
            "docs/fixtures/behavior/action-rewards-contract-v1.json")));
    }
}
