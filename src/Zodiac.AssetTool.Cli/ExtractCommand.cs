using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zodiac.Content;
using Zodiac.AssetTool;
using Zodiac.Framework;

namespace Zodiac.AssetTool.Cli;

public sealed class ExtractCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _workspaceRoot;
    private readonly string _scratchRoot;

    public ExtractCommand(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
        _scratchRoot = Path.GetFullPath(Path.Combine(_workspaceRoot, "scratch"));
    }

    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            ExtractOptions options = ParseArguments(args);
            string binPath = Path.GetFullPath(options.BinPath);
            string cuePath = Path.GetFullPath(options.CuePath);
            string referencePath = Path.GetFullPath(options.ReferencePath ?? Path.Combine(
                _scratchRoot, "fft-decomp-work", "build", "extracted", "inventory.json"));
            string outputRoot = ValidateScratchOutputRoot(options.OutputRoot, binPath, cuePath, referencePath);

            InventoryCommand.ValidateCue(cuePath, binPath);
            string sourceHash = InventoryCommand.ValidateImageProfile(binPath);
            ReferenceInventory reference = ReadReference(referencePath);
            if (!string.Equals(reference.TargetId, InventoryCommand.TargetId, StringComparison.Ordinal) || reference.Files is null ||
                reference.Files.Any(item => string.IsNullOrWhiteSpace(item.Type)))
            {
                throw new InvalidDataException($"Reference inventory must target '{InventoryCommand.TargetId}', contain a files array, and specify a type for every file.");
            }

            using var stream = new FileStream(binPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var image = new Mode2RawImageReader(stream);
            IsoPrimaryVolumeDescriptor volume = IsoPrimaryVolumeDescriptorReader.Read(image);
            IReadOnlyList<IsoFileEntry> entries = IsoPrimaryVolumeDirectoryReader.ReadFiles(image, volume);
            IsoDataFileExtractor.ValidateOutputPaths(entries);

            ReferenceInventoryEntry[] comparisonEntries = reference.Files
                .Select(item => new ReferenceInventoryEntry(item.Path, item.Lba, item.Bytes, item.Type!))
                .ToArray();
            InventoryComparisonResult comparison = InventoryComparison.Compare(entries, comparisonEntries);
            if (!comparison.IsMatch)
            {
                throw new InvalidDataException("The supplied reference inventory does not match the image paths and LBAs; refusing extraction.");
            }

            var referenceByPath = reference.Files.ToDictionary(item => item.Path, StringComparer.Ordinal);
            var entriesByPath = entries.ToDictionary(item => item.RelativePath, StringComparer.Ordinal);
            var selectedEntries = new List<(IsoFileEntry Entry, string ExpectedSha256)>(options.Paths.Count == 0 ? entries.Count : options.Paths.Count);
            IEnumerable<string> selectedPaths = options.AllData
                ? reference.Files.Where(item => string.Equals(item.Type, "data", StringComparison.OrdinalIgnoreCase)).Select(item => item.Path)
                : options.Paths;
            foreach (string selectedPath in selectedPaths)
            {
                if (!referenceByPath.TryGetValue(selectedPath, out ExtractReferenceEntry? referenceEntry) ||
                    !entriesByPath.TryGetValue(selectedPath, out IsoFileEntry? entry))
                {
                    throw new InvalidDataException($"Selected path '{selectedPath}' is not in the image inventory.");
                }

                if (!string.Equals(referenceEntry.Type, "data", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"Selected path '{selectedPath}' is type '{referenceEntry.Type}'; only ordinary data files can be extracted.");
                }

                if (string.IsNullOrWhiteSpace(referenceEntry.Sha256) ||
                    referenceEntry.Sha256.Length != 64 || referenceEntry.Sha256.Any(character => !Uri.IsHexDigit(character)))
                {
                    throw new InvalidDataException($"Reference inventory has no valid SHA-256 for '{selectedPath}'.");
                }

                selectedEntries.Add((entry, referenceEntry.Sha256));
            }

            if (File.Exists(outputRoot))
            {
                throw new IOException($"Extraction output path is occupied by a file: {outputRoot}");
            }

            foreach (var (entry, _) in selectedEntries)
            {
                string destination = Path.GetFullPath(Path.Combine(outputRoot, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (File.Exists(destination) || Directory.Exists(destination))
                {
                    throw new IOException($"Output path already exists: {destination}");
                }

                string? parent = Path.GetDirectoryName(destination);
                while (parent is not null && !PathEquals(parent, outputRoot))
                {
                    if (File.Exists(parent))
                    {
                        throw new IOException($"Output directory path is occupied by a file: {parent}");
                    }

                    parent = Path.GetDirectoryName(parent);
                }
            }

            string manifestDirectory = PreparedAssetManifest.FileName + "/";
            if (selectedEntries.Any(item =>
                string.Equals(item.Entry.RelativePath, PreparedAssetManifest.FileName, StringComparison.OrdinalIgnoreCase) ||
                item.Entry.RelativePath.StartsWith(manifestDirectory, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException(
                    $"An image file conflicts with the generated {PreparedAssetManifest.FileName}.");
            }

            string manifestPath = Path.Combine(outputRoot, PreparedAssetManifest.FileName);
            if (File.Exists(manifestPath) || Directory.Exists(manifestPath))
            {
                throw new IOException($"Output path already exists: {manifestPath}");
            }

            var manifestEntries = new List<PreparedAssetEntry>(selectedEntries.Count);
            foreach (var (entry, expectedSha256) in selectedEntries)
            {
                string destination = IsoDataFileExtractor.ExtractForm1DataFile(image, entry, outputRoot);
                using FileStream extracted = File.OpenRead(destination);
                string actualSha256 = Convert.ToHexStringLower(SHA256.HashData(extracted));
                if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"SHA-256 mismatch for '{entry.RelativePath}': extracted {actualSha256}, expected {expectedSha256}.");
                }

                var id = new AssetId($"base:{entry.RelativePath.ToLowerInvariant()}");
                manifestEntries.Add(new PreparedAssetEntry(id, entry.RelativePath, entry.ByteLength, actualSha256));

                output.WriteLine($"Extracted {entry.RelativePath} ({entry.ByteLength} bytes, SHA-256 verified)");
                output.WriteLine($"  {Path.GetRelativePath(_workspaceRoot, destination)}");
            }

            new PreparedAssetManifest(manifestEntries).SaveToFile(manifestPath);
            output.WriteLine($"Prepared asset manifest: {Path.GetRelativePath(_workspaceRoot, manifestPath)}");
            output.WriteLine($"Source SHA-256: {sourceHash}");
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            error.WriteLine(exception.Message);
            return 2;
        }
    }

    private string ValidateScratchOutputRoot(string outputRootValue, string binPath, string cuePath, string referencePath)
    {
        string outputRoot = Path.GetFullPath(outputRootValue);
        string relative = Path.GetRelativePath(_scratchRoot, outputRoot);
        if (relative is "." or ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", PathComparison) || Path.IsPathRooted(relative))
        {
            throw new InvalidDataException("Extraction output must be a dedicated subdirectory inside repository scratch/.");
        }

        string prefix = Path.EndsInDirectorySeparator(outputRoot) ? outputRoot : outputRoot + Path.DirectorySeparatorChar;
        foreach (string inputPath in new[] { binPath, cuePath, referencePath })
        {
            string fullInput = Path.GetFullPath(inputPath);
            if (PathEquals(fullInput, outputRoot) || fullInput.StartsWith(prefix, PathComparison))
            {
                throw new InvalidDataException("The extraction output directory cannot contain an input image, CUE, or reference inventory.");
            }
        }

        return outputRoot;
    }

    private static ReferenceInventory ReadReference(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The reference inventory was not found.", path);
        }

        using FileStream stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<ReferenceInventory>(stream, JsonOptions)
            ?? throw new InvalidDataException("The reference inventory JSON is empty.");
    }

    private static ExtractOptions ParseArguments(string[] args)
    {
        const string usage = "Usage: extract --bin <path> --cue <path> (--path <ISO-relative-path> [--path <ISO-relative-path> ...] | --all-data) --output <scratch-subdirectory> [--reference <inventory.json>]";
        if (args.Length == 0 || !string.Equals(args[0], "extract", StringComparison.Ordinal))
        {
            throw new ArgumentException(usage);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var paths = new List<string>();
        bool allData = false;
        for (int index = 1; index < args.Length;)
        {
            string option = args[index];
            if (option == "--all-data")
            {
                if (allData)
                {
                    throw new ArgumentException("Option '--all-data' was supplied more than once.");
                }

                allData = true;
                index++;
                continue;
            }

            if (option == "--path")
            {
                if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Option '--path' requires a value.\n{usage}");
                }

                paths.Add(args[index + 1]);
                index += 2;
                continue;
            }

            if (option is not ("--bin" or "--cue" or "--output" or "--reference") ||
                index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Invalid option '{option}'.\n{usage}");
            }

            if (!values.TryAdd(option, args[index + 1]))
            {
                throw new ArgumentException($"Option '{option}' was supplied more than once.");
            }

            index += 2;
        }

        if (!values.TryGetValue("--bin", out string? binPath) ||
            !values.TryGetValue("--cue", out string? cuePath) ||
            !values.TryGetValue("--output", out string? outputRoot) || (paths.Count == 0 && !allData))
        {
            throw new ArgumentException($"BIN, CUE, output, and either one or more paths or --all-data are required.\n{usage}");
        }

        if (allData && paths.Count > 0)
        {
            throw new ArgumentException("Use either --all-data or --path options, not both.");
        }

        if (paths.Distinct(StringComparer.Ordinal).Count() != paths.Count)
        {
            throw new ArgumentException("A selected path was supplied more than once.");
        }

        return new ExtractOptions(binPath, cuePath, outputRoot, values.GetValueOrDefault("--reference"), paths, allData);
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), PathComparison);

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private sealed record ExtractOptions(string BinPath, string CuePath, string OutputRoot, string? ReferencePath, IReadOnlyList<string> Paths, bool AllData);

    private sealed record ReferenceInventory(
        [property: JsonPropertyName("target_id")] string? TargetId,
        [property: JsonPropertyName("files")] List<ExtractReferenceEntry>? Files);

    private sealed record ExtractReferenceEntry(string Path, uint Lba, uint Bytes, string? Type, string? Sha256);
}
