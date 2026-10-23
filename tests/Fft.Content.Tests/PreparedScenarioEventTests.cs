using System.Text;
using System.Text.Json.Nodes;
using Fft.Content.Events;
using Xunit;

namespace Fft.Content.Tests;

public sealed class PreparedScenarioEventTests
{
    [Fact]
    public void Independent_fixture_has_source_offsets_first_target_landings_and_terminal_search_sentinel()
    {
        var definition = PreparedScenarioEvent.Parse(Fixture());
        Assert.Equal("sample:event/conditional-outcome-v1", definition.Id.Value);
        Assert.Equal(ScenarioEventScene.Battle, definition.Scene);
        Assert.Equal(4, definition.EntryOffset); Assert.Equal(48, definition.EndOffset);
        Assert.Equal(48, definition.Source.TextOffset);
        Assert.Equal(15, definition.Instructions.Length);
        Assert.Equal(35, definition.Instructions.Single(r => r.Offset == 21).TargetOffset);
        Assert.Equal(45, definition.Instructions.Single(r => r.Offset == 31).TargetOffset);
        Assert.Equal(ScenarioOperation.ForwardTargetExit, definition.Instructions[^2].Operation);
        Assert.Equal(ScenarioOperation.SearchEnd, definition.Instructions[^1].Operation);
        Assert.Equal(definition.ToUtf8(), PreparedScenarioEvent.Parse(definition.ToUtf8()).ToUtf8());
    }

    [Theory]
    [InlineData("format")] [InlineData("version")] [InlineData("profile")] [InlineData("dialect")]
    [InlineData("scene")] [InlineData("source")] [InlineData("source-kind")] [InlineData("source-hash")]
    [InlineData("source-boundary")] [InlineData("unknown-field")] [InlineData("missing-field")]
    [InlineData("null-instruction")] [InlineData("unknown-operation")] [InlineData("numeric-operation")]
    [InlineData("offset-gap")] [InlineData("offset-overlap")] [InlineData("wrong-target")]
    [InlineData("missing-target")] [InlineData("variable")] [InlineData("negative-immediate")]
    [InlineData("large-immediate")] [InlineData("extra-operand")] [InlineData("extra-target")]
    [InlineData("no-sentinel")] [InlineData("reachable-end")] [InlineData("null-array")]
    public void Invalid_prepared_programs_fail_before_a_definition_is_returned(string problem)
    {
        var root = JsonNode.Parse(Fixture())!.AsObject();
        var records = root["instructions"]!.AsArray();
        switch (problem)
        {
            case "format": root["format"] = "other"; break;
            case "version": root["schemaVersion"] = 2; break;
            case "profile": root["profile"] = "other:profile/event"; break;
            case "dialect": root["dialect"] = "fft.wotl:event"; break;
            case "scene": root["scene"] = "Anything"; break;
            case "source": root["source"] = null; break;
            case "source-kind": root["source"]!["kind"] = "original"; break;
            case "source-hash": root["source"]!["blockSha256"] = "bad"; break;
            case "source-boundary": root["source"]!["textOffset"] = 47; break;
            case "unknown-field": root["executeRaw"] = true; break;
            case "missing-field": records[0]!.AsObject().Remove("operand2"); break;
            case "null-instruction": records[0] = null; break;
            case "unknown-operation": records[0]!["operation"] = "EventEnd"; break;
            case "numeric-operation": records[0]!["operation"] = "1"; break;
            case "offset-gap": records[0]!["offset"] = 5; break;
            case "offset-overlap": records[1]!["offset"] = 4; break;
            case "wrong-target": records[5]!["targetOffset"] = 45; break;
            case "missing-target": records[5]!["operand1"] = 99; break;
            case "variable": records[0]!["operand1"] = 0x21; break;
            case "negative-immediate": records[3]!["operand2"] = -1; break;
            case "large-immediate": records[3]!["operand2"] = 65536; break;
            case "extra-operand": records[4]!["operand1"] = 1; break;
            case "extra-target": records[0]!["targetOffset"] = 45; break;
            case "no-sentinel": records.RemoveAt(records.Count - 1); break;
            case "reachable-end": records[^2]!["operation"] = "ForwardTarget"; break;
            case "null-array": root["instructions"] = null; break;
        }
        Assert.Throws<InvalidDataException>(() => PreparedScenarioEvent.Parse(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    [Theory]
    [InlineData("duplicate")] [InlineData("nested-duplicate")] [InlineData("truncated")] [InlineData("bytes")] [InlineData("depth")]
    public void Strict_json_and_resource_budgets_are_enforced(string problem)
    {
        string json = Encoding.UTF8.GetString(Fixture());
        byte[] input = problem switch
        {
            "duplicate" => Encoding.UTF8.GetBytes(json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1")),
            "nested-duplicate" => Encoding.UTF8.GetBytes(json.Replace("\"offset\": 4", "\"offset\": 4, \"offset\": 4")),
            "truncated" => Encoding.UTF8.GetBytes(json[..^8]),
            "depth" => Encoding.UTF8.GetBytes(new string('[', 20) + "0" + new string(']', 20)),
            _ => new byte[PreparedScenarioEvent.MaxJsonBytes + 1],
        };
        Assert.Throws<InvalidDataException>(() => PreparedScenarioEvent.Parse(input));
    }

    [Fact]
    public void Constructor_snapshots_callers_and_accepts_explicit_World_scene_without_scene_execution()
    {
        var basis = PreparedScenarioEvent.Parse(Fixture()); var records = basis.Instructions.ToList();
        var world = new PreparedScenarioEvent(basis.Id, ScenarioEventScene.World, basis.Source, records);
        records.Clear(); Assert.Equal(15, world.Instructions.Length);
        Assert.Equal(ScenarioEventScene.World, PreparedScenarioEvent.Parse(world.ToUtf8()).Scene);
    }

    private static byte[] Fixture() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Events", "conditional-event-v1.json"));
}
