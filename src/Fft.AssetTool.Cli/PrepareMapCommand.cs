using System.Security.Cryptography;
using Fft.Content;
using Fft.Core;

namespace Fft.AssetTool.Cli;

/// <summary>Verified MAP003 base day/dry preparation. All source inputs and derived output remain in scratch.</summary>
public sealed class PrepareMapCommand(string workspaceRoot)
{
    public const string ReferenceRevision = "11464943bbdbd2a0af022d90cb7f10455cf911df";
    public const string GnsHash = "3df4a492a1ae6c6aca645a9b58b5984cb8dbf13b2631a3819215d8a516327d57";
    public const string MeshHash = "227172783f50289e4a7425a63966fad7cbfd4599af80b76870ea094ff2f56ff4";
    public const string TextureHash = "746af063e812f591e8c72f0510764316e005621eab73ae5fbe99565eac9fa5a4";
    public static AssetId MapId => new("base:map/003/day-dry-frozen");
    public static AssetId TextureId => new("base:map/003/day-dry-indices");
    private readonly string scratch = Path.GetFullPath(Path.Combine(workspaceRoot, "scratch"));
    private static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        string? staging = null;
        try
        {
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            if (args.Length == 0 || args[0] != "prepare-map") throw new ArgumentException("Expected prepare-map.");
            for (int i = 1; i < args.Length; i++)
            {
                string key = args[i];
                if (key is not ("--gns" or "--mesh" or "--texture" or "--output") || i + 1 >= args.Length ||
                    args[i + 1].StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(key, args[++i]))
                    throw new ArgumentException($"Unknown, duplicate, or missing option: {key}");
            }
            if (options.Count != 4) throw new ArgumentException("--gns, --mesh, --texture and --output are required.");
            string destination = ScratchPath(options["--output"]);
            if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("Output already exists; choose a fresh scratch directory.");
            string gnsPath = ScratchPath(options["--gns"]), meshPath = ScratchPath(options["--mesh"]), texturePath = ScratchPath(options["--texture"]);
            byte[] gns = ReadVerified(gnsPath, 568, GnsHash);
            var selection = MapGnsSelection.ReadDayDry(gns);
            if (selection != new MapFileSelection(12658, 131072, 12722, 43008))
                throw new InvalidDataException("MAP003 selection differs from the pinned source trace.");
            byte[] mesh = ReadVerified(meshPath, 41852, MeshHash);
            byte[] packedTexture = ReadVerified(texturePath, 131072, TextureHash);
            var decoded = OriginalBattleMap.Parse(mesh);
            var prepared = MapContentExporter.Create(decoded, MapId, TextureId,
                new("SCUS-94221", ReferenceRevision, GnsHash, MeshHash, TextureHash));
            byte[] metadata = prepared.ToUtf8(), texture = OriginalBattleMap.DecodeTexture(packedTexture).ToArray();
            var manifest = new PreparedAssetManifest([Entry(MapId, "map.json", metadata), Entry(TextureId, "texture.indices", texture)]);

            string parent = Path.GetDirectoryName(destination)!;
            Directory.CreateDirectory(parent);
            staging = Path.Combine(parent, ".map-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            File.WriteAllBytes(Path.Combine(staging, "map.json"), metadata);
            File.WriteAllBytes(Path.Combine(staging, "texture.indices"), texture);
            manifest.SaveToFile(Path.Combine(staging, PreparedAssetManifest.FileName));
            _ = PreparedBattleMap.Load(LooseAssetStore.Load(staging), MapId);
            Directory.Move(staging, destination);
            output.WriteLine($"Prepared {MapId}: {prepared.Width}x{prepared.Depth}, two terrain layers, {prepared.Polygons.Length} polygons at {destination}.");
            output.WriteLine("Day/dry/default arrangement; base presentation frozen. Source-derived data, not original-rendering equivalence or playable combat.");
            return 0;
        }
        catch (ArgumentException e)
        {
            error.WriteLine(e.Message);
            error.WriteLine("Usage: prepare-map --gns <scratch/MAP003.GNS> --mesh <scratch/MAP003.9> --texture <scratch/MAP003.8> --output <fresh-scratch-directory>");
            return 2;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
        {
            error.WriteLine("Map preparation failed: " + e.Message);
            return 1;
        }
        finally
        {
            if (staging is not null && Directory.Exists(staging))
            {
                foreach (string file in new[] { "map.json", "texture.indices", PreparedAssetManifest.FileName }) File.Delete(Path.Combine(staging, file));
                Directory.Delete(staging);
            }
        }
    }

    private string ScratchPath(string path)
    {
        string full = Path.GetFullPath(path);
        if (!full.StartsWith(scratch + Path.DirectorySeparatorChar, Comparison)) throw new IOException("Map input/output must stay below repository scratch/.");
        for (string? at = full; at is not null; at = Path.GetDirectoryName(at))
            if ((File.Exists(at) || Directory.Exists(at)) && (File.GetAttributes(at) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reparse-point paths are unsupported for map preparation.");
        return full;
    }
    private static byte[] ReadVerified(string path, int length, string hash)
    {
        if (new FileInfo(path).Length != length) throw new InvalidDataException("Unsupported map input length: " + path);
        byte[] data = File.ReadAllBytes(path);
        if (data.Length != length || Convert.ToHexStringLower(SHA256.HashData(data)) != hash)
            throw new InvalidDataException("Map input SHA-256 mismatch: " + path);
        return data;
    }
    private static PreparedAssetEntry Entry(AssetId id, string path, byte[] data) => new(id, path, data.Length, Convert.ToHexStringLower(SHA256.HashData(data)));
}
