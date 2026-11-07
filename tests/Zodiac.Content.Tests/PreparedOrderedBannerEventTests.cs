using System.Text;
using System.Text.Json.Nodes;
using Fft.Tests.Support;
using Zodiac.Content.Events;
using Zodiac.Framework;
using Zodiac.Framework.Events;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class PreparedOrderedBannerEventTests
{
    [Theory]
    [InlineData(0, 31, 32)]
    [InlineData(1, 32, 33)]
    [InlineData(60, 91, 92)]
    [InlineData(255, 286, 287)]
    public void Separate_format_round_trips_and_projects_exact_authored_ordered_behavior(int hold, int clear, int resume)
    {
        var definition = AuthoredOrderedBannerEvent.Definition(hold);
        byte[] frozen = definition.ToUtf8();
        var loaded = PreparedOrderedBannerEvent.Parse(frozen);
        Assert.Equal(frozen, loaded.ToUtf8());
        Assert.Equal(new[] { 4, 7, 10, 13, 18, 20 }, loaded.Instructions.Select(i => i.Offset));
        var program = loaded.ToExecutionDefinition();
        var result = OrderedBannerExecutor.RunUntilBlocked(program.Start(default, SceneTaskState.Create(new("sample:scene/a")), new("sample:consumer/a"),
            new(loaded.DefinitionId, loaded.ArtworkId, loaded.OwnerId)), 3);
        Assert.True(result.Accepted, result.Diagnostic);
        Assert.Equal(3, result.InstructionsExecuted);
        var state = result.State;
        Assert.Equal(10, state.Event.WaitingAtOffset);
        Assert.Equal(13, state.Event.Offset);
        Assert.Equal(hold, state.Banner!.Definition.HoldFrames);
        Assert.Equal(0, state.Banner.RevealCompleted);
        for (int tick = 1; tick <= resume; tick++)
        {
            result = OrderedBannerExecutor.AdvanceTick(state, false, [], 2);
            Assert.True(result.Accepted, result.Diagnostic);
            state = result.State;
            Assert.Equal(tick >= clear, state.Banner!.IsTerminal);
            Assert.Equal(tick >= resume, state.Event.Exited);
            if (tick == clear) Assert.Equal(SceneBarrierStatus.Pending, state.Event.Tasks.BarrierStatus);
        }
        Assert.Equal(7, state.Event.Variables.V3);
        Assert.Equal(frozen, definition.ToUtf8());
        Assert.Throws<InvalidDataException>(() => PreparedScenarioEvent.Parse(frozen));
    }

    [Theory]
    [InlineData("format")][InlineData("schema")][InlineData("profile")][InlineData("dialect")]
    [InlineData("world")][InlineData("tutorial")][InlineData("speed")][InlineData("provenance")]
    [InlineData("edition")][InlineData("revision")][InlineData("encoding")][InlineData("hash")]
    [InlineData("missing")][InlineData("unknown")][InlineData("null")][InlineData("empty")]
    [InlineData("condition")][InlineData("negative-hold")][InlineData("large-hold")][InlineData("packed")]
    [InlineData("second-display")][InlineData("no-display")][InlineData("target")][InlineData("offset")]
    [InlineData("operation")][InlineData("numeric-op")][InlineData("reachable-end")][InlineData("missing-sentinel")]
    [InlineData("boundary")][InlineData("artwork")][InlineData("owner")]
    public void Strict_prepared_schema_rejects_unsupported_or_corrupt_domains(string fault)
    {
        var json = JsonNode.Parse(AuthoredOrderedBannerEvent.Definition().ToUtf8())!.AsObject();
        var records = json["instructions"]!.AsArray();
        switch (fault)
        {
            case "format": json["format"] = PreparedScenarioEvent.Format; break;
            case "schema": json["schemaVersion"] = 2; break;
            case "profile": json["profile"] = PreparedScenarioEvent.TaskBarrierProfile; break;
            case "dialect": json["dialect"] = "WORLD"; break;
            case "world": json["scene"] = "World"; break;
            case "tutorial": json["tutorial"] = true; break;
            case "speed": json["speed"] = 2; break;
            case "provenance": json["source"]!["kind"] = "verified-retail"; break;
            case "edition": json["source"]!["edition"] = "PAL"; break;
            case "revision": json["source"]!["referenceRevision"] = new string('0', 40); break;
            case "encoding": json["source"]!["encoding"] = "retail"; break;
            case "hash": json["source"]!["blockSha256"] = "bad"; break;
            case "missing": json.Remove("tutorial"); break;
            case "unknown": records[1]!["ready"] = true; break;
            case "null": json["instructions"] = null; break;
            case "empty": json["instructions"] = new JsonArray(); break;
            case "condition": records[1]!["operand1"] = 0; break;
            case "negative-hold": records[1]!["operand2"] = -1; break;
            case "large-hold": records[1]!["operand2"] = 256; break;
            case "packed": records[1]!["operand1"] = 0x3c08; records[1]!["operand2"] = null; break;
            case "second-display": records[2]!["operation"] = "DisplayConditions"; records[2]!["operand1"] = 8; records[2]!["operand2"] = 1; break;
            case "no-display": records[1]!["operation"] = "WaitFrames"; records[1]!["operand1"] = 0; records[1]!["operand2"] = null; break;
            case "target": records[1]!["targetOffset"] = 18; break;
            case "offset": records[1]!["offset"] = 8; break;
            case "operation": records[4]!["operation"] = "EventEnd"; break;
            case "numeric-op": records[1]!["operation"] = "12"; break;
            case "reachable-end": records[4]!["operation"] = "ForwardTarget"; break;
            case "missing-sentinel": records.RemoveAt(records.Count - 1); break;
            case "boundary": json["source"]!["textOffset"] = 20; break;
            case "artwork": json["artworkId"] = json["id"]!.GetValue<string>(); break;
            case "owner": json["ownerId"] = ""; break;
        }
        Assert.Throws<InvalidDataException>(() => PreparedOrderedBannerEvent.Parse(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Fact]
    public void Duplicate_fields_and_oversized_input_are_rejected()
    {
        string json = Encoding.UTF8.GetString(AuthoredOrderedBannerEvent.Definition().ToUtf8());
        Assert.Throws<InvalidDataException>(() => PreparedOrderedBannerEvent.Parse(Encoding.UTF8.GetBytes(json.Replace("\"speed\":1", "\"speed\":1,\"speed\":1"))));
        Assert.Throws<InvalidDataException>(() => PreparedOrderedBannerEvent.Parse(new byte[PreparedScenarioEvent.MaxJsonBytes + 1]));
    }

    [Theory]
    [InlineData(PreparedScenarioEvent.Profile)]
    [InlineData(PreparedScenarioEvent.TimedProfile)]
    [InlineData(PreparedScenarioEvent.TaskBarrierProfile)]
    public void Legacy_JSON_contracts_still_reject_display_regardless_of_claimed_profile(string profile)
    {
        var node = JsonNode.Parse(AuthoredOrderedBannerEvent.Definition().ToUtf8())!.AsObject();
        foreach (var key in new[] { "artworkId", "ownerId", "tutorial", "speed" }) node.Remove(key);
        node["format"] = PreparedScenarioEvent.Format; node["profile"] = profile;
        Assert.Throws<InvalidDataException>(() => PreparedScenarioEvent.Parse(Encoding.UTF8.GetBytes(node.ToJsonString())));
    }
}
