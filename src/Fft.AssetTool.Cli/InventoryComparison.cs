using System.Collections.Immutable;
using Fft.AssetTool;

namespace Fft.AssetTool.Cli;

public sealed record ReferenceInventoryEntry(string Path, uint Lba, uint Bytes, string Type = "data");

public sealed record InventoryMismatch(
    string Path,
    uint ManagedLba,
    uint ReferenceLba,
    uint ManagedBytes,
    uint ReferenceBytes);

public sealed record InventoryComparisonResult(
    ImmutableArray<string> MissingPaths,
    ImmutableArray<string> ExtraPaths,
    ImmutableArray<InventoryMismatch> MismatchedEntries,
    ImmutableArray<InventoryLengthNotComparable> LengthsNotComparable)
{
    public bool IsMatch => MissingPaths.IsEmpty && ExtraPaths.IsEmpty && MismatchedEntries.IsEmpty;
}

public sealed record InventoryLengthNotComparable(
    string Path,
    string ReferenceType,
    uint IsoLogicalBytes,
    uint ExtractedBytes);

public static class InventoryComparison
{
    public static InventoryComparisonResult Compare(
        IEnumerable<IsoFileEntry> managedEntries,
        IEnumerable<ReferenceInventoryEntry> referenceEntries)
    {
        ArgumentNullException.ThrowIfNull(managedEntries);
        ArgumentNullException.ThrowIfNull(referenceEntries);

        var managedByPath = managedEntries.ToDictionary(entry => entry.RelativePath, StringComparer.Ordinal);
        var referenceByPath = referenceEntries.ToDictionary(entry => entry.Path, StringComparer.Ordinal);
        var missing = ImmutableArray.CreateBuilder<string>();
        var extra = ImmutableArray.CreateBuilder<string>();
        var mismatched = ImmutableArray.CreateBuilder<InventoryMismatch>();
        var lengthsNotComparable = ImmutableArray.CreateBuilder<InventoryLengthNotComparable>();

        foreach ((string path, ReferenceInventoryEntry reference) in referenceByPath.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (!managedByPath.TryGetValue(path, out IsoFileEntry? managed))
            {
                missing.Add(path);
                continue;
            }

            bool lbaMismatch = managed.EffectiveExtentLba != reference.Lba;
            bool mixedLength = !string.Equals(reference.Type, "data", StringComparison.OrdinalIgnoreCase);
            bool comparableLengthMismatch = !mixedLength && managed.ByteLength != reference.Bytes;

            if (mixedLength)
            {
                lengthsNotComparable.Add(new InventoryLengthNotComparable(
                    path, reference.Type, managed.ByteLength, reference.Bytes));
            }

            if (lbaMismatch || comparableLengthMismatch)
            {
                mismatched.Add(new InventoryMismatch(
                    path,
                    managed.EffectiveExtentLba,
                    reference.Lba,
                    managed.ByteLength,
                    reference.Bytes));
            }
        }

        foreach (string path in managedByPath.Keys.Except(referenceByPath.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            extra.Add(path);
        }

        return new InventoryComparisonResult(
            missing.ToImmutable(), extra.ToImmutable(), mismatched.ToImmutable(), lengthsNotComparable.ToImmutable());
    }
}
