using System.Buffers.Binary;
using System.Text.Json.Nodes;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class OriginalFireImporterTests
{
    private static int Offset(uint address) => checked((int)(address - 0x80010000 + 0x800));

    [Fact]
    public void Synthetic_layout_import_keeps_source_records_and_authored_grants_distinct()
    {
        byte[] image = Image(); byte[] before = image.ToArray();
        var imported = OriginalFireImporter.DecodeSelection(image);
        var definition = imported.Package.Definition;
        var fire = Assert.Single(definition.Spells).Value;
        Assert.Equal(14, fire.Spell.Power); Assert.Equal(6, fire.MpCost); Assert.Equal((byte)4, fire.RawChargeTime);
        Assert.Equal(MagicElement.Fire, fire.Spell.Elements); Assert.True(fire.Spell.SecondaryEvadeable);
        Assert.Equal(34, imported.Records.Length); Assert.Equal(before, image);
        foreach (var record in imported.Records)
        {
            Assert.Equal(Offset(record.VirtualAddress), record.FileOffset);
            Assert.Equal(Convert.ToHexStringLower(image.AsSpan(record.FileOffset, record.ByteLength)), record.RawHex);
        }
        Assert.Contains(fire.Id, definition.Units[0].LearnedSpells); Assert.Empty(definition.Units[1].LearnedSpells);
        Assert.Equal((ushort)50, definition.Units[0].JobPoints[new("fft.ps1:job/wizard")].Current);
        var receipt = JsonNode.Parse(OriginalFireImporter.Provenance(imported))!;
        Assert.Null(receipt["verifiedInputSha256"]);
        Assert.Equal("unverified-layout-decoding", receipt["confidence"]!.GetValue<string>());
        Assert.Contains("authored encounter", receipt["scope"]!.GetValue<string>());
        Assert.Throws<InvalidDataException>(() => OriginalFireImporter.ImportVerified(image));
        Array.Clear(image); Assert.Equal(before.Length, image.Length);
        Assert.Equal(imported.Package.ToJson(), OriginalFireImporter.DecodeSelection(before).Package.ToJson());
    }

    [Theory]
    [InlineData("spell")] [InlineData("status")] [InlineData("slot")] [InlineData("menu")]
    [InlineData("innate")] [InlineData("affinity")] [InlineData("growth")] [InlineData("learning")]
    [InlineData("header")]
    public void Unsupported_or_inconsistent_inputs_fail_closed(string change)
    {
        byte[] image = Image();
        switch (change)
        {
            case "spell": image[Offset(0x8005fbf0 + 16 * 14) + 6] ^= 1; break;
            case "status": image[Offset(0x80063fc4)] = 1; break;
            case "slot": image[Offset(0x80064a94 + 11 * 25) + 3] = 17; break;
            case "menu": image[Offset(0x80065cb4 + 11)] = 8; break;
            case "innate": image[Offset(0x800610b8 + 80 * 48) + 1] = 1; break;
            case "affinity": image[Offset(0x800610b8 + 80 * 48) + 26] = 1; break;
            case "growth": image[Offset(0x800610b8 + 80 * 48) + 14] = 0; break;
            case "learning": image[Offset(0x8005ebf0 + 16 * 8) + 1] = 255; break;
            case "header": image[0] = 0; break;
        }
        Assert.Throws<InvalidDataException>(() => OriginalFireImporter.DecodeSelection(image));
    }

    [Fact]
    public void Preparation_rejects_wrong_edition_before_publishing_output()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "fft-fire-import-" + Guid.NewGuid().ToString("N"));
        string scratch = Path.Combine(workspace, "scratch"); Directory.CreateDirectory(scratch);
        string image = Path.Combine(scratch, "fake.exe"), output = Path.Combine(scratch, "prepared");
        try
        {
            File.WriteAllBytes(image, Image());
            using var message = new StringWriter(); using var error = new StringWriter();
            Assert.Equal(1, new PrepareFireBattleCommand(workspace).Run(
                ["prepare-fire-battle", "--exe", image, "--output", output], message, error));
            Assert.Contains("SHA-256", error.ToString()); Assert.False(Directory.Exists(output));
            Assert.Equal([image], Directory.GetFiles(scratch));
        }
        finally { File.Delete(image); Directory.Delete(scratch); Directory.Delete(workspace); }
    }

    private static byte[] Image()
    {
        var image = new byte[OriginalMetadataImporter.ExecutableLength];
        "PS-X EXE"u8.CopyTo(image);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x18), 0x80010000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x1c), (uint)image.Length - 0x800);
        new byte[] { 4, 1, 1, 0, 0, 226, 66, 128, 8, 0, 14, 0, 4, 6 }.CopyTo(image, Offset(0x8005fbf0 + 16 * 14));
        image[Offset(0x8005ebf0 + 16 * 8)] = 50;
        image[Offset(0x80064a94 + 11 * 25) + 3] = 16;
        foreach (int id in new[] { 74, 80 })
        {
            int job = Offset((uint)(0x800610b8 + id * 48)); image[job] = (byte)(id == 74 ? 5 : 11);
            for (int i = 13; i <= 22; i++) image[job + i] = 100;
            image[job + 23] = 3; image[job + 24] = 3;
        }
        ushort[] thresholds = [100, 200, 350, 550, 800, 1150, 1550, 2100];
        for (int i = 0; i < thresholds.Length; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(Offset((uint)(0x80066182 + (i + 1) * 2))), thresholds[i]);
        return image;
    }
}
