using System.Buffers.Binary;
using System.Collections.Immutable;
using Zodiac.AssetTool;
using Xunit;

namespace Zodiac.AssetTool.Tests;

public sealed class IsoPrimaryVolumeDirectoryReaderTests
{
    [Fact]
    public void ReadFiles_enumerates_root_and_nested_files_and_skips_dot_records()
    {
        using var image = new DirectoryImage(rootLength: 2048);
        image.SetRootRecords(
            SpecialDirectoryRecord(0, 20, 2048),
            SpecialDirectoryRecord(1, 20, 2048),
            DirectoryRecord("ROOT.BIN;1", 30, 5),
            DirectoryRecord("SUBDIR", 21, 2048, isDirectory: true));
        image.SetDirectoryRecords(21,
            SpecialDirectoryRecord(0, 21, 2048),
            SpecialDirectoryRecord(1, 20, 2048),
            DirectoryRecord("NESTED.DAT;2", 31, 7));

        ImmutableArray<IsoFileEntry> entries = image.ReadFiles();

        Assert.Collection(entries,
            entry =>
            {
                Assert.Equal("ROOT.BIN;1", entry.RawIdentifier);
                Assert.Equal("ROOT.BIN", entry.RelativePath);
                Assert.Equal(30u, entry.EffectiveExtentLba);
                Assert.Equal(5u, entry.ByteLength);
            },
            entry =>
            {
                Assert.Equal("NESTED.DAT;2", entry.RawIdentifier);
                Assert.Equal("SUBDIR/NESTED.DAT", entry.RelativePath);
                Assert.Equal(31u, entry.EffectiveExtentLba);
                Assert.Equal(7u, entry.ByteLength);
            });
    }

    [Fact]
    public void ReadFiles_includes_empty_files()
    {
        using var image = new DirectoryImage(rootLength: 2048);
        image.SetRootRecords(DirectoryRecord("EMPTY.DAT;1", 30, 0));

        IsoFileEntry entry = Assert.Single(image.ReadFiles());

        Assert.Equal("EMPTY.DAT;1", entry.RawIdentifier);
        Assert.Equal("EMPTY.DAT", entry.RelativePath);
        Assert.Equal(30u, entry.EffectiveExtentLba);
        Assert.Equal(0u, entry.ByteLength);
    }

    [Fact]
    public void ReadFiles_applies_extended_attribute_length_to_effective_extent()
    {
        using var image = new DirectoryImage(rootLength: 2048);
        image.SetRootRecords(DirectoryRecord("WITH.ATTR;1", 30, 8, extendedAttributes: 2));

        IsoFileEntry entry = Assert.Single(image.ReadFiles());

        Assert.Equal(32u, entry.EffectiveExtentLba);
    }

    [Fact]
    public void ReadFiles_treats_zero_record_length_as_padding_to_next_block()
    {
        using var image = new DirectoryImage(rootLength: 4096);
        image.SetRootRecords(DirectoryRecord("FIRST.BIN;1", 30, 1));
        image.SetRootRecordsAt(Mode2RawImageReader.UserDataSize, DirectoryRecord("SECOND.BIN;1", 31, 2));

        ImmutableArray<IsoFileEntry> entries = image.ReadFiles();

        Assert.Equal(new[] { "FIRST.BIN", "SECOND.BIN" }, entries.Select(entry => entry.RelativePath));
    }

    [Fact]
    public void ReadFiles_preserves_case_and_non_numeric_version_suffixes()
    {
        using var image = new DirectoryImage(rootLength: 2048);
        image.SetRootRecords(
            DirectoryRecord("MiXeD;12", 30, 1),
            DirectoryRecord("NO-VERSION;X", 31, 1),
            DirectoryRecord("TRAILING.", 32, 1));

        ImmutableArray<IsoFileEntry> entries = image.ReadFiles();

        Assert.Equal(new[] { "MiXeD", "NO-VERSION;X", "TRAILING." }, entries.Select(entry => entry.RelativePath));
        Assert.Equal(new[] { "MiXeD;12", "NO-VERSION;X", "TRAILING." }, entries.Select(entry => entry.RawIdentifier));
    }

