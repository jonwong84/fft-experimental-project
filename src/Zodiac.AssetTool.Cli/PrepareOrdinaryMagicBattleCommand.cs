using System.Security.Cryptography;
using System.Text;
using Zodiac.Content;
using Zodiac.Content.BattleSetup;

namespace Zodiac.AssetTool.Cli;

public sealed class PrepareOrdinaryMagicBattleCommand(string workspaceRoot)
{
    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        string? staging = null;
        try
        {
            if (args.Length != 5 || args[0] is not ("prepare-fire-battle" or "prepare-elemental-magic-battle" or "prepare-healing-magic-battle" or "prepare-learning-magic-battle") || args[1] != "--exe" || args[3] != "--output")
                throw new ArgumentException("Usage: (prepare-fire-battle|prepare-elemental-magic-battle|prepare-healing-magic-battle|prepare-learning-magic-battle) --exe <scratch/SCUS_942.21> --output <fresh-scratch-directory>");
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
            var selection = args[0] switch
            {
                "prepare-fire-battle" => OriginalOrdinarySpellSelection.Fire,
                "prepare-elemental-magic-battle" => OriginalOrdinarySpellSelection.ElementalTiers,
                "prepare-learning-magic-battle" => OriginalOrdinarySpellSelection.LearnOnHit,
                _ => OriginalOrdinarySpellSelection.CureAndFire
            };
            var result = OriginalOrdinaryMagicImporter.ImportVerified(File.ReadAllBytes(input), selection);
            byte[] package = Encoding.UTF8.GetBytes(result.Package.ToJson());
            var metadata = result.Package.MetadataPackage ?? throw new InvalidDataException("Verified spell preparation requires its metadata companion.");
            byte[] metadataBytes = Encoding.UTF8.GetBytes(metadata.ToJson());
            string parent = Path.GetDirectoryName(destination)!;
            Directory.CreateDirectory(parent);
            staging = Path.Combine(parent, ".fire-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            File.WriteAllBytes(Path.Combine(staging, "ordinary-magic.package.json"), package);
            File.WriteAllBytes(Path.Combine(staging, "ordinary-magic.metadata.json"), metadataBytes);
            File.WriteAllText(Path.Combine(staging, selection == OriginalOrdinarySpellSelection.Fire ? "fire.import.json" : "ordinary-magic.import.json"),
                OriginalOrdinaryMagicImporter.Provenance(result), new UTF8Encoding(false));
            new PreparedAssetManifest([new(PreparedOrdinaryMagicBattle.AssetId, "ordinary-magic.package.json", package.Length,
                Convert.ToHexStringLower(SHA256.HashData(package))), new(new(metadata.PackageId), "ordinary-magic.metadata.json", metadataBytes.Length,
                Convert.ToHexStringLower(SHA256.HashData(metadataBytes)))])
                .SaveToFile(Path.Combine(staging, PreparedAssetManifest.FileName));
            _ = PreparedOrdinaryMagicBattle.Load(LooseAssetStore.Load(staging));
            Directory.Move(staging, destination);
            output.WriteLine($"Prepared verified {result.Package.Definition.Spells.Count} ordinary spells and authored encounter at {destination}; {result.Records.Length} source records, explicit learned grants.");
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
                foreach (string name in new[] { "ordinary-magic.package.json", "ordinary-magic.metadata.json", "fire.import.json", "ordinary-magic.import.json", PreparedAssetManifest.FileName })
                    File.Delete(Path.Combine(staging, name));
                Directory.Delete(staging);
            }
        }
    }
}
