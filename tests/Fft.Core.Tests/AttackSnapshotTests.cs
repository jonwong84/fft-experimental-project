using System.Reflection;
using System.Text.Json.Nodes;
using Fft.Core;
using Fft.Core.Tests.Fixtures;
using Xunit;

namespace Fft.Core.Tests;

public sealed class AttackSnapshotTests
{
    private static readonly string Revision = typeof(BattleState).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[1][..40];
    private static string Load(string name = "combat-turns.v2.synthetic.json") => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void V2_replay_compares_full_snapshots_and_is_repeatable()
    {
        var first = AttackFixtureRunner.Run(Load(), Revision);
        Assert.Equal("passed-assertion", first.Status);
        Assert.Equal(first, AttackFixtureRunner.Run(Load(), Revision));
        Assert.Contains("No attack", first.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("evidence", first.ProvenanceJson);
        Assert.Equal("611b504d25d4a9b03fab8023d7dbea75fe381d5d", first.ReferenceRevision);
    }

    [Theory]
    [InlineData("hp", "combatants[0].hp")]
    [InlineData("facing", "combatants[0].facing")]
    [InlineData("pa", "combatants[0].physicalAttack")]
    [InlineData("weapon", "combatants[0].rightHandWeaponId")]
    [InlineData("count", "rngState.drawCount")]
    [InlineData("word", "rngState.value")]
    [InlineData("order", "events[0]")]
    public void Changed_expectations_identify_first_field(string change, string path)
    {
        JsonNode root = JsonNode.Parse(Load())!;
        var assertion = root["expectations"]![0]!["assertions"]![1]!;
        var state = assertion["state"]!;
        switch (change)
        {
            case "hp": state["combatants"]![0]!["hp"] = 49; break;
            case "facing": state["combatants"]![0]!["facing"] = "North"; break;
            case "pa": state["combatants"]![0]!["physicalAttack"] = 6; break;
            case "weapon": state["combatants"]![0]!["rightHandWeaponId"] = null; break;
            case "count": state["rngState"]!["drawCount"] = 1; break;
            case "word": state["rngState"]!["value"] = uint.MaxValue; break;
            case "order":
                var events = assertion["events"]!.AsArray();
                JsonNode first = events[0]!.DeepClone(); events[0] = events[1]!.DeepClone(); events[1] = first; break;
        }
        var result = AttackFixtureRunner.Run(root.ToJsonString(), Revision);
        Assert.Equal("failed-assertion", result.Status);
        Assert.Contains(path, result.Diagnostic);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("missing")]
    [InlineData("fraction")]
    [InlineData("negative-count")]
    [InlineData("overflow-word")]
    [InlineData("algorithm")]
    [InlineData("weapon")]
    [InlineData("status")]
    [InlineData("brave")]
    [InlineData("modifier")]
    [InlineData("water")]
    [InlineData("late")]
    public void Invalid_or_unsupported_fixture_fails_before_execution(string change)
    {
        var root = JsonNode.Parse(Load())!;
        var state = root["scenario"]!["initialState"]!;
        switch (change)
        {
            case "unknown": state["combatants"]![0]!["mystery"] = true; break;
            case "missing": state["combatants"]![0]!.AsObject().Remove("physicalAttack"); break;
            case "fraction": state["combatants"]![0]!["hp"] = 1.5; break;
            case "negative-count": state["rngState"]!["drawCount"] = -1; break;
            case "overflow-word": state["rngState"]!["value"] = 4294967296UL; break;
            case "algorithm": state["rngState"]!["algorithmId"] = "sample:rng/unknown"; break;
            case "weapon": state["combatants"]![0]!["rightHandWeaponId"] = "sample:item/missing"; break;
            case "status": state["combatants"]![0]!["status"] = "Dead"; break;
            case "brave": state["combatants"]![0]!["brave"] = 9; break;
            case "modifier": state["combatants"]![0]!["rightHandEvade"] = 1; break;
            case "water": state["map"]!["tiles"]![0]!["waterDepth"] = 1; break;
            case "late": root["expectations"]![2]!["assertions"]![1]!["state"]!["combatants"]![1]!["facing"] = "Up"; break;
        }
        Assert.Equal("invalid-fixture", AttackFixtureRunner.Run(root.ToJsonString(), Revision).Status);
    }

    [Fact]
    public void Attack_KO_contract_validates_without_claiming_execution_and_v1_remains_separate()
    {
        var json = Load("sword-ko.v2.source-description.json");
        Assert.Equal("not-executed", AttackFixtureRunner.Run(json, Revision).Status);
        JsonNode root = JsonNode.Parse(json)!; root["execution"] = "scenario";
        Assert.Equal("not-executed", AttackFixtureRunner.Run(root.ToJsonString(), Revision).Status);
        Assert.Equal("invalid-fixture", MovementFixtureRunner.Run(json, Revision).Status);
        Assert.Equal("passed-assertion", MovementFixtureRunner.Run(Load("flat-move.synthetic.json"), Revision).Status);
        Assert.Equal("invalid-fixture", AttackFixtureRunner.Run(Load("flat-move.synthetic.json"), Revision).Status);
    }

    [Fact]
    public void Duplicate_keys_versions_and_unresolved_evidence_are_invalid()
    {
        Assert.Equal("invalid-fixture", AttackFixtureRunner.Run(Load().Replace("\"schemaVersion\": 2", "\"schemaVersion\": 2,\"schemaVersion\":2", StringComparison.Ordinal), Revision).Status);
        var root = JsonNode.Parse(Load())!; root["schemaVersion"] = 4;
        Assert.Equal("invalid-fixture", AttackFixtureRunner.Run(root.ToJsonString(), Revision).Status);
        root = JsonNode.Parse(Load())!; root["expectations"]![0]!["assertions"]![0]!["evidenceIds"] = new JsonArray("absent");
        Assert.Equal("invalid-fixture", AttackFixtureRunner.Run(root.ToJsonString(), Revision).Status);
    }

    [Fact]
    public void Full_width_unsigned_state_is_valid_contract_data_without_precision_loss()
    {
        var root = JsonNode.Parse(Load())!; root["execution"] = "description-only";
        root["scenario"]!["initialState"]!["rngState"]!["value"] = uint.MaxValue;
        root["scenario"]!["initialState"]!["rngState"]!["drawCount"] = ulong.MaxValue;
        root["scenario"]!["initialState"]!["turns"]!["ticks"] = ulong.MaxValue;
        Assert.Equal("not-executed", AttackFixtureRunner.Run(root.ToJsonString(), Revision).Status);
    }

    [Theory]
    [InlineData(0, 50, AttackUnitStatus.Dead)]
    [InlineData(10, 50, AttackUnitStatus.Critical)]
    [InlineData(11, 50, AttackUnitStatus.None)]
    [InlineData(65535, 65535, AttackUnitStatus.None)]
    public void Representable_vitals_preserve_boundaries_and_explicit_KO_count(int hp, int maxHp, AttackUnitStatus status)
    {
        var unit = new AttackUnitState(new("sample:unit/a"), hp, maxHp, 0, 0, 5, 10, 0, 0, 0, 70, 0, BattleFacing.West, status, 255, null);
        Assert.Equal(hp, unit.Hp); Assert.Equal(status, unit.Status); Assert.Equal(255, unit.KoCount);
    }
}
