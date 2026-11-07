using System.Buffers.Binary;

namespace Zodiac.AssetTool;

/// <summary>
/// Locates and validates the ISO9660 Primary Volume Descriptor in a raw Mode 2 image.
/// </summary>
public static class IsoPrimaryVolumeDescriptorReader
{
    private const int FirstVolumeDescriptorLba = 16;
    private const byte PrimaryVolumeDescriptorType = 1;
    private const byte VolumeDescriptorSetTerminatorType = 255;
    private const int MinimumRootDirectoryRecordLength = 34;

    public static IsoPrimaryVolumeDescriptor Read(Mode2RawImageReader image)
    {
        ArgumentNullException.ThrowIfNull(image);

        IsoPrimaryVolumeDescriptor? primaryVolumeDescriptor = null;
        for (long lba = FirstVolumeDescriptorLba; lba < image.SectorCount; lba++)
        {
            byte[] sector = image.ReadUserData(checked((int)lba));
            ValidateDescriptorHeader(sector, checked((int)lba));

            if (sector[0] == PrimaryVolumeDescriptorType)
            {
                if (primaryVolumeDescriptor is not null)
                {
                    throw new InvalidDataException("The volume descriptor sequence contains multiple PVDs.");
                }

                primaryVolumeDescriptor = ParsePrimaryVolumeDescriptor(sector, image.SectorCount);
            }
            else if (sector[0] == VolumeDescriptorSetTerminatorType)
            {
                return primaryVolumeDescriptor
                    ?? throw new InvalidDataException("The volume descriptor sequence ended before a PVD was found.");
            }
        }

        throw new InvalidDataException("The volume descriptor sequence is missing its set terminator.");
    }

    private static void ValidateDescriptorHeader(ReadOnlySpan<byte> descriptor, int lba)
    {
        if (!descriptor.Slice(1, 5).SequenceEqual("CD001"u8))
        {
            throw new InvalidDataException($"Invalid volume descriptor identifier at LBA {lba}.");
        }

        if (descriptor[6] != 1)
        {
            throw new InvalidDataException($"Unsupported volume descriptor version at LBA {lba}.");
        }
    }

    private static IsoPrimaryVolumeDescriptor ParsePrimaryVolumeDescriptor(ReadOnlySpan<byte> descriptor, long imageSectorCount)
    {
        uint volumeSpaceSize = ReadBothEndianUInt32(descriptor.Slice(80, 8), "volume-space size");
        ushort volumeSetSize = ReadBothEndianUInt16(descriptor.Slice(120, 4), "volume-set size");
        ushort volumeSequenceNumber = ReadBothEndianUInt16(descriptor.Slice(124, 4), "volume sequence number");
        ushort logicalBlockSize = ReadBothEndianUInt16(descriptor.Slice(128, 4), "logical block size");
        if (logicalBlockSize != Mode2RawImageReader.UserDataSize)
        {
            throw new InvalidDataException(
                $"Unsupported ISO logical block size {logicalBlockSize}; expected {Mode2RawImageReader.UserDataSize}.");
        }

        if (volumeSetSize == 0 || volumeSequenceNumber == 0 || volumeSequenceNumber > volumeSetSize)
        {
            throw new InvalidDataException("The PVD volume-set and volume-sequence values are invalid.");
        }

        var rootDirectory = ParseRootDirectoryRecord(descriptor.Slice(156), volumeSequenceNumber);
        if (volumeSpaceSize == 0)
        {
            throw new InvalidDataException("The PVD volume-space size must be nonzero.");
        }

        if (volumeSpaceSize > imageSectorCount)
        {
            throw new InvalidDataException("The PVD volume-space size exceeds the supplied image bounds.");
        }

        if (volumeSpaceSize > int.MaxValue)
        {
            throw new InvalidDataException("The PVD volume-space size exceeds the supported track-relative LBA range.");
        }

        ValidateExtentBounds(rootDirectory, volumeSpaceSize);
        return new IsoPrimaryVolumeDescriptor(volumeSpaceSize, volumeSequenceNumber, logicalBlockSize, rootDirectory);
    }

    private static IsoRootDirectoryRecord ParseRootDirectoryRecord(ReadOnlySpan<byte> recordArea, ushort volumeSequenceNumber)
    {
        int recordLength = recordArea[0];
        if (recordLength < MinimumRootDirectoryRecordLength || recordLength > recordArea.Length)
        {
            throw new InvalidDataException("The PVD root directory record has an invalid length.");
        }

        ReadOnlySpan<byte> record = recordArea[..recordLength];
        byte identifierLength = record[32];
        int minimumLength = 33 + identifierLength + (identifierLength % 2 == 0 ? 1 : 0);
        if (identifierLength != 1 || record[33] != 0 || recordLength < Math.Max(MinimumRootDirectoryRecordLength, minimumLength))
        {
            throw new InvalidDataException("The PVD root directory record has an invalid root identifier.");
        }

        if ((record[25] & 0x02) == 0)
        {
            throw new InvalidDataException("The PVD root directory record is not marked as a directory.");
        }

        if (record[26] != 0 || record[27] != 0)
        {
            throw new InvalidDataException("Interleaved root directory records are not supported.");
        }

        uint extentLba = ReadBothEndianUInt32(record.Slice(2, 8), "root directory extent LBA");
        uint dataLength = ReadBothEndianUInt32(record.Slice(10, 8), "root directory data length");
        ushort rootVolumeSequenceNumber = ReadBothEndianUInt16(record.Slice(28, 4), "root directory volume sequence number");
        if (rootVolumeSequenceNumber != volumeSequenceNumber)
        {
            throw new InvalidDataException("The PVD root directory volume sequence does not match the PVD.");
        }

        return new IsoRootDirectoryRecord(extentLba, dataLength, record[1]);
    }

    private static void ValidateExtentBounds(IsoRootDirectoryRecord rootDirectory, uint volumeSpaceSize)
    {
        if (rootDirectory.DataLength == 0)
        {
            throw new InvalidDataException("The PVD root directory extent must not be empty.");
        }

        ulong extentStart = (ulong)rootDirectory.ExtentLba + rootDirectory.ExtendedAttributeRecordLength;
        ulong extentBlocks = ((ulong)rootDirectory.DataLength + Mode2RawImageReader.UserDataSize - 1)
            / Mode2RawImageReader.UserDataSize;
        if (extentStart >= volumeSpaceSize || extentStart + extentBlocks > volumeSpaceSize)
        {
            throw new InvalidDataException("The PVD root directory extent lies outside the declared volume.");
        }
    }

    private static uint ReadBothEndianUInt32(ReadOnlySpan<byte> value, string fieldName)
    {
        uint littleEndian = BinaryPrimitives.ReadUInt32LittleEndian(value[..4]);
        uint bigEndian = BinaryPrimitives.ReadUInt32BigEndian(value[4..]);
        if (littleEndian != bigEndian)
        {
            throw new InvalidDataException($"The PVD {fieldName} has inconsistent both-endian values.");
        }

        return littleEndian;
    }

    private static ushort ReadBothEndianUInt16(ReadOnlySpan<byte> value, string fieldName)
    {
        ushort littleEndian = BinaryPrimitives.ReadUInt16LittleEndian(value[..2]);
        ushort bigEndian = BinaryPrimitives.ReadUInt16BigEndian(value[2..]);
        if (littleEndian != bigEndian)
        {
            throw new InvalidDataException($"The PVD {fieldName} has inconsistent both-endian values.");
        }

        return littleEndian;
    }
}