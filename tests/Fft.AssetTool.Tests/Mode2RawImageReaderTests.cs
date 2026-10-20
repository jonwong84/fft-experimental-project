using Fft.AssetTool;
using Xunit;

namespace Fft.AssetTool.Tests;

public sealed class Mode2RawImageReaderTests
{
    [Fact]
    public void ReadUserData_maps_track_relative_lbas_to_payloads_across_sectors()
    {
        using var image = new MemoryStream();
        for (byte sectorNumber = 0; sectorNumber < 3; sectorNumber++)
        {
            byte[] sector = CreateMode2Form1Sector();
            Array.Fill(sector, sectorNumber, Mode2RawImageReader.UserDataOffset, Mode2RawImageReader.UserDataSize);
            image.Write(sector);
        }

        image.Position = 0;
        var reader = new Mode2RawImageReader(image);

        Assert.All(reader.ReadUserData(0), value => Assert.Equal(0, value));
        Assert.All(reader.ReadUserData(1), value => Assert.Equal(1, value));
        Assert.All(reader.ReadUserData(2), value => Assert.Equal(2, value));
    }

    [Fact]
    public void Constructor_rejects_truncated_raw_sector_image()
    {
        using var image = new MemoryStream(new byte[Mode2RawImageReader.RawSectorSize + 1]);

        Assert.Throws<InvalidDataException>(() => new Mode2RawImageReader(image));
    }

    [Fact]
    public void ReadUserData_rejects_sector_with_invalid_mode()
    {
        byte[] sector = CreateMode2Form1Sector();
        sector[15] = 1;
        using var image = new MemoryStream(sector);
        var reader = new Mode2RawImageReader(image);

        Assert.Contains("not Mode 2", Assert.Throws<InvalidDataException>(() => reader.ReadUserData(0)).Message);
    }

    [Fact]
    public void ReadUserData_rejects_mode_2_form_2_sector()
    {
        byte[] sector = CreateMode2Form1Sector();
        sector[18] = 0x20;
        sector[22] = 0x20;
        using var image = new MemoryStream(sector);
        var reader = new Mode2RawImageReader(image);

        Assert.Contains("Form 2", Assert.Throws<InvalidDataException>(() => reader.ReadUserData(0)).Message);
    }

    [Fact]
    public void ReadUserData_rejects_lba_outside_image()
    {
        using var image = new MemoryStream(CreateMode2Form1Sector());
        var reader = new Mode2RawImageReader(image);

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadUserData(1));
    }

    private static byte[] CreateMode2Form1Sector()
    {
        var sector = new byte[Mode2RawImageReader.RawSectorSize];
        Array.Fill(sector, (byte)0xff, 1, 10);
        sector[15] = 2;
        sector[16] = 1;
        sector[17] = 2;
        sector[18] = 0;
        sector[19] = 4;
        Array.Copy(sector, 16, sector, 20, 4);
        return sector;
    }
}