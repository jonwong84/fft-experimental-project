using System.Security.Cryptography;
using Fft.Core;

namespace Fft.Content;

/// <summary>Loads prepared loose files by stable ID and verifies them against a manifest.</summary>
/// <remarks>This store reads only files beneath a content root; it has no BIN, ISO, or C-reference dependency.</remarks>
public sealed class LooseAssetStore
{
    private readonly string _rootDirectory;
    private readonly Dictionary<AssetId, PreparedAssetEntry> _entries;

    public LooseAssetStore(string rootDirectory, PreparedAssetManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(manifest);
        _rootDirectory = Path.GetFullPath(rootDirectory);
        _entries = manifest.Assets.ToDictionary(entry => entry.Id);
    }

    public static LooseAssetStore Load(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        string root = Path.GetFullPath(rootDirectory);
        PreparedAssetManifest manifest = PreparedAssetManifest.LoadFromFile(Path.Combine(root, PreparedAssetManifest.FileName));
        return new LooseAssetStore(root, manifest);
    }

    public byte[] ReadAllBytes(AssetId id) => ReadAllBytes(id, int.MaxValue);

    /// <summary>Rejects an over-budget manifest/file before allocating the asset buffer.</summary>
    public byte[] ReadAllBytes(AssetId id, int maxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        if (!_entries.TryGetValue(id, out PreparedAssetEntry? entry))
        {
            throw new KeyNotFoundException($"No prepared file is mapped for asset ID '{id}'.");
        }

        if (entry.ByteLength > maxBytes)
            throw new InvalidDataException($"Prepared asset '{id}' exceeds its {maxBytes}-byte load budget.");

        string fullPath = Path.GetFullPath(Path.Combine(
            _rootDirectory, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        string rootPrefix = Path.EndsInDirectorySeparator(_rootDirectory)
            ? _rootDirectory
            : _rootDirectory + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, PathComparison))
        {
            throw new InvalidDataException($"Manifest path '{entry.RelativePath}' resolves outside the content directory.");
        }

        var fileInfo = new FileInfo(fullPath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException($"Prepared asset '{id}' was not found.", fullPath);
        }

        if (fileInfo.Length != entry.ByteLength || fileInfo.Length > int.MaxValue)
        {
            throw new InvalidDataException(
                $"Prepared asset '{id}' has {fileInfo.Length} bytes; manifest expects {entry.ByteLength}.");
        }

        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length != entry.ByteLength || stream.Length > maxBytes)
            throw new InvalidDataException($"Prepared asset '{id}' changed size before loading.");
        byte[] data = new byte[checked((int)entry.ByteLength)];
        stream.ReadExactly(data);
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException(
                $"Prepared asset '{id}' changed size while it was being read; manifest expects {entry.ByteLength} bytes.");
        }

        string actualHash = Convert.ToHexStringLower(SHA256.HashData(data));
        if (!string.Equals(actualHash, entry.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Prepared asset '{id}' does not match its manifest SHA-256.");
        }

        return data;
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}
