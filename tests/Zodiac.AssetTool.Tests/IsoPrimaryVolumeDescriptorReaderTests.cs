using System.Buffers.Binary;
using Zodiac.AssetTool;
using Xunit;

namespace Zodiac.AssetTool.Tests;

public sealed class IsoPrimaryVolumeDescriptorReaderTests
{
    [Fact]
    public void Read_finds_pvd_in_valid_volume_descriptor_sequence()
    {
        byte[] pvd = CreatePrimaryVolumeDescriptor();
        using var image = CreateImage(24, pvd, CreateDescriptor(255));

        IsoPrimaryVolumeDescriptor result = Read(image);

        Assert.Equal(24u, result.VolumeSpaceSize);
        Assert.Equal(2048, result.LogicalBlockSize);
        Assert.Equal(20u, result.RootDirectory.ExtentLba);
        Assert.Equal(2048u, result.RootDirectory.DataLength);
        Assert.Equal(0, result.RootDirectory.ExtendedAttributeRecordLength);
    }

    [Fact]
    public void Read_skips_non_pvd_descriptor_before_pvd()
    {
        using var image = CreateImage(
            24,
            CreateDescriptor(0),
            CreatePrimaryVolumeDescriptor(),
            CreateDescriptor(255));

        IsoPrimaryVolumeDescriptor result = Read(image);

        Assert.Equal(24u, result.VolumeSpaceSize);
    }

    [Fact]
    public void Read_rejects_sequence_without_terminator()
    {
        using var image = CreateImage(
            24,
            new[] { CreatePrimaryVolumeDescriptor() }
                .Concat(Enumerable.Range(0, 7).Select(_ => CreateDescriptor(2)))
                .ToArray());

        Assert.Contains("missing its set terminator", Assert.Throws<InvalidDataException>(() => Read(image)).Message);
    }

    [Theory]
    [InlineData("identifier")]
    [InlineData("version")]
    public void Read_rejects_invalid_descriptor_identifier_or_version(string invalidField)
    {
        byte[] pvd = CreatePrimaryVolumeDescriptor();
        pvd[invalidField == "identifier" ? 1 : 6] = invalidField == "identifier" ? (byte)'X' : (byte)2;
        using var image = CreateImage(24, pvd, CreateDescriptor(255));

        Assert.Throws<InvalidDataException>(() => Read(image));
    }

    [Fact]
    public void Read_rejects_inconsistent_both_endian_volume_space_size()
    {
        byte[] pvd = CreatePrimaryVolumeDescriptor();
        pvd[87] ^= 1;
        using var image = CreateImage(24, pvd, CreateDescriptor(255));

        Assert.Contains("inconsistent both-endian", Assert.Throws<InvalidDataException>(() => Read(image)).Message);
    }

    [Fact]
    public void Read_rejects_unsupported_logical_block_size()
    {
        byte[] pvd = CreatePrimaryVolumeDescriptor();
        SetBothEndianUInt16(pvd, 128, 1024);
        using var image = CreateImage(24, pvd, CreateDescriptor(255));

        Assert.Contains("logical block size", Assert.Throws<InvalidDataException>(() => Read(image)).Message);
    }

    [Theory]
    [InlineData("record-length")]
    [InlineData("root-identifier")]
    [InlineData("not-directory")]
    public void Read_rejects_invalid_root_directory_record_structure(string invalidField)
    {
        byte[] pvd = CreatePrimaryVolumeDescriptor();
        if (invalidField == "record-length")
        {
            pvd[156] = 33;
        }
        else if (invalidField == "root-identifier")
        {
            pvd[189] = 1;
        }
        else
        {
            pvd[181] = 0;
        }

        using var image = CreateImage(24, pvd, CreateDescriptor(255));

        Assert.Throws<InvalidDataException>(() => Read(image));
    }

    [Theory]
    [InlineData("outside-volume")]
    [InlineData("inconsistent-extent")]
    [InlineData("extent-too-long")]
    [InlineData("volume-larger-than-image")]
    public void Read_rejects_invalid_root_or_volume_extents(string invalidField)
    {
        byte[] pvd = CreatePrimaryVolumeDescriptor();
        if (invalidField == "outside-volume")
        {
            SetBothEndianUInt32(pvd, 158, 24);
        }
        else if (invalidField == "inconsistent-extent")
        {
            pvd[165] ^= 1;
        }
        else if (invalidField == "extent-too-long")
        {
            SetBothEndianUInt32(pvd, 166, 10 * Mode2RawImageReader.UserDataSize);
        }
        else
        {
            SetBothEndianUInt32(pvd, 80, 25);
        }

        using var image = CreateImage(24, pvd, CreateDescriptor(255));

        Assert.Throws<InvalidDataException>(() => Read(image));
    }

    private static IsoPrimaryVolumeDescriptor Read(MemoryStream image)
    {
        image.Position = 0;
        return IsoPrimaryVolumeDescriptorReader.Read(new Mode2RawImageReader(image));
    }

    private static MemoryStream CreateImage(int sectorCount, params byte[][] descriptors)
    {
        using var image = new MemoryStream();
        for (int lba = 0; lba < sectorCount; lba++)
        {
            image.Write(CreateMode2Form1Sector());
        }

        for (int index = 0; index < descriptors.Length; index++)
        {
            image.Position = (16L + index) * Mode2RawImageReader.RawSectorSize + Mode2RawImageReader.UserDataOffset;
            image.Write(descriptors[index]);
        }

        image.Position = 0;
        return new MemoryStream(image.ToArray());
    }

    private static byte[] CreateDescriptor(byte type)
    {
        byte[] descriptor = new byte[Mode2RawImageReader.UserDataSize];
        descriptor[0] = type;
        "CD001"u8.CopyTo(descriptor.AsSpan(1));
        descriptor[6] = 1;
        return descriptor;
    }

    private static byte[] CreatePrimaryVolumeDescriptor()
    {
        byte[] descriptor = CreateDescriptor(1);
        SetBothEndianUInt32(descriptor, 80, 24);
        SetBothEndianUInt16(descriptor, 120, 1);
        SetBothEndianUInt16(descriptor, 124, 1);
        SetBothEndianUInt16(descriptor, 128, Mode2RawImageReader.UserDataSize);

        descriptor[156] = 34;
        SetBothEndianUInt32(descriptor, 158, 20);
        SetBothEndianUInt32(descriptor, 166, Mode2RawImageReader.UserDataSize);
        descriptor[181] = 0x02;
        SetBothEndianUInt16(descriptor, 184, 1);
        descriptor[188] = 1;
        descriptor[189] = 0;
        return descriptor;
    }

    private static byte[] CreateMode2Form1Sector()
    {
        var sector = new byte[Mode2RawImageReader.RawSectorSize];
        Array.Fill(sector, (byte)0xff, 1, 10);
        sector[15] = 2;
        sector[16] = 1;
        sector[17] = 2;
        sector[19] = 4;
        Array.Copy(sector, 16, sector, 20, 4);
        return sector;
    }

    private static void SetBothEndianUInt16(byte[] destination, int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination.AsSpan(offset, 2), value);
        BinaryPrimitives.WriteUInt16BigEndian(destination.AsSpan(offset + 2, 2), value);
    }

    private static void SetBothEndianUInt32(byte[] destination, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset, 4), value);
        BinaryPrimitives.WriteUInt32BigEndian(destination.AsSpan(offset + 4, 4), value);
    }
}