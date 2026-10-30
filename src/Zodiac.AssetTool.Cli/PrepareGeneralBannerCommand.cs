using System.Security.Cryptography;
using System.Text.Json;
using Zodiac.Content;
using Zodiac.Framework;

namespace Zodiac.AssetTool.Cli;

/// <summary>Offline SCUS-94221 selection. Publishes a fresh root only after strict prepared loading.</summary>
public sealed class PrepareGeneralBannerCommand(string workspaceRoot)
{
    public const string ReferenceRevision = "11464943bbdbd2a0af022d90cb7f10455cf911df";
    public const string BonusHash = "c5d881b1fb62c04d1d7f0e5238faff4131676165c08e35b5cadb68b21c89165f";
    public const string RequireHash = "97112c50c0b037b2cb5c9d93ae9e2a7506b582c5edbd86a1debfb7ab8e777630";
    public static AssetId BannerId => new("fft.ps1-us:banner/general-0");
    private readonly string scratch = Path.GetFullPath(Path.Combine(workspaceRoot, "scratch"));

    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        string? staging = null;
        try
        {
            if (args.Length != 5 || args[0] != "prepare-general-banner" || args[1] != "--bin" || args[3] != "--output")
                throw new ArgumentException("Usage: prepare-general-banner --bin <scratch/image.bin> --output <fresh-scratch-directory>");
            string input = ScratchPath(args[2]), destination = ScratchPath(args[4]);
            if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Output already exists; choose a fresh directory.");
            using var stream = File.OpenRead(input);
            if (stream.Length != 541315152 || Convert.ToHexStringLower(SHA256.HashData(stream)) !=
                "29e63bb6d2e7b842fe87f7c0ee03b7076681c34bcc47095cca27ec53236bf4f9")
                throw new InvalidDataException("Expected verified North American SCUS-94221 image.");
            var reader = new Mode2RawImageReader(stream);
            byte[] Read(int lba, int length, string hash)
            {
                byte[] bytes = new byte[length];
                for (int at = 0; at < length; at += 2048)
                {
                    byte[] sector = reader.ReadUserData(lba + at / 2048);
                    sector.AsSpan(0, Math.Min(2048, length - at)).CopyTo(bytes.AsSpan(at));
                }
                Verify(bytes, hash);
                return bytes;
            }
            _ = Read(23, 68, "c2979409e7999a14c4ea02c9ee4f339deabc4adfc54b787650796b0ab84e0dfe");
            var exe = Read(24, 356352, "287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562");
            if (!exe.AsSpan(0, 8).SequenceEqual("PS-X EXE"u8)) throw new InvalidDataException("Missing executable header.");
            var bonus = Read(5824, 958464, BonusHash);
            var require = Read(2192, 127684, RequireHash);
            var decoded = DecodeVerified(bonus, require);
            var definition = CreateDefinition(decoded);
            var files = new Dictionary<string, byte[]>
            {
                ["banner.json"] = definition.ToUtf8(), ["indices.bin"] = decoded.Indices.ToArray(), ["palette-4.bin"] = decoded.Palette.ToArray(),
                ["provenance.json"] = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    edition = "SCUS-94221", referenceRevision = ReferenceRevision,
                    inputImageSha256 = "29e63bb6d2e7b842fe87f7c0ee03b7076681c34bcc47095cca27ec53236bf4f9",
                    bonusSha256 = BonusHash, requireSha256 = RequireHash,
                    evidence = "docs/evidence/events/general-banner-assets-v1.json", policy = "preloaded RGB8 CPU preview; unresolved native parity; no original equivalence",
                }),
            };
            PreparedAssetEntry Entry(string id, string file) => new(new(id), file, files[file].Length, Hash(files[file]));
            var manifest = new PreparedAssetManifest([Entry(definition.Id, "banner.json"), Entry(definition.IndicesId, "indices.bin"), Entry(definition.PaletteId, "palette-4.bin")]);
            string parent = Path.GetDirectoryName(destination)!;
            Directory.CreateDirectory(parent);
            staging = Path.Combine(parent, ".general-banner-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            foreach (var file in files) File.WriteAllBytes(Path.Combine(staging, file.Key), file.Value);
            manifest.SaveToFile(Path.Combine(staging, PreparedAssetManifest.FileName));
            _ = PreparedGeneralBanner.Load(LooseAssetStore.Load(staging), BannerId);
            Directory.Move(staging, destination);
            output.WriteLine($"Prepared {BannerId}: six pieces, 256x200 indices, palette 4 at {destination}.");
            foreach (var file in files) output.WriteLine($"{file.Key}: {Hash(file.Value)}");
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException or KeyNotFoundException)
        { error.WriteLine("Banner preparation failed: " + e.Message); return 1; }
        finally
        {
            if (staging is not null && Directory.Exists(staging))
            {
                foreach (string name in new[] { "banner.json", "indices.bin", "palette-4.bin", "provenance.json", PreparedAssetManifest.FileName })
                    File.Delete(Path.Combine(staging, name));
                Directory.Delete(staging);
            }
        }
    }

    public static DecodedGeneralBanner DecodeVerified(byte[] bonus, byte[] require)
    {
        if (bonus.Length != 958464 || require.Length != 127684) throw new InvalidDataException("Unsupported named input lengths.");
        Verify(bonus, BonusHash); Verify(require, RequireHash);
        byte[] Range(int offset, int length, string hash)
        { var bytes = require.AsSpan(offset, length).ToArray(); Verify(bytes, hash); return bytes; }
        Verify(bonus.AsSpan(0, 0x6800), "5ba6d5c56bbffa7a4c1bdaea392fcf08f265f545588ccc28496462b63d521ea4");
        _ = Range(69744, 8, "f83702f2efe22401f556e941c19d84fc387c944a3b7c77de8abaa225ef2ee849");
        var decoded = GeneralBannerDecoder.Decode(bonus.AsSpan(0, 0x6800),
            Range(73204, 72, "32e8a361664837a78145d44ab07dc25c072be39eaf12e4661da29559b4d4178c"),
            Range(73300, 24, "00daaf8efa3b8fee46b793540bd642be32ccc3a6cf13b1e58bfb904f1d362e82"),
            Range(73324, 2, "40d88127d4d31a3891f41598eeed41174e5bc89b1eb9bbd66a8cbfc09956a3fd"),
            Range(73328, 120, "3c80f578dd6d1824e143fbed35b5c51a6b9ffa7c3599bf5dbea94a3fcde3e562"));
        Verify(decoded.Palette.AsSpan(), "8aa38621ccfcd6386e23466a803999ef5ed44a1afc415f447a9fcc0054027739");
        return decoded;
    }

    private static PreparedGeneralBanner CreateDefinition(DecodedGeneralBanner decoded) => new()
    {
        Format = "fft.general-banner", SchemaVersion = 1, Id = BannerId.Value, IndicesId = BannerId.Value + "/indices", PaletteId = BannerId.Value + "/palette-4",
        IndexFormat = "index4-unpacked-u8", PaletteFormat = "rgb555-stp-le16", Width = 256, Height = 200, Condition = 8, Image = 0, PaletteBank = 4,
        Pieces = [.. decoded.Pieces.Select(p => new GeneralBannerPiece(p.SourceIndex, p.SourceIndex / 3, p.X, p.Y, p.U, p.V, p.Width, p.Height, p.Material,
            [.. p.Colors.Select(c => new BannerRgb(c.R, c.G, c.B))]))],
        Source = new("verified-retail", ReferenceRevision, BonusHash, RequireHash),
    };
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Verify(ReadOnlySpan<byte> bytes, string hash)
    { if (Hash(bytes) != hash) throw new InvalidDataException("Source identity/range SHA-256 mismatch."); }
    private string ScratchPath(string path)
    {
        string full = Path.GetFullPath(path);
        if (!full.StartsWith(scratch + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new IOException("Banner input/output must remain below scratch/.");
        for (string? at = full; at is not null; at = Path.GetDirectoryName(at))
            if ((File.Exists(at) || Directory.Exists(at)) && (File.GetAttributes(at) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reparse-point paths are unsupported.");
        return full;
    }
}