    [Theory]
    [InlineData("short-record")]
    [InlineData("crosses-block")]
    [InlineData("zero-identifier")]
    [InlineData("path-separator")]
    public void ReadFiles_rejects_invalid_record_length_or_identifier(string invalidCase)
    {
        using var image = new DirectoryImage(rootLength: invalidCase == "crosses-block" ? 4096u : 2048u);
        if (invalidCase == "crosses-block")
        {
            byte[][] prefixRecords = Enumerable.Range(0, 8)
                .Select(index => DirectoryRecord(
                    $"F{index:D2}{new string('A', index < 7 ? 217 : 215)}",
                    30,
                    1))
                .ToArray();
            image.SetRootRecords(prefixRecords);
            image.SetRootByteAt(2030, 40);
        }
        else if (invalidCase == "zero-identifier")
        {
            image.SetRootRecords(RawDirectoryRecord([], 30, 1));
        }
        else if (invalidCase == "path-separator")
        {
            image.SetRootRecords(RawDirectoryRecord("BAD/NAME"u8.ToArray(), 30, 1));
        }
        else
        {
            image.SetRootRecords([new byte[] { 33 }]);
        }

        Assert.Throws<InvalidDataException>(() => image.ReadFiles());
    }

    [Theory]
    [InlineData("extent-endian")]
    [InlineData("length-endian")]
    [InlineData("sequence-endian")]
    [InlineData("wrong-sequence")]
    public void ReadFiles_rejects_invalid_both_endian_or_volume_sequence_fields(string invalidCase)
    {
        byte[] record = DirectoryRecord("ENTRY.BIN;1", 30, 2);
        switch (invalidCase)
        {
            case "extent-endian":
                record[9] ^= 1;
                break;
            case "length-endian":
                record[17] ^= 1;
                break;
            case "sequence-endian":
                record[31] ^= 1;
                break;
            case "wrong-sequence":
                SetBothEndianUInt16(record, 28, 2);
                break;
        }

        using var image = new DirectoryImage(rootLength: 2048);
        image.SetRootRecords(record);

        Assert.Throws<InvalidDataException>(() => image.ReadFiles());
    }

    [Theory]
    [InlineData("file-outside-volume")]
    [InlineData("directory-outside-volume")]
    public void ReadFiles_rejects_out_of_volume_file_and_directory_extents(string invalidCase)
    {
        using var image = new DirectoryImage(rootLength: 2048, volumeSpaceSize: 40);
        image.SetRootRecords(invalidCase == "file-outside-volume"
            ? DirectoryRecord("BAD.BIN;1", 40, 1)
            : DirectoryRecord("BADDIR", 39, 4096, isDirectory: true));

        Assert.Throws<InvalidDataException>(() => image.ReadFiles());
    }

    [Theory]
    [InlineData("multi-extent")]
    [InlineData("interleaved")]
    public void ReadFiles_rejects_multi_extent_and_interleaved_records(string invalidCase)
    {
        byte[] record = DirectoryRecord("UNSUPPORTED.BIN;1", 30, 1);
        if (invalidCase == "multi-extent")
        {
            record[25] |= 0x80;
        }
        else
        {
            record[26] = 1;
        }

        using var image = new DirectoryImage(rootLength: 2048);
        image.SetRootRecords(record);

        Assert.Throws<InvalidDataException>(() => image.ReadFiles());
    }

    [Fact]
    public void ReadFiles_rejects_repeated_directory_extent()
    {
        using var image = new DirectoryImage(rootLength: 2048);
        image.SetRootRecords(DirectoryRecord("CYCLE", 20, 2048, isDirectory: true));

        Assert.Contains("Repeated directory extent", Assert.Throws<InvalidDataException>(() => image.ReadFiles()).Message);
    }

    [Fact]
    public void ReadFiles_rejects_duplicate_normalized_paths()
    {
        using var image = new DirectoryImage(rootLength: 2048);
        image.SetRootRecords(
            DirectoryRecord("SAME.BIN;1", 30, 1),
            DirectoryRecord("SAME.BIN;2", 31, 1));

        Assert.Contains("Duplicate normalized", Assert.Throws<InvalidDataException>(() => image.ReadFiles()).Message);
    }

    private static byte[] DirectoryRecord(
        string identifier,
        uint extentLba,
        uint byteLength,
        bool isDirectory = false,
        byte extendedAttributes = 0)
    {
        return RawDirectoryRecord(
            System.Text.Encoding.ASCII.GetBytes(identifier), extentLba, byteLength, isDirectory, extendedAttributes);
    }

    private static byte[] SpecialDirectoryRecord(byte identifier, uint extentLba, uint byteLength) =>
        RawDirectoryRecord([identifier], extentLba, byteLength, isDirectory: true);

