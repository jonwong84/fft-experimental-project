using System.Buffers.Binary;
using System.Text.Json;
using Xunit;

namespace Zodiac.AssetTool.Tests;

public sealed class ReadyArtworkDecoderTests
{
    internal static (byte[] Image, byte[] Quads, byte[] Colors, byte[] Rect) Input()
    {
        var image = new byte[0x6800]; var quads = new byte[72]; var colors = new byte[120]; var rect = new byte[8];
        int[] r = [384, 254, 64, 2];
        for (int j = 0; j < 4; j++) BinaryPrimitives.WriteInt16LittleEndian(rect.AsSpan(j * 2), (short)r[j]);
        for (int i = 0; i < 6; i++)
        {
            short[] q = [224, 0, 16, 24, -86, -22];
            for (int j = 0; j < 6; j++) BinaryPrimitives.WriteInt16LittleEndian(quads.AsSpan(i * 12 + j * 2), q[j]);
            colors[i * 20] = 255;
            for (int j = 0; j < 4; j++) { colors[i * 20 + 4 + j * 4] = (byte)(j * 64); colors[i * 20 + 7 + j * 4] = 255; }
        }
        return (image, quads, colors, rect);
    }
    [Theory]
    [InlineData("RPR-001")]
    [InlineData("RPR-002")]
    [InlineData("RPR-003")]
    [InlineData("RPR-005")]
    public void Authored_offline_expectations_execute(string id)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/ready-presentation-contract-v1.json")));
        var c = doc.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("id").GetString() == id);
        var expected = c.GetProperty("expected"); var input = Input();
        if (id == "RPR-001")
        {
            var packed = c.GetProperty("given").GetProperty("packedBytes").EnumerateArray().Select(v => v.GetByte()).ToArray();
            packed.CopyTo(input.Image, 0);
            var decoded = ReadyArtworkDecoder.Decode(input.Image, input.Quads, [0, 6], input.Colors, input.Rect);
            Assert.Equal(expected.GetProperty("unpackedIndices").EnumerateArray().Select(v => v.GetByte()), decoded.Indices.Take(4));
        }
        else if (id == "RPR-002")
        {
            var decoded = ReadyArtworkDecoder.Decode(input.Image, input.Quads, [0, 6], input.Colors, input.Rect);
            var g = decoded.Glyphs[0];
            Assert.Equal(expected.GetProperty("screen").EnumerateArray().Select(v => v.GetInt32()), new[] { g.X, g.Y, g.Width, g.Height });
            Assert.Equal(expected.GetProperty("uv").EnumerateArray().Select(v => v.GetInt32()), new[] { g.U, g.V, g.Width, g.Height });
        }
        else if (id == "RPR-005")
        {
            input.Image[0x6440] = 0x55; input.Image[0x6480] = 0xaa;
            var decoded = ReadyArtworkDecoder.Decode(input.Image, input.Quads, [0, 6], input.Colors, input.Rect);
            Assert.Equal(expected.GetProperty("paletteBytes").GetInt32(), decoded.Palette.Length);
            Assert.Equal(0x55, decoded.Palette[0]);
            Assert.Equal(new byte[] { 0, 64, 128, 192 }, decoded.Glyphs[0].Colors.Select(c => c.R));
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => ReadyArtworkDecoder.Decode(input.Image[..^1], input.Quads, [0, 6], input.Colors, input.Rect));
            Assert.Throws<InvalidDataException>(() => ReadyArtworkDecoder.Decode(input.Image, input.Quads, [0, 7], input.Colors, input.Rect));
        }
    }
    [Theory]
    [InlineData("quad-length")]
    [InlineData("color-length")]
    [InlineData("palette-rect")]
    [InlineData("height")]
    [InlineData("width")]
    [InlineData("negative-u")]
    [InlineData("wrap-u")]
    [InlineData("out-v")]
    [InlineData("screen")]
    public void Malformed_offline_records_reject(string fault)
    {
        var d = Input();
        switch (fault)
        {
            case "quad-length": d.Quads = []; break;
            case "color-length": d.Colors = []; break;
            case "palette-rect": d.Rect[0] = 0; break;
            case "height": d.Quads[6] = 23; break;
            case "width": d.Quads[4] = 0; break;
            case "negative-u": BinaryPrimitives.WriteInt16LittleEndian(d.Quads, -1); break;
            case "wrap-u": BinaryPrimitives.WriteInt16LittleEndian(d.Quads, 240); break;
            case "out-v": BinaryPrimitives.WriteInt16LittleEndian(d.Quads.AsSpan(2), 177); break;
            case "screen": BinaryPrimitives.WriteInt16LittleEndian(d.Quads.AsSpan(8), 32767); break;
        }
        Assert.Throws<InvalidDataException>(() => ReadyArtworkDecoder.Decode(d.Image, d.Quads, [0, 6], d.Colors, d.Rect));
    }
}
