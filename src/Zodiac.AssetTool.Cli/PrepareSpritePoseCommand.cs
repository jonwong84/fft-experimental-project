using System.Security.Cryptography;
using Zodiac.Content;
using Zodiac.Framework;

namespace Zodiac.AssetTool.Cli;

/// <summary>Verified SCUS-94221 Chocobo ordinary frame only. All game-derived inputs/output remain local in scratch.</summary>
public sealed class PrepareSpritePoseCommand(string workspaceRoot)
{
    public const string ReferenceRevision = "11464943bbdbd2a0af022d90cb7f10455cf911df";
    public const string SprHash = "7f7a94e5615137f9a4fa6e1dcfbad8b2760a6b66099154171b0e1feb137632d2";
    public const string ShpHash = "235b36f2e2990993a38c9d30fcd927aefef645af9de8c49394dc07e5f8a1ae27";
    public const string SeqHash = "f746461ad326c8bb1a12c0d430495046330649b2952dae72db50b94873395b6b";
    public static AssetId PoseId => new("base:battle/cyoko/idle-front-pose");
    public static AssetId TextureId => new("base:battle/cyoko/ordinary-rgba");
    private readonly string scratch = Path.GetFullPath(Path.Combine(workspaceRoot, "scratch"));
    private static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        string? staging = null;
        try
        {
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            if (args.Length == 0 || args[0] != "prepare-sprite-pose") throw new ArgumentException("Expected prepare-sprite-pose.");
            for (int i = 1; i < args.Length; i++)
            {
                string key = args[i];
                if (key is not ("--spr" or "--shp" or "--seq" or "--output") || i + 1 >= args.Length ||
                    args[i + 1].StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(key, args[++i]))
                    throw new ArgumentException($"Unknown, duplicate, or missing option: {key}");
            }
            if (options.Count != 4) throw new ArgumentException("--spr, --shp, --seq and --output are required.");
            string destination = ScratchPath(options["--output"]);
            if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Output already exists; choose a fresh scratch directory.");
            byte[] spr = ReadVerified(ScratchPath(options["--spr"]), 49572, SprHash);
            byte[] shp = ReadVerified(ScratchPath(options["--shp"]), 7316, ShpHash);
            byte[] seq = ReadVerified(ScratchPath(options["--seq"]), 3068, SeqHash);
            var decoded = OrdinaryUnitPoseDecoder.Decode(UnitSpriteSheet.Parse(spr), UnitShapeFile.Parse(shp), UnitSequenceFile.Parse(seq), 6, 0);
            if (decoded.FrameId != 9 || decoded.Wait != 6) throw new InvalidDataException("Selected pose differs from ZOD-027's trace.");
            var pose = SpritePoseContentExporter.Create(decoded, PoseId, TextureId,
                new("SCUS-94221", ReferenceRevision, SprHash, ShpHash, SeqHash));
            byte[] metadata = pose.ToUtf8(), rgba = decoded.Rgba.ToArray();
            var manifest = new PreparedAssetManifest([Entry(PoseId, "pose.json", metadata), Entry(TextureId, "ordinary.rgba", rgba)]);

            string parent = Path.GetDirectoryName(destination)!;
            Directory.CreateDirectory(parent);
            staging = Path.Combine(parent, ".sprite-pose-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            File.WriteAllBytes(Path.Combine(staging, "pose.json"), metadata);
            File.WriteAllBytes(Path.Combine(staging, "ordinary.rgba"), rgba);
            manifest.SaveToFile(Path.Combine(staging, PreparedAssetManifest.FileName));
            _ = PreparedSpritePose.Load(LooseAssetStore.Load(staging), PoseId); // Validate the published contract before moving it.
            Directory.Move(staging, destination);
            output.WriteLine($"Prepared {PoseId}: frame 9, wait 6 (frozen), bank 0, {pose.PartsBackToFront.Length} pieces at {destination}.");
            output.WriteLine("Source-derived preparation; no original-game rendered comparison. Load prepared files without SPR/SHP/SEQ/BIN access.");
            return 0;
        }
        catch (ArgumentException e)
        {
            error.WriteLine(e.Message);
            error.WriteLine("Usage: prepare-sprite-pose --spr <scratch/CYOKO.SPR> --shp <scratch/CYOKO.SHP> --seq <scratch/CYOKO.SEQ> --output <fresh-scratch-directory>");
            return 2;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
        {
            error.WriteLine("Sprite pose preparation failed: " + e.Message);
            return 1;
        }
        finally
        {
            if (staging is not null && Directory.Exists(staging))
            {
                // Only known files in our unique staging directory; no recursive cleanup of user paths.
                foreach (string file in new[] { "pose.json", "ordinary.rgba", PreparedAssetManifest.FileName })
                    File.Delete(Path.Combine(staging, file));
                Directory.Delete(staging);
            }
        }
    }

    private string ScratchPath(string path)
    {
        string full = Path.GetFullPath(path);
        if (!full.StartsWith(scratch + Path.DirectorySeparatorChar, Comparison))
            throw new IOException("Sprite input and prepared output must stay below repository scratch/.");
        for (string? at = full; at is not null; at = Path.GetDirectoryName(at))
            if ((File.Exists(at) || Directory.Exists(at)) && (File.GetAttributes(at) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reparse-point paths are unsupported for sprite preparation.");
        return full;
    }

    private static byte[] ReadVerified(string path, int length, string hash)
    {
        if (new FileInfo(path).Length != length) throw new InvalidDataException("Unsupported sprite input length: " + path);
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length != length || Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("Sprite input SHA-256 mismatch: " + path);
        return bytes;
    }

    private static PreparedAssetEntry Entry(AssetId id, string path, byte[] bytes) =>
        new(id, path, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));
}
