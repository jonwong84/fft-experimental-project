using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fft.Tests.Support;
using Zodiac.Content.Events;
using Zodiac.Framework;
using Zodiac.Framework.Events;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class PreparedReadyTests
{
    private static string Temp() => Path.Combine(Path.GetTempPath(), "fft-ready-content-" + Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData("RPR-004")]
    [InlineData("RPR-022")]
    [InlineData("RPR-023")]
    [InlineData("RPR-024")]
    public void Content_contract_cases_execute(string id)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/ready-presentation-contract-v1.json")));
        Assert.Single(doc.RootElement.GetProperty("cases").EnumerateArray(), c => c.GetProperty("id").GetString() == id);
        if (id == "RPR-004")
        {
            var d = AuthoredReady.Artwork();
            Assert.Throws<InvalidDataException>(() => (d with { Glyphs = [.. d.Glyphs.Select(g => g with { SourceIndex = 0 })] }).Validate());
            Assert.Throws<InvalidDataException>(() => (d with { Glyphs = [.. d.Glyphs.Select(g => g with { U = 240 })] }).Validate());
            var parsed = PreparedReadyArtwork.Parse(d.ToUtf8()); Assert.Equal(6, parsed.Glyphs.Length);
        }
        else if (id == "RPR-022")
        {
            var bytes = AuthoredReady.Artwork().ToUtf8();
            Assert.Throws<InvalidDataException>(() => PreparedReadyArtwork.Parse(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1,\"schemaVersion\": 1"))));
            var node = JsonNode.Parse(bytes)!; node["glyphs"]![0]!["colors"]![0]!["r"] = 1.5;
            Assert.Throws<InvalidDataException>(() => PreparedReadyArtwork.Parse(Encoding.UTF8.GetBytes(node.ToJsonString())));
            Assert.Equal(PreparedReadyArtwork.ReferenceRevision, PreparedReadyArtwork.Parse(bytes).Source.ReferenceRevision);
        }
        else if (id == "RPR-023")
        {
            string root = Temp(); AuthoredReady.WriteArtwork(root);
            var loaded = PreparedReadyArtwork.Load(LooseAssetStore.Load(root), AuthoredReady.ArtworkId);
            Assert.Equal((51200, 32), (loaded.Indices.Length, loaded.Palette.Length));
            File.AppendAllText(Path.Combine(root, "indices.bin"), "x");
            Assert.Throws<InvalidDataException>(() => PreparedReadyArtwork.Load(LooseAssetStore.Load(root), AuthoredReady.ArtworkId));
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => PreparedOrderedBannerEvent.Parse(AuthoredReady.Event().ToUtf8()));
            Assert.Throws<InvalidDataException>(() => PreparedScenarioEvent.Parse(AuthoredReady.Event().ToUtf8()));
            Assert.Throws<InvalidDataException>(() => PreparedGeneralBanner.Parse(AuthoredReady.Artwork().ToUtf8()));
            foreach (int condition in Enumerable.Range(1, 8))
                Assert.Throws<InvalidDataException>(() => ChangeDisplay(condition, 60));
        }
    }
    private static PreparedReadyEvent ChangeDisplay(int condition, int hold)
    {
        var d = AuthoredReady.Event();
        return new(d.Id, d.ArtworkId, d.OwnerId, d.Source, d.Instructions.Select(i => i.Operation == OrderedScenarioOperation.DisplayConditions ? i with { Operand1 = condition, Operand2 = hold } : i));
    }
    [Fact]
    public void RDY_029_strict_separate_profile_rejects_unsupported_domain_and_unreachable_records()
    {
        using var spec = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/ready-display-contract-v1.json")));
        Assert.Equal("RDY-029", spec.RootElement.GetProperty("cases")[28].GetProperty("id").GetString());
        var d = AuthoredReady.Event();
        foreach (var source in new[] { d.Source with { Kind = "retail" }, d.Source with { Encoding = "bogus" }, d.Source with { ReferenceRevision = new string('0', 40) } })
            Assert.Throws<InvalidDataException>(() => new PreparedReadyEvent(d.Id, d.ArtworkId, d.OwnerId, source, d.Instructions));
        Assert.Throws<InvalidDataException>(() => new PreparedReadyEvent(d.Id, d.ArtworkId, d.OwnerId, d.Source, d.Instructions, ScenarioEventScene.World));
        Assert.Throws<InvalidDataException>(() => new PreparedReadyEvent(d.Id, d.ArtworkId, d.OwnerId, d.Source, d.Instructions, tutorial: true));
        Assert.Throws<InvalidDataException>(() => new PreparedReadyEvent(d.Id, d.ArtworkId, d.OwnerId, d.Source, d.Instructions, speed: 2));
        Assert.Throws<InvalidDataException>(() => new PreparedReadyEvent(d.Id, d.ArtworkId, d.OwnerId, d.Source,
            [new(4, OrderedScenarioOperation.ForwardTargetExit, 0), new(6, OrderedScenarioOperation.DisplayConditions, 1, 60), new(9, OrderedScenarioOperation.SearchEnd)]));
    }
    [Theory]
    [InlineData("format")]
    [InlineData("version")]
    [InlineData("condition")]
    [InlineData("image")]
    [InlineData("paletteBank")]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("null-glyph")]
    [InlineData("null-color")]
    [InlineData("id")]
    [InlineData("ids-equal")]
    [InlineData("count")]
    [InlineData("source")]
    [InlineData("nested-unknown")]
    [InlineData("nested-duplicate")]
    [InlineData("large")]
    [InlineData("invalid-index")]
    [InlineData("short-palette")]
    public void Invalid_artwork_schema_and_payloads_fail_before_loading(string fault)
    {
        var d = AuthoredReady.Artwork(); var node = JsonNode.Parse(d.ToUtf8())!;
        switch (fault)
        {
            case "format": node["format"] = "fft.general-banner"; break;
            case "version": node["schemaVersion"] = 2; break;
            case "condition": node["condition"] = 8; break;
            case "image": node["image"] = 1; break;
            case "paletteBank": node["paletteBank"] = 4; break;
            case "missing": node.AsObject().Remove("width"); break;
            case "unknown": node["crop"] = 1; break;
            case "null-glyph": node["glyphs"]![0] = null; break;
            case "null-color": node["glyphs"]![0]!["colors"]![0] = null; break;
            case "id": node["id"] = "invalid"; break;
            case "ids-equal": node["paletteId"] = d.IndicesId; break;
            case "count": node["glyphs"]!.AsArray().RemoveAt(0); break;
            case "source": node["source"]!["kind"] = "unverified"; break;
            case "nested-unknown": node["glyphs"]![0]!["triplet"] = 0; break;
        }
        byte[] bytes = Encoding.UTF8.GetBytes(node.ToJsonString());
        if (fault == "nested-duplicate") bytes = Encoding.UTF8.GetBytes(node.ToJsonString().Replace("\"sourceIndex\":0", "\"sourceIndex\":0,\"sourceIndex\":0"));
        if (fault == "large") bytes = new byte[32769];
        if (fault is "invalid-index" or "short-palette")
        {
            string root = Temp();
            AuthoredReady.WriteArtwork(root, indices: fault == "invalid-index" ? Enumerable.Repeat((byte)16, 51200).ToArray() : null, palette: fault == "short-palette" ? new byte[31] : null);
            Assert.Throws<InvalidDataException>(() => PreparedReadyArtwork.Load(LooseAssetStore.Load(root), AuthoredReady.ArtworkId));
        }
        else Assert.Throws<InvalidDataException>(() => PreparedReadyArtwork.Parse(bytes));
    }
    [Theory]
    [InlineData("format")]
    [InlineData("profile")]
    [InlineData("scene")]
    [InlineData("tutorial")]
    [InlineData("speed")]
    [InlineData("duplicate")]
    [InlineData("null")]
    [InlineData("unknown")]
    [InlineData("fraction")]
    [InlineData("hold")]
    [InlineData("instruction")]
    public void Invalid_event_json_is_closed(string fault)
    {
        var node = JsonNode.Parse(AuthoredReady.Event().ToUtf8())!;
        switch (fault)
        {
            case "format": node["format"] = PreparedOrderedBannerEvent.Format; break;
            case "profile": node["profile"] = PreparedScenarioEvent.TaskBarrierProfile; break;
            case "scene": node["scene"] = "World"; break;
            case "tutorial": node["tutorial"] = true; break;
            case "speed": node["speed"] = 2; break;
            case "null": node["instructions"] = null; break;
            case "unknown": node["background"] = true; break;
            case "fraction": node["instructions"]![0]!["operand2"] = 1.5; break;
            case "hold": node["instructions"]![0]!["operand2"] = 256; break;
            case "instruction": node["instructions"]![0]!["operation"] = "EventEnd"; break;
        }
        string text = node.ToJsonString();
        if (fault == "duplicate") text = text.Replace("\"speed\":1", "\"speed\":1,\"speed\":1");
        Assert.Throws<InvalidDataException>(() => PreparedReadyEvent.Parse(Encoding.UTF8.GetBytes(text)));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(255)]
    public void Loaded_event_projects_to_exact_ready_timing(int hold)
    {
        string root = Temp(); AuthoredReady.WriteEvent(root, AuthoredReady.Event(hold));
        var d = PreparedReadyEvent.Load(LooseAssetStore.Load(root), AuthoredReady.Id);
        var result = ReadySceneExecutor.RunUntilBlocked(d.ToExecutionDefinition().Start(default, SceneTaskState.Create(new("sample:scene/ready")), new("sample:consumer/ready"),
            new(d.DefinitionId, d.ArtworkId, d.OwnerId)), 8192);
        Assert.True(result.Accepted, result.Diagnostic);
        var state = result.State;
        for (int tick = 0; tick <= hold + 10; tick++)
        {
            Assert.Equal(tick >= hold + 8, state.Ready!.ChildIsTerminal);
            Assert.Equal(tick >= hold + 9, state.Ready.IsTerminal);
            Assert.Equal(tick >= hold + 10, state.Event.Exited);
            if (tick == hold + 10) break;
            result = ReadySceneExecutor.AdvanceTick(state, false, [], 8192); Assert.True(result.Accepted, result.Diagnostic); state = result.State;
        }
    }
}
