using System.Collections.Immutable;
using System.Security.Cryptography;
using Zodiac.Content;
using Zodiac.Framework;

namespace Fft.Tests.Support;

internal static class AuthoredGeneralBanner
{
    internal static PreparedGeneralBanner Definition() => new()
    {
        Format = "fft.general-banner", SchemaVersion = 1, Id = "sample:banner/test", IndicesId = "sample:banner/indices", PaletteId = "sample:banner/palette",
        IndexFormat = "index4-unpacked-u8", PaletteFormat = "rgb555-stp-le16", Width = 256, Height = 200, Condition = 8, Image = 0, PaletteBank = 4,
        Pieces = [.. Enumerable.Range(0, 6).Select(i => new GeneralBannerPiece(i, i / 3, 4, i < 3 ? 20 : 60, 0, i < 3 ? 128 : 160, 232, 31,
            i % 3 == 2 ? "add" : "subtract", [new(128, 128, 128), new(128, 128, 128), new(192, 192, 192), new(192, 192, 192)]))],
        Source = new("authored-test", "11464943bbdbd2a0af022d90cb7f10455cf911df", null, null),
    };
    internal static LoadedGeneralBanner Loaded() => new(Definition(), [.. Enumerable.Repeat((byte)1, 51200)], [0, 0, 31, 128, .. new byte[28]]);
    internal static void Write(string root, PreparedGeneralBanner? definition = null, byte[]? metadata = null, byte[]? indices = null, byte[]? palette = null)
    {
        Directory.CreateDirectory(root);
        var d = definition ?? Definition();
        var files = new[] { (d.Id, "banner.json", metadata ?? d.ToUtf8()), (d.IndicesId, "indices.bin", indices ?? Loaded().Indices.ToArray()),
            (d.PaletteId, "palette.bin", palette ?? Loaded().Palette.ToArray()) };
        foreach (var (_, name, bytes) in files) File.WriteAllBytes(Path.Combine(root, name), bytes);
        new PreparedAssetManifest(files.Select(f => new PreparedAssetEntry(new(f.Item1), f.Item2, f.Item3.Length, Convert.ToHexStringLower(SHA256.HashData(f.Item3)))))
            .SaveToFile(Path.Combine(root, PreparedAssetManifest.FileName));
    }
}
