using System.Security.Cryptography;
using System.Text;
using Zodiac.Content;
using Zodiac.Content.BattleSetup;

namespace Zodiac.AssetTool.Cli;

public sealed class PrepareFireBattleCommand(string workspaceRoot)
{
    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        string? staging = null;
        try
        {
            if (args.Length != 5 || args[0] != "prepare-fire-battle" || args[1] != "--exe" || args[3] != "--output")
                throw new ArgumentException("Usage: prepare-fire-battle --exe <scratch/SCUS_942.21> --output <fresh-scratch-directory>");
            string scratch = Path.GetFullPath(Path.Combine(workspaceRoot, "scratch")) + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            string CheckedPath(string value)
            {
                string path = Path.GetFullPath(value);
                if (!path.StartsWith(scratch, comparison))
                    throw new IOException("Original input and prepared output must stay under repository scratch.");
                for (string? p = path; p is not null; p = Path.GetDirectoryName(p))
                    if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Reparse-point preparation paths are unsupported.");
                return path;
            }
            string input = CheckedPath(args[2]);
            string destination = CheckedPath(args[4]);
            if (File.Exists(destination) || Directory.Exists(destination) || input.StartsWith(destination + Path.DirectorySeparatorChar, comparison))
                throw new IOException("Select a fresh output directory that cannot contain the original input.");
            if (new FileInfo(input).Length != OriginalMetadataImporter.ExecutableLength)
                throw new InvalidDataException("Unsupported executable length.");
            var result = OriginalFireImporter.ImportVerified(File.ReadAllBytes(input));
            byte[] package = Encoding.UTF8.GetBytes(result.Package.ToJson());
            string parent = Path.GetDirectoryName(destination)!;
            Directory.CreateDirectory(parent);
            staging = Path.Combine(parent, ".fire-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            File.WriteAllBytes(Path.Combine(staging, "ordinary-magic.package.json"), package);
            File.WriteAllText(Path.Combine(staging, "fire.import.json"), OriginalFireImporter.Provenance(result), new UTF8Encoding(false));
            new PreparedAssetManifest([new(PreparedOrdinaryMagicBattle.AssetId, "ordinary-magic.package.json", package.Length,
                Convert.ToHexStringLower(SHA256.HashData(package)))])
                .SaveToFile(Path.Combine(staging, PreparedAssetManifest.FileName));
            _ = PreparedOrdinaryMagicBattle.Load(LooseAssetStore.Load(staging));
            Directory.Move(staging, destination);
            output.WriteLine($"Prepared verified Fire and authored encounter at {destination}; {result.Records.Length} source records, explicit learned grant.");
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException or NotSupportedException or UnauthorizedAccessException)
        {
            error.WriteLine("Fire preparation failed: " + e.Message);
            return 1;
        }
        finally
        {
            if (staging is not null && Directory.Exists(staging))
            {
                foreach (string name in new[] { "ordinary-magic.package.json", "fire.import.json", PreparedAssetManifest.FileName })
                    File.Delete(Path.Combine(staging, name));
                Directory.Delete(staging);
            }
        }
    }
}
