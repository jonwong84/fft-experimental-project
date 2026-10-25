using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Zodiac.AssetTool;

namespace Zodiac.AssetTool.Cli;

public sealed class InventoryCommand
{
    public const string TargetId = "scus-94221";
    public const long ExpectedImageBytes = 541_315_152;
    public const string ExpectedImageSha256 = "29e63bb6d2e7b842fe87f7c0ee03b7076681c34bcc47095cca27ec53236bf4f9";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private static readonly Regex CueFileLine = new("^FILE\\s+\"(?<path>[^\"]+)\"\\s+BINARY$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CueTrackLine = new("^TRACK\\s+01\\s+MODE2/2352$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CueIndexLine = new("^INDEX\\s+01\\s+00:00:00$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly string _workspaceRoot;
    private readonly string _scratchRoot;

    public InventoryCommand(string workspaceRoot)
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
            CommandOptions options = ParseArguments(args);
            string binPath = Path.GetFullPath(options.BinPath);
            string cuePath = Path.GetFullPath(options.CuePath);
            ValidateCue(cuePath, binPath);
            string sourceHash = ValidateImageProfile(binPath);

            string referencePath = Path.GetFullPath(options.ReferencePath ?? Path.Combine(
                _scratchRoot, "fft-decomp-work", "build", "extracted", "inventory.json"));
            string inventoryPath = ResolveScratchOutput(options.InventoryPath ?? Path.Combine(_scratchRoot, "managed-inventory.json"));
            string reportPath = ResolveScratchOutput(options.ReportPath ?? Path.Combine(_scratchRoot, "managed-inventory-comparison.txt"));
            EnsureDistinctPaths(binPath, cuePath, referencePath, inventoryPath, reportPath);

            using var stream = new FileStream(binPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var rawImage = new Mode2RawImageReader(stream);
            IsoPrimaryVolumeDescriptor volume = IsoPrimaryVolumeDescriptorReader.Read(rawImage);
            IReadOnlyList<IsoFileEntry> entries = IsoPrimaryVolumeDirectoryReader.ReadFiles(rawImage, volume);

            ReferenceInventory reference = ReadReferenceInventory(referencePath);
            if (!string.Equals(reference.TargetId, TargetId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Reference inventory target_id must be '{TargetId}', found '{reference.TargetId ?? "<missing>"}'.");
            }

            if (reference.Files is null)
            {
                throw new InvalidDataException("The reference inventory does not contain a files array.");
            }

            InventoryComparisonResult comparison = InventoryComparison.Compare(entries, reference.Files);
            var inventory = new ManagedInventory(TargetId, sourceHash, entries);
            string inventoryJson = JsonSerializer.Serialize(inventory, JsonOptions) + Environment.NewLine;
            string reportText = FormatReport(referencePath, entries.Count, comparison);

            Directory.CreateDirectory(_scratchRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(inventoryPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            File.WriteAllText(inventoryPath, inventoryJson);
            File.WriteAllText(reportPath, reportText);

            output.Write(reportText);
            output.WriteLine($"Managed inventory: {Path.GetRelativePath(_workspaceRoot, inventoryPath)}");
            output.WriteLine($"Comparison report: {Path.GetRelativePath(_workspaceRoot, reportPath)}");
            return comparison.IsMatch ? 0 : 1;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException or JsonException or RegexMatchTimeoutException)
        {
            error.WriteLine(exception.Message);
            return 2;
        }
    }

    public static string ValidateImageProfile(string binPath)
    {
        var file = new FileInfo(binPath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The supplied BIN file does not exist.", binPath);
        }

        if (file.Length != ExpectedImageBytes)
        {
            throw new InvalidDataException(
                $"Unsupported BIN size: expected {ExpectedImageBytes} bytes for {TargetId}, found {file.Length}.");
        }

        using FileStream stream = file.OpenRead();
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(stream));
        if (!string.Equals(sha256, ExpectedImageSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported BIN SHA-256 for {TargetId}: {sha256}.");
        }

        return sha256;
    }

    public static void ValidateCue(string cuePath, string suppliedBinPath)
    {
        if (!File.Exists(cuePath))
        {
            throw new FileNotFoundException("The supplied CUE file does not exist.", cuePath);
        }

        string[] lines = File.ReadAllLines(cuePath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();
        if (lines.Length != 3)
        {
            throw new InvalidDataException("The supported CUE must contain exactly one FILE, one TRACK, and one INDEX directive.");
        }

        Match fileMatch = CueFileLine.Match(lines[0]);
        if (!fileMatch.Success || !CueTrackLine.IsMatch(lines[1]) || !CueIndexLine.IsMatch(lines[2]))
        {
            throw new InvalidDataException("Unsupported CUE layout; expected FILE \"...\" BINARY, TRACK 01 MODE2/2352, INDEX 01 00:00:00.");
        }

        string cueDirectory = Path.GetDirectoryName(Path.GetFullPath(cuePath))!;
        string referencedBinPath = Path.GetFullPath(Path.Combine(cueDirectory, fileMatch.Groups["path"].Value));
        if (!PathEquals(referencedBinPath, Path.GetFullPath(suppliedBinPath)))
        {
            throw new InvalidDataException("The CUE-referenced BIN path does not match the supplied BIN path.");
        }
    }

    private CommandOptions ParseArguments(string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], "inventory", StringComparison.Ordinal))
        {
            throw new ArgumentException(Usage);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 1; index < args.Length; index += 2)
        {
            string option = args[index];
            if (option is not ("--bin" or "--cue" or "--reference" or "--output" or "--report"))
            {
                throw new ArgumentException($"Unknown option '{option}'.\n{Usage}");
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Option '{option}' requires a value.\n{Usage}");
            }

            if (!values.TryAdd(option, args[index + 1]))
            {
                throw new ArgumentException($"Option '{option}' was supplied more than once.");
            }
        }

        if (!values.TryGetValue("--bin", out string? binPath) || !values.TryGetValue("--cue", out string? cuePath))
        {
            throw new ArgumentException($"Both --bin and --cue are required.\n{Usage}");
        }

        return new CommandOptions(
            binPath,
            cuePath,
            values.GetValueOrDefault("--reference"),
            values.GetValueOrDefault("--output"),
            values.GetValueOrDefault("--report"));
    }

    private string ResolveScratchOutput(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string relative = Path.GetRelativePath(_scratchRoot, fullPath);
        if (relative == "." || relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", PathComparison)
            || Path.IsPathRooted(relative))
        {
            throw new InvalidDataException("Inventory and report output paths must remain inside the repository scratch directory.");
        }

        return fullPath;
    }

    private static ReferenceInventory ReadReferenceInventory(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The reference inventory was not found.", path);
        }

        using FileStream stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<ReferenceInventory>(stream, JsonOptions)
            ?? throw new InvalidDataException("The reference inventory JSON is empty.");
    }

    private static string FormatReport(string referencePath, int managedCount, InventoryComparisonResult comparison)
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine($"Reference: {referencePath}");
        report.AppendLine($"Managed entries: {managedCount}");
        report.AppendLine($"Missing from managed: {comparison.MissingPaths.Length}");
        foreach (string path in comparison.MissingPaths)
        {
            report.AppendLine($"  missing: {path}");
        }

        report.AppendLine($"Extra in managed: {comparison.ExtraPaths.Length}");
        foreach (string path in comparison.ExtraPaths)
        {
            report.AppendLine($"  extra: {path}");
        }

        report.AppendLine($"Extent/length mismatches: {comparison.MismatchedEntries.Length}");
        foreach (InventoryMismatch mismatch in comparison.MismatchedEntries)
        {
            report.AppendLine(
                $"  mismatch: {mismatch.Path}: LBA managed={mismatch.ManagedLba} reference={mismatch.ReferenceLba}; " +
                $"bytes managed={mismatch.ManagedBytes} reference={mismatch.ReferenceBytes}");
        }

        report.AppendLine($"Non-comparable extracted lengths: {comparison.LengthsNotComparable.Length}");
        foreach (InventoryLengthNotComparable entry in comparison.LengthsNotComparable)
        {
            report.AppendLine(
                $"  non-comparable: {entry.Path}: reference type={entry.ReferenceType}; " +
                $"ISO logical bytes={entry.IsoLogicalBytes}; extracted bytes={entry.ExtractedBytes}");
        }

        string result = comparison.IsMatch
            ? comparison.LengthsNotComparable.IsEmpty
                ? "Result: match"
                : "Result: paths, LBAs, and comparable lengths match; mixed extracted lengths need format-aware interpretation."
            : "Result: comparable differences found; normalization was not adjusted.";
        report.AppendLine(result);
        return report.ToString();
    }

    private static void EnsureDistinctPaths(params string[] paths)
    {
        var seen = new HashSet<string>(PathComparer);
        foreach (string path in paths)
        {
            if (!seen.Add(Path.GetFullPath(path)))
            {
                throw new InvalidDataException("Input, reference, inventory, and report paths must all be distinct.");
            }
        }
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), PathComparison);

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private const string Usage = "Usage: inventory --bin <path> --cue <path> [--reference <inventory.json>] [--output <scratch-path>] [--report <scratch-path>]";

    private sealed record CommandOptions(
        string BinPath,
        string CuePath,
        string? ReferencePath,
        string? InventoryPath,
        string? ReportPath);

    private sealed record ReferenceInventory(
        [property: JsonPropertyName("target_id")] string? TargetId,
        [property: JsonPropertyName("files")] List<ReferenceInventoryEntry>? Files);

    private sealed record ManagedInventory(string TargetId, string SourceSha256, IReadOnlyList<IsoFileEntry> Files)
    {
        public int SchemaVersion { get; } = 1;

        public int FileCount => Files.Count;
    }
}
