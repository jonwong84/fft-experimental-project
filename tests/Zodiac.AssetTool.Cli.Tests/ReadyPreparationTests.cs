using System.Text.Json;
using Fft.Tests.Support;
using Zodiac.Content;
using Zodiac.Content.Events;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class ReadyPreparationTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(60, false)]
    [InlineData(255, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(60, true)]
    [InlineData(255, true)]
    public void RDY_030_unsigned_condition_and_hold_remain_distinct(int hold, bool processed)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/ready-display-contract-v1.json")));
        Assert.Equal("RDY-030", doc.RootElement.GetProperty("cases")[29].GetProperty("id").GetString());
        var bytes = AuthoredReady.Block(hold, processed);
        var inspection = ScenarioBlockInspector.Inspect(bytes, processed ? ScenarioBlockEncoding.Processed : ScenarioBlockEncoding.Raw, processed ? 13 : null);
        var definition = ReadyEventExporter.Create(inspection, AuthoredReady.Id, AuthoredReady.ArtworkId, AuthoredReady.Owner);
        var display = Assert.Single(definition.Instructions.Where(i => i.Operation == OrderedScenarioOperation.DisplayConditions));
        Assert.Equal(0, display.Operand1); Assert.Equal(hold, display.Operand2);
        Assert.Throws<InvalidDataException>(() => ScenarioEventExporter.CreateSyntheticOrderedBanner(inspection, AuthoredReady.Id, AuthoredReady.ArtworkId, AuthoredReady.Owner));
        Assert.Throws<InvalidDataException>(() => ScenarioEventExporter.CreateSyntheticTaskBarriers(inspection, AuthoredReady.Id));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RPR_025_preparation_publishes_atomically_and_preserves_existing_output(bool processed)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "fft-ready-prep-" + Guid.NewGuid().ToString("N"));
        string scratch = Path.Combine(workspace, "scratch"); Directory.CreateDirectory(scratch);
        string input = Path.Combine(scratch, "authored.block"), destination = Path.Combine(scratch, "prepared");
        File.WriteAllBytes(input, AuthoredReady.Block(processed: processed));
        var args = new List<string>{"prepare-ready-event","--input",input,"--encoding",processed?"processed":"raw","--id",AuthoredReady.Id.Value,
            "--artwork-id",AuthoredReady.ArtworkId.Value,"--owner-id",AuthoredReady.Owner.Value,"--provenance","authored","--output",destination};
        if (processed) args.AddRange(["--text-offset", "13"]);
        var command = new PrepareReadyCommand(workspace); var error = new StringWriter();
        Assert.Equal(0, command.Run(args.ToArray(), new StringWriter(), error));
        var definition = PreparedReadyEvent.Load(LooseAssetStore.Load(destination), AuthoredReady.Id);
        Assert.Equal(processed ? "processed" : "raw", definition.Source.Encoding);
        byte[] frozen = File.ReadAllBytes(Path.Combine(destination, "event.json"));
        Assert.Equal(1, command.Run(args.ToArray(), new StringWriter(), error));
        Assert.Equal(frozen, File.ReadAllBytes(Path.Combine(destination, "event.json")));
        Assert.Empty(Directory.GetDirectories(scratch, ".ready-*"));
    }
    [Theory]
    [InlineData("retail")]
    [InlineData("outside")]
    [InlineData("duplicate")]
    [InlineData("boundary")]
    [InlineData("unknown")]
    [InlineData("condition")]
    [InlineData("retail-4")]
    [InlineData("retail-401")]
    [InlineData("bin")]
    public void Failed_preparation_cannot_publish(string fault)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "fft-ready-invalid-" + Guid.NewGuid().ToString("N"));
        string scratch = Path.Combine(workspace, "scratch"); Directory.CreateDirectory(scratch);
        string input = Path.Combine(scratch, "input.block"), dest = Path.Combine(scratch, "output");
        File.WriteAllBytes(input, AuthoredReady.Block(code: fault == "unknown" ? [0xd4, 0, 0x45, 0xdb] : fault == "condition" ? [0x78, 8, 60, 0xd4, 0, 0xdb] : null));
        var args = new List<string>{"prepare-ready-event","--input",input,"--encoding","raw","--id",AuthoredReady.Id.Value,"--artwork-id",AuthoredReady.ArtworkId.Value,
            "--owner-id",AuthoredReady.Owner.Value,"--provenance",fault.StartsWith("retail",StringComparison.Ordinal)?"retail":"authored","--output",fault=="outside"?Path.Combine(workspace,"outside"):dest};
        if (fault == "duplicate") args.AddRange(["--id", AuthoredReady.Id.Value]);
        if (fault == "boundary") args.AddRange(["--text-offset", "13"]);
        if (fault == "bin") args = ["prepare-ready-artwork", "--bin", input, "--output", dest];
        Assert.Equal(1, new PrepareReadyCommand(workspace).Run(args.ToArray(), new StringWriter(), new StringWriter()));
        Assert.False(Directory.Exists(dest)); Assert.False(Directory.Exists(Path.Combine(workspace, "outside")));
        Assert.Empty(Directory.GetDirectories(scratch, ".ready-*"));
    }
    [Fact]
    public void Forged_inspection_and_source_identities_cannot_bypass_offline_validation()
    {
        Assert.Throws<InvalidDataException>(() => PrepareReadyCommand.DecodeVerified(new byte[958464], new byte[127684]));
        Assert.Throws<InvalidDataException>(() => PrepareReadyCommand.DecodeVerified([], []));
        var inspected = ScenarioBlockInspector.Inspect(AuthoredReady.Block(), ScenarioBlockEncoding.Raw);
        // Reinspection uses retained bytes even if the caller supplies previously valid diagnostics.
        var bytes = inspected.Bytes.ToArray(); bytes[5] = 8;
        var changed = ScenarioBlockInspector.Inspect(bytes, ScenarioBlockEncoding.Raw);
        Assert.Throws<InvalidDataException>(() => ReadyEventExporter.Create(changed, AuthoredReady.Id, AuthoredReady.ArtworkId, AuthoredReady.Owner));
    }
}
