namespace Zodiac.AssetTool;

/// <summary>
/// Immutable metadata for one file in the ISO primary-volume directory tree.
/// </summary>
public sealed record IsoFileEntry(
    string RawIdentifier,
    string RelativePath,
    uint EffectiveExtentLba,
    uint ByteLength);