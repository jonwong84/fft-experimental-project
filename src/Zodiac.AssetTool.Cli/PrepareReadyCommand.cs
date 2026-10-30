using System.Security.Cryptography;
using System.Text.Json;
using Zodiac.Content;
using Zodiac.Content.Events;
using Zodiac.Framework;

namespace Zodiac.AssetTool.Cli;

/// <summary>Bounded offline Ready preparation. Original input is never needed by the prepared runtime.</summary>
public sealed class PrepareReadyCommand(string workspaceRoot)
{
    public static AssetId ArtworkId => new("fft.ps1-us:ready/image-0");
    public const string Usage = "prepare-ready-artwork --bin <scratch/image.bin> --output <fresh-scratch-directory> OR prepare-ready-event --input <scratch/block> --encoding raw|processed [--text-offset <n>] --id <id> --artwork-id <id> --owner-id <id> --provenance authored --output <fresh-scratch-directory>";
    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        string? staging = null;
        var names = new List<string>();
        try
        {
            if (args.Length < 1 || args.Length % 2 != 1 || args[0] is not ("prepare-ready-event" or "prepare-ready-artwork")) throw new ArgumentException(Usage);
            bool artwork = args[0] == "prepare-ready-artwork";
            var allowed = artwork ? new[] { "--bin", "--output" } : new[] { "--input", "--encoding", "--text-offset", "--id", "--artwork-id", "--owner-id", "--provenance", "--output" };
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i < args.Length; i += 2)
                if (!allowed.Contains(args[i]) || string.IsNullOrWhiteSpace(args[i + 1]) || !options.TryAdd(args[i], args[i + 1])) throw new ArgumentException(Usage);
            foreach (var key in allowed.Where(k => k != "--text-offset"))
                if (!options.ContainsKey(key)) throw new ArgumentException(Usage);
            string destination = ScratchPath(options["--output"]);
            if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("Output exists; choose a fresh scratch directory.");
            var files = new Dictionary<string, byte[]>();
            var entries = new List<PreparedAssetEntry>();
            void Add(AssetId id, string name, byte[] bytes)
            { files.Add(name, bytes); entries.Add(new(id, name, bytes.Length, Hash(bytes))); }
            if (artwork)
            {
                var decoded = ReadImage(ScratchPath(options["--bin"]));
                var definition = CreateDefinition(decoded);
                Add(ArtworkId, "artwork.json", definition.ToUtf8());
                Add(new(definition.IndicesId), "indices.bin", decoded.Indices.ToArray());
                Add(new(definition.PaletteId), "palette-2.bin", decoded.Palette.ToArray());
                files.Add("provenance.json", JsonSerializer.SerializeToUtf8Bytes(new
                {
                    edition = "SCUS-94221",
                    definition.Source,
                    inputImageSha256 = "29e63bb6d2e7b842fe87f7c0ee03b7076681c34bcc47095cca27ec53236bf4f9",
                    evidence = "docs/evidence/events/ready-rendering-source-v1.json"
                }));
            }
            else
            {
                if (options["--provenance"] != "authored" || options["--encoding"] is not ("raw" or "processed")) throw new ArgumentException(Usage);
                int? textOffset = null;
                if (options.TryGetValue("--text-offset", out var text))
                {
                    if (!int.TryParse(text, out int offset)) throw new ArgumentException(Usage);
                    textOffset = offset;
                }
                using var stream = File.OpenRead(ScratchPath(options["--input"]));
                if (stream.Length != 8192) throw new InvalidDataException("An authored 8192-byte block is required.");
                var bytes = new byte[8192]; stream.ReadExactly(bytes);
                var inspected = ScenarioBlockInspector.Inspect(bytes, options["--encoding"] == "raw" ? ScenarioBlockEncoding.Raw : ScenarioBlockEncoding.Processed, textOffset);
                var definition = ReadyEventExporter.Create(inspected, new(options["--id"]), new(options["--artwork-id"]), new(options["--owner-id"]));
                Add(definition.Id, "event.json", definition.ToUtf8());
                files.Add("provenance.json", JsonSerializer.SerializeToUtf8Bytes(new { kind = "authored", definition.Source, profile = PreparedReadyEvent.Profile }));
            }
            string parent = Path.GetDirectoryName(destination)!;
            Directory.CreateDirectory(parent);
            staging = Path.Combine(parent, ".ready-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            foreach (var file in files) { names.Add(file.Key); File.WriteAllBytes(Path.Combine(staging, file.Key), file.Value); }
            names.Add(PreparedAssetManifest.FileName);
            new PreparedAssetManifest(entries).SaveToFile(Path.Combine(staging, PreparedAssetManifest.FileName));
            var store = LooseAssetStore.Load(staging);
            if (artwork) _ = PreparedReadyArtwork.Load(store, ArtworkId);
            else _ = PreparedReadyEvent.Load(store, entries[0].Id).ToExecutionDefinition();
            Directory.Move(staging, destination);
            output.WriteLine($"Prepared Ready {(artwork ? "artwork" : "event")} at {destination}.");
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException or KeyNotFoundException)
        { error.WriteLine("Ready preparation failed: " + e.Message); return 1; }
        finally
        {
            if (staging is not null && Directory.Exists(staging))
            {
                foreach (string name in names) File.Delete(Path.Combine(staging, name));
                Directory.Delete(staging);
            }
        }
    }
    private static DecodedReadyArtwork ReadImage(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length != 541315152 || HashStream(stream) != "29e63bb6d2e7b842fe87f7c0ee03b7076681c34bcc47095cca27ec53236bf4f9")
            throw new InvalidDataException("Expected North American SCUS-94221 image.");
        var reader = new Mode2RawImageReader(stream);
        byte[] Read(int lba, int length, string hash)
        {
            var bytes = new byte[length];
            for (int at = 0; at < length; at += 2048)
                reader.ReadUserData(lba + at / 2048).AsSpan(0, Math.Min(2048, length - at)).CopyTo(bytes.AsSpan(at));
            Verify(bytes, hash); return bytes;
        }
        _ = Read(23, 68, "c2979409e7999a14c4ea02c9ee4f339deabc4adfc54b787650796b0ab84e0dfe");
        var exe = Read(24, 356352, "287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562");
        if (!exe.AsSpan(0, 8).SequenceEqual("PS-X EXE"u8)) throw new InvalidDataException("Missing executable header.");
        return DecodeVerified(Read(5824, 958464, PrepareGeneralBannerCommand.BonusHash), Read(2192, 127684, PrepareGeneralBannerCommand.RequireHash));
    }
    public static DecodedReadyArtwork DecodeVerified(byte[] bonus, byte[] require)
    {
        if (bonus.Length != 958464 || require.Length != 127684) throw new InvalidDataException("Invalid Ready input lengths.");
        Verify(bonus, PrepareGeneralBannerCommand.BonusHash); Verify(require, PrepareGeneralBannerCommand.RequireHash);
        byte[] Range(int offset, int length, string hash)
        { var bytes = require.AsSpan(offset, length).ToArray(); Verify(bytes, hash); return bytes; }
        Verify(bonus.AsSpan(0, 0x6800), "5ba6d5c56bbffa7a4c1bdaea392fcf08f265f545588ccc28496462b63d521ea4");
        var decoded = ReadyArtworkDecoder.Decode(bonus.AsSpan(0, 0x6800),
            Range(73448, 72, "d683d8cf6f60818ad871f1e5a2dc3338e9541530bbba6c4dfc59ea21ed10168c"),
            Range(75068, 2, "40d88127d4d31a3891f41598eeed41174e5bc89b1eb9bbd66a8cbfc09956a3fd"),
            Range(75080, 120, "6e49be7cf5a0f2156e1e865289df2ba4305eae1a9bd9717c226a0e484bea4961"),
            Range(69744, 8, "f83702f2efe22401f556e941c19d84fc387c944a3b7c77de8abaa225ef2ee849"));
        Verify(decoded.Palette.AsSpan(), "06ab76ecb265e6f4a40d0abf00812d2ac0f2817c4d20c80b69a502c2978b3256");
        return decoded;
    }
    public static PreparedReadyArtwork CreateDefinition(DecodedReadyArtwork decoded) => new()
    {
        Format = "fft.ready-artwork",
        SchemaVersion = 1,
        Id = ArtworkId.Value,
        IndicesId = ArtworkId.Value + "/indices",
        PaletteId = ArtworkId.Value + "/palette-2",
        IndexFormat = "index4-unpacked-u8",
        PaletteFormat = "rgb555-stp-le16",
        Width = 256,
        Height = 200,
        Condition = 0,
        Image = 0,
        PaletteBank = 2,
        Glyphs = [.. decoded.Glyphs.Select(g => new ReadyGlyph(g.SourceIndex, g.X, g.Y, g.U, g.V, g.Width, g.Height, "average", [.. g.Colors.Select(c => new BannerRgb(c.R, c.G, c.B))]))],
        Source = new("verified-retail", PreparedReadyArtwork.ReferenceRevision, PrepareGeneralBannerCommand.BonusHash, PrepareGeneralBannerCommand.RequireHash),
    };
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string HashStream(Stream stream) => Convert.ToHexStringLower(SHA256.HashData(stream));
    private static void Verify(ReadOnlySpan<byte> bytes, string hash)
    { if (Hash(bytes) != hash) throw new InvalidDataException("Ready source/range SHA-256 mismatch."); }
    private string ScratchPath(string path)
    {
        string scratch = Path.GetFullPath(Path.Combine(workspaceRoot, "scratch")) + Path.DirectorySeparatorChar, full = Path.GetFullPath(path);
        if (!full.StartsWith(scratch, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new IOException("Ready preparation must remain below workspace scratch/.");
        for (string? at = full; at is not null; at = Path.GetDirectoryName(at))
            if ((File.Exists(at) || Directory.Exists(at)) && (File.GetAttributes(at) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reparse-point paths are unsupported.");
        return full;
    }
}
