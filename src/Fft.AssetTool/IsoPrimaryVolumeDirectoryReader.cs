using System.Collections.Immutable;
using System.Text;

namespace Fft.AssetTool;

/// <summary>
/// Recursively enumerates file records from an already validated ISO primary volume.
/// </summary>
/// <remarks>
/// Normalization preserves identifier casing and trailing dots, and removes only a
/// final semicolon followed by one or more decimal version digits. This policy is
/// explicit but has not been compared with the reference extractor.
/// </remarks>
public static class IsoPrimaryVolumeDirectoryReader
{
    private const byte DirectoryFlag = 0x02;
    private const byte MultiExtentFlag = 0x80;
    private const int MinimumDirectoryRecordLength = 34;

    public static ImmutableArray<IsoFileEntry> ReadFiles(
        Mode2RawImageReader image,
        IsoPrimaryVolumeDescriptor volume)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(volume);
        ValidateVolume(image, volume);

        var files = ImmutableArray.CreateBuilder<IsoFileEntry>();
        var pendingDirectories = new Stack<DirectoryWork>();
        var visitedDirectoryExtents = new HashSet<uint>();
        var encounteredPaths = new HashSet<string>(StringComparer.Ordinal);

        IsoRootDirectoryRecord root = volume.RootDirectory;
        uint rootEffectiveLba = checked(root.ExtentLba + root.ExtendedAttributeRecordLength);
        visitedDirectoryExtents.Add(rootEffectiveLba);
        pendingDirectories.Push(new DirectoryWork(string.Empty, rootEffectiveLba, root.DataLength));

        while (pendingDirectories.TryPop(out DirectoryWork directory))
        {
            ReadDirectory(image, volume, directory, pendingDirectories, visitedDirectoryExtents, encounteredPaths, files);
        }