    private static byte[] RawDirectoryRecord(
        byte[] identifier,
        uint extentLba,
        uint byteLength,
        bool isDirectory = false,
        byte extendedAttributes = 0)
    {
        int recordLength = 33 + identifier.Length + (identifier.Length % 2 == 0 ? 1 : 0);
        byte[] record = new byte[recordLength];
        record[0] = (byte)recordLength;
        record[1] = extendedAttributes;
        SetBothEndianUInt32(record, 2, extentLba);
        SetBothEndianUInt32(record, 10, byteLength);
        record[25] = isDirectory ? (byte)0x02 : (byte)0;
        SetBothEndianUInt16(record, 28, 1);
        record[32] = (byte)identifier.Length;
        identifier.CopyTo(record, 33);
        return record;
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

    private sealed class DirectoryImage : IDisposable
    {
        private readonly byte[][] _sectors;
        private readonly MemoryStream _stream;
        private readonly uint _volumeSpaceSize;
        private readonly uint _rootLength;

        public DirectoryImage(uint rootLength, uint volumeSpaceSize = 40)
        {
            _volumeSpaceSize = volumeSpaceSize;
            _rootLength = rootLength;
            _sectors = Enumerable.Range(0, (int)volumeSpaceSize).Select(_ => CreateMode2Sector()).ToArray();
            WriteUserData(16, CreatePvd());
            WriteUserData(17, CreateDescriptor(255));
            _stream = new MemoryStream(_sectors.SelectMany(sector => sector).ToArray());
        }

        public void SetRootRecords(params byte[][] records) => SetRecords(20, _rootLength, 0, records);

        public void SetRootRecordsAt(int offset, params byte[][] records)
        {
            foreach (byte[] record in records)
            {
                uint block = (uint)(offset / Mode2RawImageReader.UserDataSize);
                int blockOffset = offset % Mode2RawImageReader.UserDataSize;
                byte[] payload = _sectors[20 + block]
                    .AsSpan(Mode2RawImageReader.UserDataOffset, Mode2RawImageReader.UserDataSize)
                    .ToArray();
                record.CopyTo(payload, blockOffset);
                WriteUserData(20 + block, payload);
                offset += record.Length;
            }
        }

        public void SetRootByteAt(int offset, byte value)
        {
            uint block = (uint)(offset / Mode2RawImageReader.UserDataSize);
            int blockOffset = offset % Mode2RawImageReader.UserDataSize;
            byte[] payload = _sectors[20 + block]
                .AsSpan(Mode2RawImageReader.UserDataOffset, Mode2RawImageReader.UserDataSize)
                .ToArray();
            payload[blockOffset] = value;
            WriteUserData(20 + block, payload);
        }

        public void SetDirectoryRecords(uint lba, params byte[][] records) => SetRecords(lba, 2048, 0, records);

        public ImmutableArray<IsoFileEntry> ReadFiles()
        {
            _stream.Position = 0;
            var reader = new Mode2RawImageReader(_stream);
            IsoPrimaryVolumeDescriptor volume = IsoPrimaryVolumeDescriptorReader.Read(reader);
            return IsoPrimaryVolumeDirectoryReader.ReadFiles(reader, volume);
        }

        public void Dispose() => _stream.Dispose();

        private void SetRecords(uint extentLba, uint byteLength, int offset, byte[][] records)
        {
            byte[] content = new byte[checked((int)byteLength)];
            foreach (byte[] record in records)
            {
                record.CopyTo(content, offset);
                offset += record.Length;
            }

            int remaining = content.Length;
            for (uint block = 0; remaining > 0; block++)
            {
                int chunk = Math.Min(remaining, Mode2RawImageReader.UserDataSize);
                WriteUserData(extentLba + block, content.AsSpan((int)(block * Mode2RawImageReader.UserDataSize), chunk).ToArray());
                remaining -= chunk;
            }
        }

        private void WriteUserData(uint lba, byte[] payload)
        {
            Buffer.BlockCopy(payload, 0, _sectors[lba], Mode2RawImageReader.UserDataOffset, payload.Length);
            if (_stream is not null)
            {
                long position = (long)lba * Mode2RawImageReader.RawSectorSize + Mode2RawImageReader.UserDataOffset;
                _stream.Position = position;
                _stream.Write(payload);
            }
        }

        private byte[] CreatePvd()
        {
            byte[] descriptor = CreateDescriptor(1);
            SetBothEndianUInt32(descriptor, 80, _volumeSpaceSize);
            SetBothEndianUInt16(descriptor, 120, 1);
            SetBothEndianUInt16(descriptor, 124, 1);
            SetBothEndianUInt16(descriptor, 128, Mode2RawImageReader.UserDataSize);
            byte[] root = RawDirectoryRecord([0], 20, _rootLength, isDirectory: true);
            root.CopyTo(descriptor, 156);
            return descriptor;
        }

        private static byte[] CreateDescriptor(byte type)
        {
            byte[] descriptor = new byte[Mode2RawImageReader.UserDataSize];
            descriptor[0] = type;
            "CD001"u8.CopyTo(descriptor.AsSpan(1));
            descriptor[6] = 1;
            return descriptor;
        }

        private static byte[] CreateMode2Sector()
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
    }
}