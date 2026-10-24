using System.Buffers.Binary;
using Fft.AssetTool.Cli;
using Xunit;

namespace Fft.AssetTool.Cli.Tests;

public sealed class OriginalMetadataImporterTests
{
    private const int JobOffset = 0x52698;
    private const int SkillsetOffset = 0x55294 + 5 * 25;

    [Fact]
    public void Synthetic_selection_maps_raw_widths_ordered_slots_and_distinct_learning_definitions()
    {
        byte[] image = Synthetic();
        OriginalMetadataImport result = OriginalMetadataImporter.DecodeSelection(image);
        var job = Assert.Single(result.Package.Jobs);
        Assert.Equal("fft.ps1:job/squire", job.Id.Value);
        Assert.Equal("fft.ps1:skillset/basic-skill", job.PrimarySkillsetId.Value);
        Assert.Equal(255, job.BaseMove);
        Assert.Equal(127, job.BaseJump);
        Assert.True(job.SteppingStone);
        Assert.Equal(new[] { "fft.ps1:ability/source-0486", "fft.ps1:ability/source-0486" },
            job.InnateAbilityIds.Select(id => id.Value));
        var skillset = Assert.Single(result.Package.Skillsets);
        Assert.Equal(22, skillset.Slots.Length);
        Assert.Equal(Enumerable.Range(0, 22), skillset.Slots.Select(slot => slot.Slot));
        Assert.Equal("fft.ps1:ability/accumulate", skillset.Slots[0].AbilityId!.Value.Value);
        Assert.Equal(skillset.Slots[0].AbilityId, skillset.Slots[1].AbilityId);
        Assert.Null(skillset.Slots[2].AbilityId);
        Assert.Equal("fft.ps1:ability/source-0511", skillset.Slots[21].AbilityId!.Value.Value);
        Assert.Equal(8, result.Package.Abilities.Length);
        var ability = Assert.Single(result.Package.Abilities.Where(a => a.Id.Value == "fft.ps1:ability/accumulate"));
        Assert.Equal(65535, ability.JpCost);
        Assert.Equal(255, ability.LearnOnHitRateRaw);
        Assert.Equal(255, ability.TypeFlagsRaw);
        Assert.Equal(new byte[] { 1, 128, 255, 17 }, ability.AiFlagsRaw);
        Assert.Contains(result.Diagnostics, message => message.Contains("unknown type flag 0x10", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, message => message.Contains("unused low skillset flag bits 0x03", StringComparison.Ordinal));
    }

    [Fact]
    public void Source_envelope_covers_exact_job_and_skillset_bytes_and_each_correct_secondary_table()
    {
        byte[] image = Synthetic();
        OriginalMetadataImport result = OriginalMetadataImporter.DecodeSelection(image);
        var job = Assert.Single(result.Records.Where(record => record.Table == "g_job_data"));
        Assert.Equal(74, job.Index);
        Assert.Equal(0x80061e98u, job.VirtualAddress);
        Assert.Equal(JobOffset, job.FileOffset);
        Assert.Equal(48, job.ByteLength);
        Assert.Equal(Convert.ToHexStringLower(image.AsSpan(JobOffset, 48)), job.RawHex);
        var skillset = Assert.Single(result.Records.Where(record => record.Table == "g_ability_human_skillset_abilities"));
        Assert.Equal(5, skillset.Index);
        Assert.Equal(SkillsetOffset, skillset.FileOffset);
        Assert.Equal(25, skillset.ByteLength);
        CheckSecondary("fft.ps1:ability/accumulate", "g_ability_range_data", 146, 0x8005fbf0u + 146 * 14, 14);
        CheckSecondary("fft.ps1:ability/source-0368", "g_item_ability_item_ids", 0, 0x80061010, 1);
        CheckSecondary("fft.ps1:ability/source-0382", "g_ability_throw_item_types", 0, 0x80061020, 1);
        CheckSecondary("fft.ps1:ability/source-0394", "g_ability_jump_data", 0, 0x8006102c, 2);
        CheckSecondary("fft.ps1:ability/source-0406", "g_ability_charge_data", 0, 0x80061044, 2);
        CheckSecondary("fft.ps1:ability/source-0414", "g_ability_math_rsm_data", 0, 0x80061054, 1);
        CheckSecondary("fft.ps1:ability/source-0486", "g_ability_math_rsm_data", 72, 0x8006109c, 1);
        CheckSecondary("fft.ps1:ability/source-0511", "g_ability_math_rsm_data", 97, 0x800610b5, 1);
        foreach (var record in result.Records)
            Assert.Equal(Convert.ToHexStringLower(image.AsSpan(record.FileOffset, record.ByteLength)), record.RawHex);

        void CheckSecondary(string ability, string table, int index, uint address, int length)
        {
            var record = Assert.Single(result.Records.Where(r => r.DefinitionId == ability && r.Table != "g_ability_data"));
            Assert.Equal(table, record.Table);
            Assert.Equal(index, record.Index);
            Assert.Equal(address, record.VirtualAddress);
            Assert.Equal(Offset(address), record.FileOffset);
            Assert.Equal(length, record.ByteLength);
        }
    }

    [Fact]
    public void Output_and_raw_records_are_deterministic_snapshots_without_retaining_input_bytes()
    {
        byte[] image = Synthetic();
        var first = OriginalMetadataImporter.DecodeSelection(image);
        var second = OriginalMetadataImporter.DecodeSelection(image);
        string json = first.Package.ToJson();
        Assert.Equal(json, second.Package.ToJson());
        Assert.Equal(first.Records.ToArray(), second.Records.ToArray());
        Assert.Equal(first.Diagnostics.ToArray(), second.Diagnostics.ToArray());
        string[] raw = first.Records.Select(record => record.RawHex).ToArray();
        Array.Clear(image);
        Assert.Equal(json, first.Package.ToJson());
        Assert.Equal(raw, first.Records.Select(record => record.RawHex));
    }

    [Theory]
    [InlineData("header")]
    [InlineData("short-header")]
    [InlineData("load")]
    [InlineData("zero-size")]
    [InlineData("wrong-size")]
    [InlineData("truncated")]
    [InlineData("outside-data")]
    public void Invalid_executable_layout_is_rejected(string invalid)
    {
        byte[] image = Synthetic();
        switch (invalid)
        {
            case "header": image[0] = 0; break;
            case "short-header": image = image[..0x7ff]; break;
            case "load": BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x18), 0x80020000); break;
            case "zero-size": BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x1c), 0); break;
            case "wrong-size": BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x1c), 1); break;
            case "truncated": image = image[..^1]; break;
            case "outside-data":
                image = image[..0x53000];
                BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x1c), (uint)image.Length - 0x800);
                break;
        }
        Assert.Throws<InvalidDataException>(() => OriginalMetadataImporter.DecodeSelection(image));
    }

    [Theory]
    [InlineData(176)]
    [InlineData(223)]
    [InlineData(255)]
    public void Nonhuman_skillset_references_are_rejected(byte skillset)
    {
        byte[] image = Synthetic();
        image[JobOffset] = skillset;
        Assert.Contains("human-skillset", Assert.Throws<InvalidDataException>(() =>
            OriginalMetadataImporter.DecodeSelection(image)).Message);
    }

    [Theory]
    [InlineData(512)]
    [InlineData(65535)]
    public void Full_width_innate_references_are_rejected_without_truncation(ushort innate)
    {
        byte[] image = Synthetic();
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(JobOffset + 1), innate);
        Assert.Contains("0..511", Assert.Throws<InvalidDataException>(() =>
            OriginalMetadataImporter.DecodeSelection(image)).Message);
    }

    [Fact]
    public void Edition_attribution_requires_exact_size_and_hash_even_for_parseable_synthetic_image()
    {
        Assert.Throws<InvalidDataException>(() => OriginalMetadataImporter.ImportVerified(new byte[1]));
        byte[] image = Synthetic();
        Assert.Equal(OriginalMetadataImporter.ExecutableLength, image.Length);
        Assert.Contains("SHA-256 mismatch", Assert.Throws<InvalidDataException>(() =>
            OriginalMetadataImporter.ImportVerified(image)).Message);
    }

    private static byte[] Synthetic()
    {
        var image = new byte[0x57000];
        "PS-X EXE"u8.CopyTo(image);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x18), 0x80010000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x1c), 0x56800);
        image[JobOffset] = 5;
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(JobOffset + 1), 486);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(JobOffset + 5), 486);
        image[JobOffset + 0x17] = 255;
        image[JobOffset + 0x18] = 255;
        // Unknown/unused source bytes survive independently from the modeled definition.
        image[JobOffset + 0x2f] = 0xab;
        var ids = new ushort[22];
        ids[0] = ids[1] = 146;
        ids[7] = 368; ids[8] = 382; ids[15] = 394; ids[16] = 406; ids[17] = 414; ids[21] = 511;
        for (int i = 0; i < ids.Length; i++)
        {
            image[SkillsetOffset + 3 + i] = (byte)(ids[i] & 255);
            if (ids[i] >= 256) image[SkillsetOffset + i / 8] |= (byte)(0x80 >> (i % 8));
        }
        image[SkillsetOffset + 2] |= 3;
        foreach (ushort id in ids.Append((ushort)486).Where(id => id != 0).Distinct())
            new byte[] { 255, 255, 255, 255, 1, 128, 255, 17 }.CopyTo(image, Offset(0x8005ebf0u + (uint)id * 8));
        image[Offset(0x8005fbf0u + 146 * 14)] = 0xa5;
        image[Offset(0x8006109c)] = 0x5a;
        return image;
    }

    private static int Offset(uint address) => checked((int)(address - 0x80010000u + 0x800));
}
