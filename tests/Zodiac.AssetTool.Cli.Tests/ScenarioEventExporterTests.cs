using System.Buffers.Binary;
using System.Security.Cryptography;
using Zodiac.Content;
using Zodiac.Content.Events;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class ScenarioEventExporterTests
{
    [Theory]
    [InlineData(ScenarioBlockEncoding.Raw)]
    [InlineData(ScenarioBlockEncoding.Processed)]
    public void Authored_block_converts_and_loads_through_manifest_without_original_bytes(ScenarioBlockEncoding encoding)
    {
        byte[] code = [0xbe,0,0, 0xb1,0,0,2,0, 0xbe,1,0, 0xb0,1,0,1,0, 0xa2, 0xd0,1,
            0xbe,3,0, 0xb0,3,0,1,0, 0xd1,2, 0xd2,1, 0xbe,3,0, 0xb0,3,0,2,0, 0xd2,2, 0xd4,3, 0xdb];
        byte[] raw = Block(code);
        if (encoding == ScenarioBlockEncoding.Processed) BinaryPrimitives.WriteUInt32LittleEndian(raw, ScenarioBlockInspector.ProcessedMarker);
        var inspection = ScenarioBlockInspector.Inspect(raw, encoding, encoding == ScenarioBlockEncoding.Processed ? 48 : null);
        var prepared = ScenarioEventExporter.CreateSynthetic(inspection, new("sample:event/conditional-outcome-v1"), ScenarioEventScene.Battle);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(raw)), prepared.Source.BlockSha256);
        if (encoding == ScenarioBlockEncoding.Raw)
            Assert.Equal("7cb7271dcaf8ccc99a5507ac3b5b431c6c69663eadb1d6fad522cd1d9c0b7f2f", prepared.Source.BlockSha256);
        Assert.Equal(15, prepared.Instructions.Length);
        Assert.Equal(35, prepared.Instructions.Single(r => r.Offset == 21).TargetOffset);
        Assert.Equal(45, prepared.Instructions.Single(r => r.Offset == 31).TargetOffset);
        byte[] json = prepared.ToUtf8(); Array.Fill(raw, (byte)0); // Runtime loader needs only the prepared file.
        string directory = Path.Combine(Path.GetTempPath(), "fft-event-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "event.json");
        try
        {
            File.WriteAllBytes(file, json);
            new PreparedAssetManifest([new(prepared.Id, "event.json", json.Length, Convert.ToHexStringLower(SHA256.HashData(json)))])
                .SaveToFile(Path.Combine(directory, PreparedAssetManifest.FileName));
            var loaded = PreparedScenarioEvent.Load(LooseAssetStore.Load(directory), prepared.Id);
            Assert.Equal(json, loaded.ToUtf8());
            File.AppendAllText(file, " ");
            Assert.Throws<InvalidDataException>(() => PreparedScenarioEvent.Load(LooseAssetStore.Load(directory), prepared.Id));
        }
        finally
        {
            File.Delete(file); File.Delete(Path.Combine(directory, PreparedAssetManifest.FileName)); Directory.Delete(directory);
        }
    }

    [Fact]
    public void First_matching_target_wins_and_conditional_jump_to_exit_marker_lands_after_it()
    {
        var eventData = Prepare([0xd0,1, 0xd4,1, 0xd4,2, 0xd2,1, 0xd4,3, 0xdb]);
        Assert.Equal(8, eventData.Instructions[0].TargetOffset); // First D4 at 6, land at the second D4 at 8.
        var duplicate = Prepare([0xd1,1, 0xd2,1, 0xd4,2, 0xd2,1, 0xd4,3, 0xdb]);
        Assert.Equal(8, duplicate.Instructions[0].TargetOffset);
        // D1 ignores the D4 marker at 6 and finds D2 at 8, landing at 10.
        var unconditional = Prepare([0xd1,1, 0xd4,1, 0xd2,1, 0xd4,3, 0xdb]);
        Assert.Equal(10, unconditional.Instructions[0].TargetOffset);
    }

    [Theory]
    [InlineData("only-exit-target")]
    [InlineData("target-after-end")]
    [InlineData("lands-on-end")]
    [InlineData("fallthrough-end")]
    [InlineData("unsupported-unreachable")]
    [InlineData("unknown-byte")]
    [InlineData("packed-variable")]
    public void Unsupported_or_invalid_programs_never_become_prepared_definitions(string problem)
    {
        byte[] code = problem switch
        {
            "only-exit-target" => [0xd1,1, 0xd4,1, 0xdb],
            "target-after-end" => [0xd1,1, 0xdb, 0xd2,1, 0xd4,0],
            "lands-on-end" => [0xd0,1, 0xd4,1, 0xdb],
            "fallthrough-end" => [0xf2, 0xdb],
            "unsupported-unreachable" => [0xd4,0, 0x16, 0xdb], // Pause is known-length, even though unreachable.
            "packed-variable" => [0xbe,0x80,0, 0xd4,0, 0xdb],
            _ => [0xff, 0xdb],
        };
        Assert.Throws<InvalidDataException>(() => Prepare(code));
    }

    [Fact]
    public void Minimal_exit_and_nop_work_and_forged_inspection_records_cannot_bypass_raw_validation()
    {
        var prepared = Prepare([0xf2, 0xd4,255, 0xdb]);
        Assert.Equal(ScenarioOperation.Nop, prepared.Instructions[0].Operation);
        var inspected = ScenarioBlockInspector.Inspect(Block([0x16, 0xdb]), ScenarioBlockEncoding.Raw);
        var forged = inspected with { Instructions = [], Issues = [] };
        Assert.Throws<InvalidDataException>(() => ScenarioEventExporter.CreateSynthetic(forged, new("sample:event/test"), ScenarioEventScene.Battle));
    }

    [Fact]
    public void Label_bytes_inside_operands_are_not_jump_targets()
    {
        var prepared = Prepare([0xd1,1, 0xb0,0,0,0xd2,1, 0xd2,1, 0xd4,0, 0xdb]);
        Assert.Equal(13, prepared.Instructions[0].TargetOffset); // Skip the D2/01 bytes inside AddImmediate at 6.
    }

    private static PreparedScenarioEvent Prepare(byte[] code) => ScenarioEventExporter.CreateSynthetic(
        ScenarioBlockInspector.Inspect(Block(code), ScenarioBlockEncoding.Raw), new("sample:event/test"), ScenarioEventScene.Battle);
    private static byte[] Block(byte[] code)
    {
        byte[] block = new byte[8192]; int text = 4 + code.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(block, (uint)text); code.CopyTo(block, 4); block[text] = 0xfe;
        return block;
    }
}
