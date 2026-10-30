using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zodiac.Framework;

namespace Zodiac.Content;

public sealed record PreparedAssetEntry(AssetId Id, string RelativePath, long ByteLength, string Sha256);

/// <summary>A versioned mapping from stable asset IDs to prepared loose files.</summary>
public sealed class PreparedAssetManifest
{
    public const int CurrentSchemaVersion = 1;
    public const string FileName = "assets.manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public int SchemaVersion => CurrentSchemaVersion;
    public ImmutableArray<PreparedAssetEntry> Assets { get; }

    public PreparedAssetManifest(IEnumerable<PreparedAssetEntry> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var entries = ImmutableArray.CreateBuilder<PreparedAssetEntry>();
        var ids = new HashSet<AssetId>();
        foreach (PreparedAssetEntry entry in assets)
        {
            ArgumentNullException.ThrowIfNull(entry);
            if (string.IsNullOrWhiteSpace(entry.Id.Value))
            {
                throw new ArgumentException("Manifest entries must have a non-default asset ID.", nameof(assets));
            }

            string relativePath = ValidateRelativePath(entry.RelativePath);
            if (entry.ByteLength < 0)
            {
                throw new ArgumentException($"Manifest entry '{entry.Id}' has a negative byte length.", nameof(assets));
            }

            string hash = ValidateSha256(entry.Sha256, entry.Id);
            if (!ids.Add(entry.Id))
            {
                throw new ArgumentException($"Manifest contains duplicate asset ID '{entry.Id}'.", nameof(assets));
            }

            entries.Add(entry with { RelativePath = relativePath, Sha256 = hash });
        }

        Assets = entries.OrderBy(entry => entry.Id.Value, StringComparer.Ordinal).ToImmutableArray();
    }

    public void SaveToFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ManifestDocument document = ToDocument();
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, document, JsonOptions);
        stream.WriteByte((byte)'\n');
    }

    public static PreparedAssetManifest LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = File.OpenRead(path);
        ManifestDocument document = JsonSerializer.Deserialize<ManifestDocument>(stream, JsonOptions)
            ?? throw new InvalidDataException("Prepared asset manifest is empty.");
        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported prepared asset manifest schema version {document.SchemaVersion}; expected {CurrentSchemaVersion}.");
        }

        if (document.Assets is null)
        {
            throw new InvalidDataException("Prepared asset manifest has no assets array.");
        }

        try
        {
            return new PreparedAssetManifest(document.Assets.Select(entry => new PreparedAssetEntry(
                new AssetId(entry.Id ?? throw new InvalidDataException("Manifest asset ID is missing.")),
                entry.RelativePath ?? throw new InvalidDataException("Manifest asset path is missing."),
                entry.ByteLength,
                entry.Sha256 ?? throw new InvalidDataException("Manifest SHA-256 is missing."))));
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Prepared asset manifest contains an invalid entry.", exception);
        }
    }

    private ManifestDocument ToDocument() => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        Assets = Assets.Select(entry => new ManifestEntryDocument
        {
            Id = entry.Id.Value,
            RelativePath = entry.RelativePath,
            ByteLength = entry.ByteLength,
            Sha256 = entry.Sha256,
        }).ToList(),
    };

    internal static string ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\'))
        {
            throw new ArgumentException("Manifest paths must be relative and use '/' separators.", nameof(path));
        }

        string[] components = path.Split('/');
        foreach (string component in components)
        {
            if (component.Length == 0 || component is "." or ".." || component.EndsWith(' ') || component.EndsWith('.') ||
                component.Any(character => character < 0x20 || "<>:\"|?*".Contains(character)))
            {
                throw new ArgumentException("Manifest contains an invalid relative path component.", nameof(path));
            }

            string deviceName = component.Split('.')[0];
            if (deviceName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
                IsNumberedDeviceName(deviceName, "COM") || IsNumberedDeviceName(deviceName, "LPT"))
            {
                throw new ArgumentException("Manifest contains a reserved Windows device path component.", nameof(path));
            }
        }

        return string.Join('/', components);
    }

    private static bool IsNumberedDeviceName(string value, string prefix) =>
        value.Length == 4 && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && value[3] is >= '1' and <= '9';

    private static string ValidateSha256(string hash, AssetId id)
    {
        if (hash is null || hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException($"Manifest entry '{id}' must have a 64-character SHA-256 value.", nameof(hash));
        }

        return hash.ToLowerInvariant();
    }

    private sealed class ManifestDocument
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("assets")]
        public List<ManifestEntryDocument>? Assets { get; set; }
    }

    private sealed class ManifestEntryDocument
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("relativePath")]
        public string? RelativePath { get; set; }

        [JsonPropertyName("byteLength")]
        public long ByteLength { get; set; }

        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }
    }
}
