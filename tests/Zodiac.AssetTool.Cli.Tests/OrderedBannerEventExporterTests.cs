using Fft.Tests.Support;
using Zodiac.Content;
using Zodiac.Content.Events;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class OrderedBannerEventExporterTests
{
    private static PreparedOrderedBannerEvent Export(byte[] block, bool processed = false)
        => ScenarioEventExporter.CreateSyntheticOrderedBanner(ScenarioBlockInspector.Inspect(block,
            processed ? ScenarioBlockEncoding.Processed : ScenarioBlockEncoding.Raw, processed ? 21 : null),
            AuthoredOrderedBannerEvent.Id, AuthoredOrderedBannerEvent.Artwork, AuthoredOrderedBannerEvent.Owner);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(60)] [InlineData(255)]
    public void Explicit_authored_raw_and_processed_exports_decode_two_unsigned_bytes(int hold)
    {
        var raw = Export(AuthoredOrderedBannerEvent.Block(AuthoredOrderedBannerEvent.Code(hold)));
        var processed = Export(AuthoredOrderedBannerEvent.Block(AuthoredOrderedBannerEvent.Code(hold), true), true);
        Assert.Equal(AuthoredOrderedBannerEvent.Definition(hold).ToUtf8(), raw.ToUtf8());
        Assert.Equal(raw.Instructions.ToArray(), processed.Instructions.ToArray());
        Assert.NotEqual(raw.Source.BlockSha256, processed.Source.BlockSha256);
        Assert.Equal("processed", processed.Source.Encoding);
        Assert.Equal(8, raw.Instructions[1].Operand1);
        Assert.Equal(hold, raw.Instructions[1].Operand2);
        var inspection = ScenarioBlockInspector.Inspect(AuthoredOrderedBannerEvent.Block(AuthoredOrderedBannerEvent.Code(hold)), ScenarioBlockEncoding.Raw);
        Assert.Throws<InvalidDataException>(() => ScenarioEventExporter.CreateSynthetic(inspection, raw.Id, ScenarioEventScene.Battle));
        Assert.Throws<InvalidDataException>(() => ScenarioEventExporter.CreateSynthetic(inspection, raw.Id, ScenarioEventScene.World));
        Assert.Throws<InvalidDataException>(() => ScenarioEventExporter.CreateSyntheticTimed(inspection, raw.Id));
        Assert.Throws<InvalidDataException>(() => ScenarioEventExporter.CreateSyntheticTaskBarriers(inspection, raw.Id));
    }

    [Theory]
    [InlineData(0)] [InlineData(7)] [InlineData(9)] [InlineData(255)]
    public void Unsupported_conditions_fail_even_after_an_unconditional_exit(int condition)
        => Assert.Throws<InvalidDataException>(() => Export(AuthoredOrderedBannerEvent.Block([0xd4, 0, 0x78, (byte)condition, 60, 0xdb])));

    [Theory]
    [InlineData("speed")] [InlineData("unknown")] [InlineData("end")] [InlineData("two")]
    [InlineData("none")] [InlineData("negative-wait")] [InlineData("task-high-byte")] [InlineData("truncated")]
    public void Unsupported_records_and_malformed_streams_fail_before_export(string fault)
    {
        byte[] code = fault switch
        {
            "speed" => [0x78, 8, 60, 0xd4, 0, 0x1c, 1, 0xdb],
            "unknown" => [0x78, 8, 60, 0xd4, 0, 0xff, 0xdb],
            "end" => [0x78, 8, 60, 0xdb],
            "two" => [0x78, 8, 60, 0xd4, 0, 0x78, 8, 1, 0xdb],
            "none" => [0xe5, 56, 0, 0xd4, 0, 0xdb],
            "negative-wait" => [0x78, 8, 60, 0xd4, 0, 0xf1, 0xff, 0xff, 0xdb],
            "task-high-byte" => [0x78, 8, 60, 0xd4, 0, 0xe5, 56, 1, 0xdb],
            _ => [0x78, 8],
        };
        Assert.Throws<InvalidDataException>(() => Export(AuthoredOrderedBannerEvent.Block(code)));
    }

    [Fact]
    public void Authored_forward_branch_can_skip_admission_but_all_records_are_validated()
    {
        var definition = Export(AuthoredOrderedBannerEvent.Block([0xd0, 1, 0x78, 8, 60, 0xd2, 1, 0xe5, 56, 0, 0xd4, 2, 0xdb]));
        Assert.Equal(11, definition.Instructions[0].TargetOffset);
        Assert.NotNull(definition.ToExecutionDefinition());
        var corrupt = ScenarioBlockInspector.Inspect(AuthoredOrderedBannerEvent.Block(), ScenarioBlockEncoding.Raw);
        var forged = corrupt with { Instructions = [], Issues = [] };
        Assert.Equal(Export(AuthoredOrderedBannerEvent.Block()).ToUtf8(), ScenarioEventExporter.CreateSyntheticOrderedBanner(forged,
            definition.Id, definition.ArtworkId, definition.OwnerId).ToUtf8());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Preparation_publishes_fresh_verified_root_and_preserves_existing_outputs(bool processed)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "fft-ordered-prep-" + Guid.NewGuid().ToString("N"));
        string scratch = Path.Combine(workspace, "scratch"), destination = Path.Combine(scratch, "prepared");
        Directory.CreateDirectory(scratch);
        string input = Path.Combine(scratch, "authored.block");
        File.WriteAllBytes(input, AuthoredOrderedBannerEvent.Block(processed: processed));
        var args = new List<string> { "prepare-ordered-banner-event", "--input", input, "--encoding", processed ? "processed" : "raw",
            "--id", AuthoredOrderedBannerEvent.Id.Value, "--artwork-id", AuthoredOrderedBannerEvent.Artwork.Value,
            "--owner-id", AuthoredOrderedBannerEvent.Owner.Value, "--provenance", "authored", "--output", destination };
        if (processed) args.AddRange(["--text-offset", "21"]);
        var command = new PrepareOrderedBannerEventCommand(workspace);
        var error = new StringWriter();
        Assert.Equal(0, command.Run(args.ToArray(), new StringWriter(), error));
        var loaded = PreparedOrderedBannerEvent.Load(LooseAssetStore.Load(destination), AuthoredOrderedBannerEvent.Id);
        Assert.Equal(processed ? "processed" : "raw", loaded.Source.Encoding);
        byte[] frozen = File.ReadAllBytes(Path.Combine(destination, "event.json"));
        Assert.Equal(1, command.Run(args.ToArray(), new StringWriter(), error));
        Assert.Equal(frozen, File.ReadAllBytes(Path.Combine(destination, "event.json")));
        Assert.Empty(Directory.GetDirectories(scratch, ".ordered-banner-*"));
    }

    [Theory]
    [InlineData("provenance")] [InlineData("outside")] [InlineData("duplicate")] [InlineData("boundary")] [InlineData("invalid")]
    public void Failed_preparation_leaves_no_output(string fault)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "fft-ordered-prep-" + Guid.NewGuid().ToString("N"));
        string scratch = Path.Combine(workspace, "scratch"), destination = Path.Combine(scratch, "prepared");
        Directory.CreateDirectory(scratch);
        string input = Path.Combine(scratch, "authored.block");
        File.WriteAllBytes(input, AuthoredOrderedBannerEvent.Block(fault == "invalid" ? [0x78, 0, 60, 0xd4, 0, 0xdb] : null));
        var args = new List<string> { "prepare-ordered-banner-event", "--input", input, "--encoding", "raw",
            "--id", AuthoredOrderedBannerEvent.Id.Value, "--artwork-id", AuthoredOrderedBannerEvent.Artwork.Value,
            "--owner-id", AuthoredOrderedBannerEvent.Owner.Value, "--provenance", fault == "provenance" ? "retail" : "authored",
            "--output", fault == "outside" ? Path.Combine(workspace, "outside") : destination };
        if (fault == "duplicate") args.AddRange(["--id", AuthoredOrderedBannerEvent.Id.Value]);
        if (fault == "boundary") args.AddRange(["--text-offset", "21"]);
        Assert.Equal(1, new PrepareOrderedBannerEventCommand(workspace).Run(args.ToArray(), new StringWriter(), new StringWriter()));
        Assert.False(Directory.Exists(destination));
        Assert.False(Directory.Exists(Path.Combine(workspace, "outside")));
        Assert.Empty(Directory.GetDirectories(scratch, ".ordered-banner-*"));
    }
}
