using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Zodiac.Framework.Tests.Fixtures;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class AttackFixtureV3Tests
{
    private static readonly string Revision = typeof(BattleState).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[1][..40];
    private static string Load(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sword-" + name + ".v3.settled-facing.json"));

    [Theory]
    [InlineData("normal")] [InlineData("miss")] [InlineData("critical")] [InlineData("ko")]
    [InlineData("side")] [InlineData("zero-pa")] [InlineData("cap-ko-wrap")] [InlineData("diagonal-rejected")]
    [InlineData("move-attack")]
    public void Source_derived_v3_vectors_execute_and_repeat_with_complete_snapshots(string name)
    {
        var first = AttackFixtureRunner.Run(Load(name), Revision);
        Assert.Equal("passed-assertion", first.Status);
        Assert.Contains("V3", first.Diagnostic);
        Assert.Contains("weapons", first.ProvenanceJson);
        Assert.Equal("11464943bbdbd2a0af022d90cb7f10455cf911df", first.ReferenceRevision);
        Assert.Equal(first, AttackFixtureRunner.Run(Load(name), Revision));
    }

    [Theory]
    [InlineData("damage", "events[1].damage")]
    [InlineData("facing", "combatants[0].facing")]
    [InlineData("rng", "rngState.drawCount")]
    [InlineData("outcome", "attackOutcome")]
    [InlineData("rejection", "attackRejection")]
    public void Executed_attack_expectations_report_mismatches_including_outcome_and_rejection(string change, string path)
    {
        var root = JsonNode.Parse(Load(change == "rejection" ? "diagonal-rejected" : "normal"))!;
        var assertions = root["expectations"]![0]!["assertions"]!;
        switch (change)
        {
            case "damage": assertions[1]!["events"]![1]!["damage"] = 26; break;
            case "facing": assertions[1]!["state"]!["combatants"]![0]!["facing"] = "West"; break;
            case "rng": assertions[1]!["state"]!["rngState"]!["drawCount"] = 7; break;
            case "outcome": assertions[0]!["attackOutcome"] = "miss"; break;
            case "rejection": assertions[0]!["attackRejection"] = "UnsupportedSetup"; break;
        }
        var report = AttackFixtureRunner.Run(root.ToJsonString(), Revision);
        Assert.Equal("failed-assertion", report.Status);
        Assert.Contains(path, report.Diagnostic);
    }

    [Theory]
    [InlineData("missing-power")] [InlineData("power-overflow")] [InlineData("unknown-field")]
    [InlineData("duplicate-id")] [InlineData("missing-id")] [InlineData("duplicate-json-key")]
    public void V3_requires_explicit_unambiguous_supported_weapon_parameters(string change)
    {
        var root = JsonNode.Parse(Load("normal"))!;
        var row = root["scenario"]!["weapons"]![0]!.AsObject();
        switch (change)
        {
            case "missing-power": row.Remove("power"); break;
            case "power-overflow": row["power"] = 256; break;
            case "unknown-field": row["element"] = 1; break;
            case "duplicate-id": root["scenario"]!["weapons"]!.AsArray().Add(row.DeepClone()); break;
            case "missing-id": root["scenario"]!["weapons"]!.AsArray().Clear(); break;
        }
        string json = root.ToJsonString();
        if (change == "duplicate-json-key") json = json.Replace("\"schemaVersion\":3", "\"schemaVersion\":3,\"schemaVersion\":3", StringComparison.Ordinal);
        Assert.Equal("invalid-fixture", AttackFixtureRunner.Run(json, Revision).Status);
    }

    [Fact]
    public void Description_only_remains_unexecuted_even_with_explicit_v3_weapons()
    {
        var root = JsonNode.Parse(Load("normal"))!; root["execution"] = "description-only";
        Assert.Equal("not-executed", AttackFixtureRunner.Run(root.ToJsonString(), Revision).Status);
    }

    [Theory]
    [InlineData("normal")] [InlineData("miss")] [InlineData("critical")] [InlineData("ko")]
    [InlineData("side")] [InlineData("zero-pa")] [InlineData("cap-ko-wrap")] [InlineData("diagonal-rejected")]
    [InlineData("move-attack")]
    public void Historical_v3_is_hash_preserved_validated_and_explicitly_superseded(string name)
    {
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sword-facing-supersession-v1.json")))!;
        var entry = manifest["fixtures"]!.AsArray().Single(row => row!["name"]!.GetValue<string>() == name)!;
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sword-" + name + ".v3.source-derived.json");
        string json = File.ReadAllText(path);
        Assert.Equal(entry["historicalSha256"]!.GetValue<string>(), HistoricalHash(json));
        Assert.Equal(entry["historicalId"]!.GetValue<string>(), JsonNode.Parse(json)!["fixtureId"]!.GetValue<string>());
        Assert.Equal(entry["replacementId"]!.GetValue<string>(), JsonNode.Parse(Load(name))!["fixtureId"]!.GetValue<string>());
        var result = AttackFixtureRunner.Run(json, Revision);
        Assert.Equal("not-executed", result.Status); Assert.Contains("superseded", result.Diagnostic);
        Assert.Contains("settled-facing", result.Diagnostic);
        var malformed = JsonNode.Parse(json)!; malformed["scenario"]!["weapons"]![0]!["power"] = 256;
        Assert.Equal("invalid-fixture", AttackFixtureRunner.Run(malformed.ToJsonString(), Revision).Status);
    }

    [Theory] [InlineData("unknown")] [InlineData("legacy-schema")]
    public void Policy_must_be_explicit_and_compatible_with_schema(string kind)
    {
        var root = JsonNode.Parse(Load("normal"))!;
        if (kind == "unknown") root["target"]!["policyId"] = "fft:combat/unknown";
        else root["schemaVersion"] = 2;
        Assert.Equal("invalid-fixture", AttackFixtureRunner.Run(root.ToJsonString(), Revision).Status);
    }

    [Fact]
    public void Historical_identity_hash_is_stable_under_Git_checkout_line_endings()
    {
        string lf = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sword-normal.v3.source-derived.json"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(HistoricalHash(lf), HistoricalHash(lf.Replace("\n", "\r\n", StringComparison.Ordinal)));
    }
    private static string HistoricalHash(string json) => Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes(json.Replace("\r\n", "\n", StringComparison.Ordinal))));
}
