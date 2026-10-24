using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Fft.AssetTool.Cli;

internal static class PngRgbaWriter
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static void Write(Stream output, int width, int height, byte[] rgba)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(rgba);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (rgba.LongLength != (long)width * height * 4)
        {
            throw new ArgumentException("RGBA data length does not match the image dimensions.", nameof(rgba));
        }

        output.Write(Signature);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header[4..], (uint)height);
        header[8] = 8; // bit depth
        header[9] = 6; // RGBA
        header[10] = 0; // compression method
        header[11] = 0; // filter method
        header[12] = 0; // no interlace
        WriteChunk(output, "IHDR", header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            int rowLength = checked(width * 4);
            var scanline = new byte[rowLength + 1];
            for (int y = 0; y < height; y++)
            {
                scanline[0] = 0; // PNG filter: None
                rgba.AsSpan(y * rowLength, rowLength).CopyTo(scanline.AsSpan(1));
                zlib.Write(scanline);
            }
        }

        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", ReadOnlySpan<byte>.Empty);
    }

    private static void WriteChunk(Stream output, string name, ReadOnlySpan<byte> data)
    {
        byte[] type = Encoding.ASCII.GetBytes(name);
        Span<byte> integer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(integer, (uint)data.Length);
        output.Write(integer);
        output.Write(type);
        output.Write(data);

        uint crc = 0xffffffff;
        foreach (byte value in type)
        {
            crc = UpdateCrc(crc, value);
        }

        foreach (byte value in data)
        {
            crc = UpdateCrc(crc, value);
        }

        BinaryPrimitives.WriteUInt32BigEndian(integer, ~crc);
        output.Write(integer);
    }

    private static uint UpdateCrc(uint crc, byte value)
    {
        crc ^= value;
        for (int bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
        }

        return crc;
    }
}
