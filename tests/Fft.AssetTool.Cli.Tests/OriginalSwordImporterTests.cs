using System.Buffers.Binary;
using System.Text.Json.Nodes;
using Fft.AssetTool.Cli;
using Xunit;

namespace Fft.AssetTool.Cli.Tests;

public sealed class OriginalSwordImporterTests
{
    private static int Offset(uint address) => checked((int)(address - 0x80010000 + 0x800));

    [Fact]
    public void Selected_records_and_runtime_Attack_formula_are_decoded_without_synthetic_substitution()
    {
        byte[] image = Synthetic();
        var result = OriginalSwordImporter.DecodeSelection(image);
        Assert.Equal(7, result.Weapon.Power); Assert.Equal(9, result.Weapon.Evade);
        Assert.Equal(132, result.Weapon.Flags); Assert.Equal(0, result.Proc.Type);
        Assert.Equal(171, result.Weapon.Unused03); Assert.Equal(0x1234, result.Item.Price);
        Assert.Equal(8, result.AttackMenuType); Assert.Equal(8, result.AttackSecondary[8]);
        Assert.Equal(1, result.Weapon.Formula); Assert.Equal(255, result.AttackAbility.TypeFlags);
        Assert.Contains(result.Diagnostics, text => text.StartsWith("power: imported 7", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, text => text.StartsWith("procTypeRaw: imported 0", StringComparison.Ordinal));
        Assert.Equal(Enumerable.Range(0, 25).Select(i => (byte)i), result.Attributes);
        Assert.Equal(7, result.Records.Length);
        foreach (var record in result.Records)
        {
            Assert.Equal(Offset(record.VirtualAddress), record.FileOffset);
            Assert.Equal(Convert.ToHexStringLower(image.AsSpan(record.FileOffset, record.ByteLength)), record.RawHex);
        }
        Assert.Equal(new[] { "g_item_primary_data", "g_item_weapon_data", "g_status_infliction_data", "g_item_attributes", "g_ability_data", "g_ability_range_data", "g_command_menu_types_by_skillset" }, result.Records.Select(r => r.Table));
    }

    [Fact]
    public void Export_is_deterministic_and_unverified_decode_never_claims_an_edition_hash()
    {
        byte[] image = Synthetic();
        var first = OriginalSwordImporter.DecodeSelection(image);
        string json = OriginalSwordImporter.ToJson(first);
        Assert.Equal(json, OriginalSwordImporter.ToJson(OriginalSwordImporter.DecodeSelection(image)));
        Array.Clear(image);
        Assert.Equal(json, OriginalSwordImporter.ToJson(first));
        Assert.DoesNotContain("\r", json);
        var root = JsonNode.Parse(json)!;
        Assert.Null(root["targetEdition"]); Assert.Null(root["input"]!["sha256"]);
        Assert.Equal("unverified-layout-decoding", root["confidence"]!.GetValue<string>());
        Assert.Equal("metadata-only", root["runtimeReadiness"]!.GetValue<string>());
        Assert.Equal(1, root["attack"]!["effectiveFormula"]!.GetValue<int>());
        Assert.Throws<InvalidDataException>(() => OriginalSwordImporter.ImportVerified(Synthetic()));
        Assert.Throws<InvalidDataException>(() => OriginalSwordImporter.ImportVerified(new byte[1]));
    }

    [Theory]
    [InlineData("header")] [InlineData("size")] [InlineData("short-data")]
    [InlineData("category")] [InlineData("type")] [InlineData("secondary")]
    [InlineData("proc")] [InlineData("attributes")] [InlineData("menu")]
    public void Invalid_mapping_or_unsupported_reference_fails_with_a_diagnostic(string invalid)
    {
        byte[] image = Synthetic();
        switch (invalid)
        {
            case "header": image[0] = 0; break;
            case "size": BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x1c), 0); break;
            case "short-data": image = image[..0x53000]; BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x1c), (uint)image.Length - 0x800); break;
            case "category": image[Offset(0x80062eb8 + 19 * 12) + 3] = 0; break;
            case "type": image[Offset(0x80062eb8 + 19 * 12) + 5] = 4; break;
            case "secondary": image[Offset(0x80062eb8 + 19 * 12) + 4] = 20; break;
            case "proc": image[Offset(0x80063ab8 + 19 * 8) + 7] = 128; break;
            case "attributes": image[Offset(0x80062eb8 + 19 * 12) + 7] = 80; break;
            case "menu": image[Offset(0x80065cb4 + 1)] = 0; break;
        }
        Assert.False(string.IsNullOrWhiteSpace(Assert.Throws<InvalidDataException>(() => OriginalSwordImporter.DecodeSelection(image)).Message));
    }

    [Fact]
    public void Valid_reference_boundary_indices_resolve_the_last_record_not_the_next_table()
    {
        byte[] image = Synthetic(); image[Offset(0x80063ab8 + 19 * 8) + 7] = 127;
        image[Offset(0x80062eb8 + 19 * 12) + 7] = 79;
        var result = OriginalSwordImporter.DecodeSelection(image);
        Assert.Contains(result.Records, r => r.Table == "g_status_infliction_data" && r.Index == 127 && r.VirtualAddress == 0x800642be);
        Assert.Contains(result.Records, r => r.Table == "g_item_attributes" && r.Index == 79 && r.VirtualAddress == 0x80064a7b);
    }

    [Fact]
    public void Sword_command_uses_existing_scratch_and_hash_guards()
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Assert.Equal(1, new ImportMetadataCommand(root).Run(["import-sword", "--exe", Path.Combine(root, "outside.exe"), "--output", Path.Combine(root, "scratch", "out")], output, error));
        Assert.Contains("scratch", error.ToString()); Assert.Empty(output.ToString()); Assert.False(Directory.Exists(root));
    }

    private static byte[] Synthetic()
    {
        var image = new byte[OriginalMetadataImporter.ExecutableLength];
        "PS-X EXE"u8.CopyTo(image); BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x18), 0x80010000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x1c), (uint)image.Length - 0x800);
        new byte[] { 0, 12, 1, 128, 19, 3, 254, 0, 52, 18, 1, 253 }.CopyTo(image, Offset(0x80062eb8 + 19 * 12));
        // Deliberately invented values, not a copy of the retail weapon row.
        new byte[] { 1, 132, 1, 171, 7, 9, 0, 3 }.CopyTo(image, Offset(0x80063ab8 + 19 * 8));
        Enumerable.Range(0, 25).Select(i => (byte)i).ToArray().CopyTo(image, Offset(0x800642c4));
        new byte[] { 255, 255, 255, 255, 1, 2, 3, 4 }.CopyTo(image, Offset(0x8005ebf0));
        Enumerable.Range(0, 14).Select(i => (byte)i).ToArray().CopyTo(image, Offset(0x8005fbf0));
        image[Offset(0x80065cb4 + 1)] = 8;
        return image;
    }
}
