using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Zodiac.Content;
using Zodiac.Content.Events;
using Zodiac.Framework;

namespace Fft.Tests.Support;

internal static class AuthoredReady
{
    internal static readonly AssetId Id = new("sample:event/ready-test");
    internal static readonly AssetId ArtworkId = new("sample:ready/artwork");
    internal static readonly AssetId Owner = new("sample:owner/ready-test");
    internal static PreparedReadyArtwork Artwork() => new()
    {
        Format = "fft.ready-artwork",
        SchemaVersion = 1,
        Id = ArtworkId.Value,
        IndicesId = "sample:ready/indices",
        PaletteId = "sample:ready/palette",
        IndexFormat = "index4-unpacked-u8",
        PaletteFormat = "rgb555-stp-le16",
        Width = 256,
        Height = 200,
        Condition = 0,
        Image = 0,
        PaletteBank = 2,
        Glyphs = [..Enumerable.Range(0,6).Select(i=>new ReadyGlyph(i,8+i*32,40,16*i,0,16,24,"average",
            [new(0,0,0),new(64,64,64),new(128,128,128),new(255,255,255)]))],
        Source = new("authored-test", PreparedReadyArtwork.ReferenceRevision, null, null),
    };
    internal static LoadedReadyArtwork Loaded(ushort word = 0x801f) => new(Artwork(), [.. Enumerable.Repeat((byte)1, 51200)],
        [0, 0, (byte)word, (byte)(word >> 8), .. new byte[28]]);
    internal static byte[] Code(int hold = 60) => [0x78, 0, (byte)hold, 0xe5, 56, 0, 0xd4, 0, 0xdb];
    internal static byte[] Block(int hold = 60, bool processed = false, byte[]? code = null)
    {
        code ??= Code(hold);
        var bytes = new byte[8192];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, processed ? 0xf2f2f2f2u : (uint)(4 + code.Length));
        code.CopyTo(bytes, 4); bytes[4 + code.Length] = 0xfe; return bytes;
    }
    internal static PreparedReadyEvent Event(int hold = 60) => new(Id, ArtworkId, Owner,
        new("synthetic", "SCUS-94221", PreparedReadyArtwork.ReferenceRevision, Convert.ToHexStringLower(SHA256.HashData(Block(hold))), "raw", 13),
        [new(4,OrderedScenarioOperation.DisplayConditions,0,hold),new(7,OrderedScenarioOperation.WaitForTaskClass,56),
         new(10,OrderedScenarioOperation.ForwardTargetExit,0),new(12,OrderedScenarioOperation.SearchEnd)]);
    internal static void WriteEvent(string root, PreparedReadyEvent? definition = null)
    {
        definition ??= Event();
        Write(root, [(definition.Id, "event.json", definition.ToUtf8())]);
    }
    internal static void WriteArtwork(string root, PreparedReadyArtwork? definition = null, byte[]? metadata = null, byte[]? indices = null, byte[]? palette = null)
    {
        definition ??= Artwork();
        Write(root, [(new(definition.Id),"artwork.json",metadata??definition.ToUtf8()),
            (new(definition.IndicesId),"indices.bin",indices??Loaded().Indices.ToArray()),
            (new(definition.PaletteId),"palette.bin",palette??Loaded().Palette.ToArray())]);
    }
    private static void Write(string root, (AssetId Id, string File, byte[] Bytes)[] files)
    {
        Directory.CreateDirectory(root);
        foreach (var file in files) File.WriteAllBytes(Path.Combine(root, file.File), file.Bytes);
        new PreparedAssetManifest(files.Select(f => new PreparedAssetEntry(f.Id, f.File, f.Bytes.Length, Convert.ToHexStringLower(SHA256.HashData(f.Bytes)))))
            .SaveToFile(Path.Combine(root, PreparedAssetManifest.FileName));
    }
}
