using System.Buffers.Binary;
using System.Security.Cryptography;
using Zodiac.Content;
using Zodiac.Framework;

namespace Zodiac.AssetTool.Cli;

public sealed class PrepareDarkScreenCommand(string workspaceRoot)
{
    public static AssetId Id => new("fft.ps1-us:dark-screen/origin-v1");
    public const string Usage = "prepare-dark-screen --executable <scratch/SCUS_942.21> --output <fresh-scratch-directory> [--hold-frames <0..255>]";
    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        string? staging = null;
        try
        {
            if (args.Length is not (5 or 7) || args[0] != "prepare-dark-screen") throw new ArgumentException(Usage);
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i < args.Length; i += 2)
                if (args[i] is not ("--executable" or "--output" or "--hold-frames") || string.IsNullOrWhiteSpace(args[i + 1]) || !options.TryAdd(args[i], args[i + 1]))
                    throw new ArgumentException(Usage);
            if (!options.ContainsKey("--executable") || !options.ContainsKey("--output")) throw new ArgumentException(Usage);
            if (!int.TryParse(options.GetValueOrDefault("--hold-frames", "44"), out int hold) || hold is < 0 or > 255) throw new ArgumentException(Usage);
            string destination = ScratchPath(options["--output"]), input = ScratchPath(options["--executable"]);
            if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("Output exists; select a fresh directory.");
            using var stream = File.OpenRead(input);
            if (stream.Length != 356352) throw new InvalidDataException("Expected the verified SCUS-94221 executable.");
            var bytes = new byte[356352]; stream.ReadExactly(bytes);
            var definition = DecodeVerified(bytes, hold);
            string parent = Path.GetDirectoryName(destination)!; Directory.CreateDirectory(parent);
            staging = Path.Combine(parent, ".dark-screen-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);
            var data = definition.ToUtf8(); File.WriteAllBytes(Path.Combine(staging, "dark-screen.json"), data);
            new PreparedAssetManifest([new(Id, "dark-screen.json", data.Length, Hash(data))]).SaveToFile(Path.Combine(staging, PreparedAssetManifest.FileName));
            _ = PreparedDarkScreen.Load(LooseAssetStore.Load(staging), Id).ToExecutionDefinition();
            Directory.Move(staging, destination);
            output.WriteLine($"Prepared dark-screen math/authored preview at {destination}."); return 0;
        }
        catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException or KeyNotFoundException)
        { error.WriteLine("Dark-screen preparation failed: " + e.Message); return 1; }
        finally
        {
            if (staging is not null && Directory.Exists(staging))
            {
                foreach (string name in new[] { "dark-screen.json", PreparedAssetManifest.FileName })
                    if (File.Exists(Path.Combine(staging, name))) File.Delete(Path.Combine(staging, name));
                Directory.Delete(staging);
            }
        }
    }
    public static PreparedDarkScreen DecodeVerified(ReadOnlySpan<byte> executable, int hold)
    {
        if (executable.Length != 356352 || Hash(executable) != PreparedDarkScreen.ExecutableHash ||
            !executable[..8].SequenceEqual("PS-X EXE"u8) || BinaryPrimitives.ReadUInt32LittleEndian(executable[0x18..]) != 0x80010000)
            throw new InvalidDataException("Original executable identity/header differs.");
        var sqrt = executable.Slice(0x800 + 0x2b8b8 - 0x10000, 384);
        var sincos = executable.Slice(0x800 + 0x2bee8 - 0x10000, 16384);
        if (Hash(sqrt) != PreparedDarkScreen.SqrtHash || Hash(sincos) != PreparedDarkScreen.SinCosHash) throw new InvalidDataException("Original math table identity differs.");
        var decoded = DarkScreenDecoder.Decode(sqrt, sincos);
        var definition = new PreparedDarkScreen
        {
            Format = "fft.dark-screen", SchemaVersion = 1, ExecutionProfile = PreparedDarkScreen.Profile,
            Id = Id.Value, OwnerId = "sample:owner/dark-screen-preview", HoldFrames = hold, Parameters = new(),
            Tiles = [.. decoded.Tiles.Select(t => new Zodiac.Framework.Events.DarkScreenTileDefinition(t.X, t.Y, t.InitialDelay))],
            Rotations = [.. decoded.Rotations.Select(r => new DarkScreenRotation(r.Angle, r.AX, r.AY, r.BX, r.BY))],
            Source = new("verified-retail", PreparedDarkScreen.ReferenceRevision, PreparedDarkScreen.ExecutableHash, PreparedDarkScreen.SqrtHash, PreparedDarkScreen.SinCosHash),
        };
        definition.Validate(); return definition;
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private string ScratchPath(string path)
    {
        string scratch = Path.GetFullPath(Path.Combine(workspaceRoot, "scratch")) + Path.DirectorySeparatorChar, full = Path.GetFullPath(path);
        if (!full.StartsWith(scratch, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw new IOException("Paths must remain below workspace scratch/.");
        for (string? at = full; at is not null; at = Path.GetDirectoryName(at))
            if ((File.Exists(at) || Directory.Exists(at)) && (File.GetAttributes(at) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse-point paths are unsupported.");
        return full;
    }
}
