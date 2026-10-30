using System.Security.Cryptography;
using Zodiac.Content;
using Zodiac.Content.Events;

namespace Zodiac.AssetTool.Cli;

/// <summary>Publishes an authored descriptor only; all three prepared resources are loaded separately.</summary>
public sealed class PrepareDarkScreenIntroductionCommand(string workspaceRoot)
{
    public const string Usage = "prepare-dark-screen-introduction --id <id> --dark-screen-id <id> --general-artwork-id <id> --ready-artwork-id <id> --owner-id <id> --general-hold <0..255> --ready-hold <0..255> --provenance authored --output <fresh-scratch-root>";
    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        string? staging = null;
        try
        {
            string[] keys = ["--id", "--dark-screen-id", "--general-artwork-id", "--ready-artwork-id", "--owner-id", "--general-hold", "--ready-hold", "--provenance", "--output"];
            if (args.Length != 19 || args[0] != "prepare-dark-screen-introduction") throw new ArgumentException(Usage);
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i < args.Length; i += 2)
                if (!keys.Contains(args[i]) || string.IsNullOrWhiteSpace(args[i + 1]) || !options.TryAdd(args[i], args[i + 1])) throw new ArgumentException(Usage);
            if (!int.TryParse(options["--general-hold"], out int general) || !int.TryParse(options["--ready-hold"], out int ready)) throw new ArgumentException(Usage);
            var definition = new PreparedDarkScreenIntroduction
            {
                Format = PreparedDarkScreenIntroduction.FormatName, SchemaVersion = 1, Profile = PreparedDarkScreenIntroduction.ProfileName,
                Id = options["--id"], DarkScreenId = options["--dark-screen-id"], GeneralArtworkId = options["--general-artwork-id"],
                ReadyArtworkId = options["--ready-artwork-id"], OwnerId = options["--owner-id"], GeneralHold = general, ReadyHold = ready,
                Provenance = options["--provenance"], ReferenceRevision = PreparedDarkScreen.ReferenceRevision,
            };
            byte[] bytes = definition.ToUtf8();
            string destination = Scratch(options["--output"]);
            if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Output exists.");
            string parent = Path.GetDirectoryName(destination)!; Directory.CreateDirectory(parent);
            staging = Path.Combine(parent, ".dark-introduction-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);
            File.WriteAllBytes(Path.Combine(staging, "introduction.json"), bytes);
            new PreparedAssetManifest([new(new(definition.Id), "introduction.json", bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)))])
                .SaveToFile(Path.Combine(staging, PreparedAssetManifest.FileName));
            _ = PreparedDarkScreenIntroduction.Load(LooseAssetStore.Load(staging), new(definition.Id));
            Directory.Move(staging, destination); output.WriteLine($"Prepared authored dark-screen introduction at {destination}."); return 0;
        }
        catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException or KeyNotFoundException)
        { error.WriteLine("Introduction preparation failed: " + e.Message); return 1; }
        finally
        {
            if (staging is not null && Directory.Exists(staging))
            {
                foreach (string name in new[] { "introduction.json", PreparedAssetManifest.FileName })
                    if (File.Exists(Path.Combine(staging, name))) File.Delete(Path.Combine(staging, name));
                Directory.Delete(staging);
            }
        }
    }
    private string Scratch(string value)
    {
        string path = Path.GetFullPath(value, workspaceRoot), prefix = Path.GetFullPath(Path.Combine(workspaceRoot, "scratch")) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException("Output must stay below scratch/.");
        for (string? p = path; p is not null; p = Path.GetDirectoryName(p))
            if ((Directory.Exists(p) || File.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse path unsupported.");
        return path;
    }
}