        return files.ToImmutable();
    }

    private static void ValidateVolume(Mode2RawImageReader image, IsoPrimaryVolumeDescriptor volume)
    {
        if (volume.LogicalBlockSize != Mode2RawImageReader.UserDataSize)
        {
            throw new InvalidDataException("The PVD logical block size is not supported by the raw image reader.");
        }

        if (volume.VolumeSpaceSize == 0 || volume.VolumeSpaceSize > image.SectorCount)
        {
            throw new InvalidDataException("The PVD volume-space size is outside the supplied image bounds.");
        }

        if (volume.VolumeSequenceNumber == 0)
        {
            throw new InvalidDataException("The PVD volume sequence number must be nonzero.");
        }

        ValidateDirectoryExtent(
            volume.RootDirectory.ExtentLba,
            volume.RootDirectory.ExtendedAttributeRecordLength,
            volume.RootDirectory.DataLength,
            volume.VolumeSpaceSize,
            "PVD root directory");
    }

    private static void ReadDirectory(
        Mode2RawImageReader image,
        IsoPrimaryVolumeDescriptor volume,
        DirectoryWork directory,
        Stack<DirectoryWork> pendingDirectories,
        HashSet<uint> visitedDirectoryExtents,
        HashSet<string> encounteredPaths,
        ImmutableArray<IsoFileEntry>.Builder files)
    {
        uint remainingBytes = directory.ByteLength;
        uint currentLba = directory.EffectiveExtentLba;

        while (remainingBytes > 0)
        {
            byte[] block = image.ReadUserData(checked((int)currentLba));
            int bytesInBlock = (int)Math.Min((uint)Mode2RawImageReader.UserDataSize, remainingBytes);
            int position = 0;

            while (position < bytesInBlock)
            {
                if (block[position] == 0)
                {
                    break;
                }

                int recordLength = block[position];
                int bytesRemainingInBlock = bytesInBlock - position;
                if (recordLength < MinimumDirectoryRecordLength || recordLength > bytesRemainingInBlock)
                {
                    throw new InvalidDataException(
                        $"Invalid directory record length at LBA {currentLba}, block offset {position}.");
                }

                IsoDirectoryRecord record = ParseDirectoryRecord(
                    block.AsSpan(position, recordLength), volume, currentLba, position);
                position += recordLength;

                if (record.IsCurrentOrParent)
                {
                    continue;
                }

                string normalizedIdentifier = NormalizeIdentifier(record.RawIdentifier);
                string relativePath = directory.RelativePath.Length == 0
                    ? normalizedIdentifier
                    : $"{directory.RelativePath}/{normalizedIdentifier}";
                if (!encounteredPaths.Add(relativePath))
                {
                    throw new InvalidDataException($"Duplicate normalized ISO path '{relativePath}'.");
                }

                if (record.IsDirectory)
                {
                    if (!visitedDirectoryExtents.Add(record.EffectiveExtentLba))
                    {
                        throw new InvalidDataException(
                            $"Repeated directory extent LBA {record.EffectiveExtentLba} would create a directory cycle or alias.");
                    }

                    pendingDirectories.Push(new DirectoryWork(
                        relativePath,
                        record.EffectiveExtentLba,
                        record.ByteLength));
                }
                else
                {
                    files.Add(new IsoFileEntry(
                        record.RawIdentifier,
                        relativePath,
                        record.EffectiveExtentLba,
                        record.ByteLength));
                }
            }

            remainingBytes -= (uint)bytesInBlock;
            currentLba++;
        }
    }

    private static IsoDirectoryRecord ParseDirectoryRecord(
        ReadOnlySpan<byte> record,
        IsoPrimaryVolumeDescriptor volume,
        uint directoryLba,
        int blockOffset)
    {
        byte identifierLength = record[32];
        int minimumLength = 33 + identifierLength + (identifierLength % 2 == 0 ? 1 : 0);
        if (identifierLength == 0 || record.Length < Math.Max(MinimumDirectoryRecordLength, minimumLength))
        {
            throw InvalidRecord(directoryLba, blockOffset, "invalid identifier length");
        }

        ReadOnlySpan<byte> identifierBytes = record.Slice(33, identifierLength);
        bool isCurrentOrParent = identifierLength == 1 && identifierBytes[0] is 0 or 1;
        string rawIdentifier;
        if (isCurrentOrParent)
        {
            rawIdentifier = identifierBytes[0] == 0 ? "." : "..";
        }
        else
        {
            if (identifierBytes.Contains((byte)'/') || identifierBytes.Contains((byte)'\\'))
            {
                throw InvalidRecord(directoryLba, blockOffset, "identifier contains a path separator");
            }

            if (identifierBytes.ContainsAnyInRange((byte)0x80, byte.MaxValue))
            {
                throw InvalidRecord(directoryLba, blockOffset, "identifier is not ASCII");
            }

            rawIdentifier = Encoding.ASCII.GetString(identifierBytes);
            if (NormalizeIdentifier(rawIdentifier).Length == 0)
            {
                throw InvalidRecord(directoryLba, blockOffset, "identifier normalizes to an empty path component");
            }
        }

        if ((record[25] & MultiExtentFlag) != 0)
        {
            throw InvalidRecord(directoryLba, blockOffset, "multi-extent records are not supported");
        }

        if (record[26] != 0 || record[27] != 0)
        {
            throw InvalidRecord(directoryLba, blockOffset, "interleaved records are not supported");
        }

        uint extentLba = ReadBothEndianUInt32(record.Slice(2, 8), directoryLba, blockOffset, "extent LBA");
        uint byteLength = ReadBothEndianUInt32(record.Slice(10, 8), directoryLba, blockOffset, "data length");
        ushort volumeSequenceNumber = ReadBothEndianUInt16(record.Slice(28, 4), directoryLba, blockOffset);
        if (volumeSequenceNumber != volume.VolumeSequenceNumber)
        {
            throw InvalidRecord(directoryLba, blockOffset, "volume sequence number does not match the PVD");
        }

        bool isDirectory = (record[25] & DirectoryFlag) != 0;
        if (isCurrentOrParent && !isDirectory)
        {
            throw InvalidRecord(directoryLba, blockOffset, "current/parent record is not marked as a directory");
        }

        if (isDirectory && byteLength == 0)
        {
            throw InvalidRecord(directoryLba, blockOffset, "directory extent is empty");
        }

        uint extendedAttributeRecordLength = record[1];
        ValidateDirectoryExtent(
            extentLba,
            extendedAttributeRecordLength,
            byteLength,
            volume.VolumeSpaceSize,
            "directory record");
        uint effectiveExtentLba = (uint)((ulong)extentLba + extendedAttributeRecordLength);

        return new IsoDirectoryRecord(rawIdentifier, isCurrentOrParent, isDirectory, effectiveExtentLba, byteLength);
    }

    private static void ValidateDirectoryExtent(
        uint extentLba,
        uint extendedAttributeRecordLength,
        uint byteLength,
        uint volumeSpaceSize,
        string description)
    {
        ulong effectiveLba = (ulong)extentLba + extendedAttributeRecordLength;
        ulong blockCount = ((ulong)byteLength + Mode2RawImageReader.UserDataSize - 1)
            / Mode2RawImageReader.UserDataSize;
        if (effectiveLba > volumeSpaceSize || (byteLength > 0 && effectiveLba + blockCount > volumeSpaceSize))
        {
            throw new InvalidDataException($"The {description} extent lies outside the PVD volume bounds.");
        }

        if (byteLength == 0 && effectiveLba >= volumeSpaceSize)
        {
            throw new InvalidDataException($"The {description} extent lies outside the PVD volume bounds.");
        }
    }

    private static string NormalizeIdentifier(string rawIdentifier)
    {
        int semicolon = rawIdentifier.LastIndexOf(';');
        if (semicolon < 0 || semicolon == rawIdentifier.Length - 1)
        {
            return rawIdentifier;
        }

        for (int index = semicolon + 1; index < rawIdentifier.Length; index++)
        {
            if (!char.IsAsciiDigit(rawIdentifier[index]))
            {
                return rawIdentifier;
            }
        }

        return rawIdentifier[..semicolon];
    }

    private static uint ReadBothEndianUInt32(
        ReadOnlySpan<byte> value,
        uint directoryLba,
        int blockOffset,
        string fieldName)
    {
        uint littleEndian = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(value[..4]);
        uint bigEndian = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(value[4..]);
        if (littleEndian != bigEndian)
        {
            throw InvalidRecord(directoryLba, blockOffset, $"{fieldName} has inconsistent both-endian values");
        }

        return littleEndian;
    }

    private static ushort ReadBothEndianUInt16(ReadOnlySpan<byte> value, uint directoryLba, int blockOffset)
    {
        ushort littleEndian = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(value[..2]);
        ushort bigEndian = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(value[2..]);
        if (littleEndian != bigEndian)
        {
            throw InvalidRecord(directoryLba, blockOffset, "volume sequence number has inconsistent both-endian values");
        }

        return littleEndian;
    }

    private static InvalidDataException InvalidRecord(uint directoryLba, int blockOffset, string reason) =>
        new($"Invalid ISO directory record at LBA {directoryLba}, block offset {blockOffset}: {reason}.");

    private readonly record struct DirectoryWork(string RelativePath, uint EffectiveExtentLba, uint ByteLength);

    private readonly record struct IsoDirectoryRecord(
        string RawIdentifier,
        bool IsCurrentOrParent,
        bool IsDirectory,
        uint EffectiveExtentLba,
        uint ByteLength);
}