using System.Text;
using System.Text.Json.Nodes;
using Fft.Tests.Support;
using Zodiac.Content.Events;
using Zodiac.Framework;
using Zodiac.Framework.Events;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class PreparedDarkScreenTests
{
    private static string Temp() => Path.Combine(Path.GetTempPath(), "fft-dark-content-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public void Authored_roundtrip_loads_immutable_data_and_preserves_worker_timing()
    {
        string root = AuthoredDarkScreen.Write(Temp());
        var definition = PreparedDarkScreen.Load(LooseAssetStore.Load(root), AuthoredDarkScreen.Id);
        var program = definition.ToExecutionDefinition();
        var result = DarkScreenExecutor.RunUntilBlocked(program.StartScene(default, SceneTaskState.Create(new("sample:scene/test")),
            new("sample:consumer/test"), new(new(definition.Id), new(definition.OwnerId))), 8);
        Assert.True(result.Accepted);
        var state = result.State;
        while (!state.Event.Exited) { result = DarkScreenExecutor.AdvanceTick(state, [], 8); Assert.True(result.Accepted, result.Diagnostic); state = result.State; }
        Assert.Equal(157, state.Event.Tick); Assert.True(state.Worker!.IsTerminal);
        Assert.Equal(288, state.Worker.Tiles.Length);
    }
    [Theory]
    [InlineData("format")] [InlineData("schemaVersion")] [InlineData("executionProfile")] [InlineData("id")]
    [InlineData("ownerId")] [InlineData("holdFrames")] [InlineData("parameters")] [InlineData("tiles")]
    [InlineData("rotations")] [InlineData("source")]
    public void Missing_required_fields_reject(string name)
    {
        var node = JsonNode.Parse(AuthoredDarkScreen.Definition().ToUtf8())!.AsObject(); node.Remove(name);
        Assert.Throws<InvalidDataException>(() => PreparedDarkScreen.Parse(Encoding.UTF8.GetBytes(node.ToJsonString())));
    }
    [Theory]
    [InlineData("duplicate")] [InlineData("extra")] [InlineData("missing-parameter")] [InlineData("fraction")]
    [InlineData("null-tile")] [InlineData("bad-axes")] [InlineData("wrong-provenance")] [InlineData("null")]
    [InlineData("missing-delay")] [InlineData("missing-axis")]
    public void Invalid_or_noncanonical_metadata_rejects(string kind)
    {
        var text = Encoding.UTF8.GetString(AuthoredDarkScreen.Definition().ToUtf8()); var node = JsonNode.Parse(text)!;
        switch (kind)
        {
            case "duplicate": text = text.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1,\"schemaVersion\": 1"); break;
            case "extra": node["unknown"] = true; break;
            case "missing-parameter": node["parameters"]!.AsObject().Remove("reserved"); break;
            case "missing-delay": node["tiles"]![0]!.AsObject().Remove("initialDelay"); break;
            case "missing-axis": node["rotations"]![0]!.AsObject().Remove("ax"); break;
            case "fraction": node["tiles"]![0]!["initialDelay"] = 0.5; break;
            case "null-tile": node["tiles"]![0] = null; break;
            case "bad-axes": node["rotations"]![8]!["aY"] = 4095; break;
            case "wrong-provenance": node["source"]!["kind"] = "retail"; break;
            case "null": text = "null"; break;
        }
        if (kind is not ("duplicate" or "null")) text = node.ToJsonString();
        Assert.Throws<InvalidDataException>(() => PreparedDarkScreen.Parse(Encoding.UTF8.GetBytes(text)));
    }
    [Fact]
    public void Loader_rejects_wrong_id_hash_size_and_oversized_metadata()
    {
        var root = AuthoredDarkScreen.Write(Temp()); var store = LooseAssetStore.Load(root);
        Assert.Throws<KeyNotFoundException>(() => PreparedDarkScreen.Load(store, new("sample:missing/id")));
        var path = Path.Combine(root, "dark-screen.json"); var data = File.ReadAllBytes(path); data[0] ^= 1; File.WriteAllBytes(path,data);
        Assert.Throws<InvalidDataException>(() => PreparedDarkScreen.Load(store, AuthoredDarkScreen.Id));
        File.AppendAllText(path, "x"); Assert.Throws<InvalidDataException>(() => PreparedDarkScreen.Load(store, AuthoredDarkScreen.Id));
        Assert.Throws<InvalidDataException>(() => PreparedDarkScreen.Parse(new byte[65537]));
    }
    [Theory]
    [InlineData("layout")] [InlineData("delay")] [InlineData("rotation")] [InlineData("range")] [InlineData("hold")]
    [InlineData("params")] [InlineData("source")]
    public void Unsupported_domain_rejects_before_execution(string kind)
    {
        var d = AuthoredDarkScreen.Definition();
        d = kind switch
        {
            "layout" => d with { Tiles = d.Tiles.SetItem(0, new(0,0,0)) },
            "delay" => d with { Tiles = d.Tiles.SetItem(0, d.Tiles[0] with { InitialDelay = 42 }) },
            "rotation" => d with { Rotations = d.Rotations.SetItem(0, d.Rotations[0] with { Angle = 1 }) },
            "range" => d with { Rotations = d.Rotations.SetItem(0, d.Rotations[0] with { AX = 4097 }) },
            "hold" => d with { HoldFrames = 256 },
            "params" => d with { Parameters = new(Divisor: 0) },
            _ => d with { Source = d.Source with { ExecutableSha256 = new string('0',64) } },
        };
        Assert.Throws<InvalidDataException>(() => d.ToExecutionDefinition());
    }
    [Fact]
    public void Existing_profiles_reject_the_new_metadata()
    {
        var data = AuthoredDarkScreen.Definition().ToUtf8();
        Assert.Throws<InvalidDataException>(() => PreparedReadyArtwork.Parse(data));
        Assert.Throws<InvalidDataException>(() => PreparedGeneralBanner.Parse(data));
        Assert.Throws<InvalidDataException>(() => PreparedReadyEvent.Parse(data));
        Assert.Throws<InvalidDataException>(() => PreparedOrderedBannerEvent.Parse(data));
    }
}
