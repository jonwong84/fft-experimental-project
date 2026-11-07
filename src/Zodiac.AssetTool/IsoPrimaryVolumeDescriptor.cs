namespace Zodiac.AssetTool;

/// <summary>
/// Validated ISO9660 metadata needed to begin primary-volume directory traversal.
/// </summary>
public sealed record IsoPrimaryVolumeDescriptor(
    uint VolumeSpaceSize,
    ushort VolumeSequenceNumber,
    ushort LogicalBlockSize,
    IsoRootDirectoryRecord RootDirectory);

/// <summary>
/// Validated extent metadata from the PVD root directory record.
/// </summary>
public readonly record struct IsoRootDirectoryRecord(
    uint ExtentLba,
    uint DataLength,
    byte ExtendedAttributeRecordLength);