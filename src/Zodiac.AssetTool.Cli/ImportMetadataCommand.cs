using System.Text;
using System.Text.Json;
using Zodiac.Content.ContentPackages;

namespace Zodiac.AssetTool.Cli;

/// <summary>Imports verified original metadata into ignored local storage.</summary>
public sealed class ImportMetadataCommand(string workspaceRoot)
{
    private readonly string _scratch = Path.GetFullPath(Path.Combine(workspaceRoot, "scratch"));
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            if (args.Length == 0 || args[0] is not ("import-metadata" or "import-sword")) throw new ArgumentException("Expected import-metadata or import-sword command.");
            bool swordMode = args[0] == "import-sword";
            for (int i = 1; i < args.Length; i++)
            {
                string key = args[i];
                if ((key is not ("--exe" or "--output") && !(swordMode && key == "--metadata")) || i + 1 >= args.Length ||
                    args[i + 1].StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(key, args[++i]))
                    throw new ArgumentException($"Unknown, duplicate, or missing option: {key}");
            }
            if (!options.TryGetValue("--exe", out string? exe) || !options.TryGetValue("--output", out string? destination))
                throw new ArgumentException("--exe and --output are required.");
            string inputPath = ScratchPath(exe);
            string outputPath = ScratchPath(destination);
            string? metadataPath = options.TryGetValue("--metadata", out string? selected) ? ScratchPath(selected) : null;
            if (File.Exists(outputPath) || Directory.Exists(outputPath))
                throw new IOException("Output directory already exists; choose a fresh scratch destination.");
            if (inputPath.StartsWith(outputPath + Path.DirectorySeparatorChar, PathComparison))
                throw new IOException("Output directory cannot contain the input executable.");
            if (metadataPath is not null && metadataPath.StartsWith(outputPath + Path.DirectorySeparatorChar, PathComparison))
                throw new IOException("Output directory cannot contain the selected metadata dependency.");
            if (new FileInfo(inputPath).Length != OriginalMetadataImporter.ExecutableLength)
                throw new InvalidDataException("Input has an unsupported executable byte length.");
            byte[] bytes = File.ReadAllBytes(inputPath);
            OriginalMetadataImport? result = swordMode ? null : OriginalMetadataImporter.ImportVerified(bytes);
            OriginalSwordImport? sword = swordMode ? OriginalSwordImporter.ImportVerified(bytes) : null;
            SwordContentPackage? swordPackage = metadataPath is null ? null :
                OriginalSwordContentExporter.CreatePackage(sword!, ContentPackage.Load(metadataPath));
            string provenance = swordMode ? OriginalSwordImporter.ToJson(sword!) : JsonSerializer.Serialize(new
            {
                format = "fft.original-metadata-import", schemaVersion = 1,
                targetEdition = "SCUS-94221", scope = "partial-learning-metadata",
                confidence = "verified-input-source-traced-decoding",
                input = new { fileName = "SCUS_942.21", byteLength = bytes.Length, sha256 = OriginalMetadataImporter.ExecutableSha256 },
                referenceRevision = OriginalMetadataImporter.ReferenceRevision,
                converterVersion = OriginalMetadataImporter.ConverterVersion,
                records = result!.Records, diagnostics = result.Diagnostics,
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }) + "\n";

            // Publish the two related files together only after all decoding and validation succeeds.
            string parent = Path.GetDirectoryName(outputPath)!;
            Directory.CreateDirectory(parent);
            string staging = Path.Combine(parent, ".metadata-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(staging);
                if (!swordMode) result!.Package.Save(Path.Combine(staging, "content.package.json"));
                swordPackage?.Save(Path.Combine(staging, "sword.package.json"));
                File.WriteAllText(Path.Combine(staging, swordMode ? "sword.import.json" : "import.provenance.json"), provenance.Replace("\r\n", "\n", StringComparison.Ordinal), new UTF8Encoding(false));
                Directory.Move(staging, outputPath);
            }
            finally
            {
                // Only the two known files in our freshly generated staging path are candidates for cleanup.
                if (Directory.Exists(staging))
                {
                    File.Delete(Path.Combine(staging, "content.package.json"));
                    File.Delete(Path.Combine(staging, "import.provenance.json"));
                    File.Delete(Path.Combine(staging, "sword.import.json"));
                    File.Delete(Path.Combine(staging, "sword.package.json"));
                    Directory.Delete(staging);
                }
            }
            if (swordMode) output.WriteLine($"Imported selected retail sword and Attack metadata: {sword!.Records.Length} source records at {outputPath}; " +
                (swordPackage is null ? "metadata-only, no runtime package." : "validated schema2 sword.package.json exported; combat execution remains separate."));
            else output.WriteLine($"Imported {result!.Package.Jobs.Length} job, {result.Package.Skillsets.Length} skillset, " +
                $"{result.Package.Abilities.Length} abilities; {result.Records.Length} source records preserved at {outputPath}.");
            foreach (string diagnostic in swordMode ? sword!.Diagnostics : result!.Diagnostics) output.WriteLine(diagnostic);
            return 0;
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            error.WriteLine("Usage: import-metadata|import-sword --exe <scratch/SCUS_942.21> --output <fresh-scratch-directory> [--metadata <scratch/content.package.json> (import-sword only)]");
            return 2;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ContentPackageValidationException)
        {
            error.WriteLine($"Metadata import failed: {exception.Message}");
            return 1;
        }
    }

    private string ScratchPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string full = Path.GetFullPath(path);
        if (!full.StartsWith(_scratch + Path.DirectorySeparatorChar, PathComparison))
            throw new IOException("Original input and generated metadata must stay below the repository scratch directory.");
        // Prevent an existing junction/symlink from redirecting local game-derived output outside scratch.
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((Directory.Exists(current) || File.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Reparse-point paths are unsupported for metadata import: {current}");
        }
        return full;
    }
}
