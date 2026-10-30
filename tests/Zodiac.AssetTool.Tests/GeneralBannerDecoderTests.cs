using System.Buffers.Binary;
using System.Text.Json;
using Xunit;

namespace Zodiac.AssetTool.Tests;

public sealed class GeneralBannerDecoderTests
{
    [Theory]
    [InlineData("PACKING")]
    [InlineData("CLUT")]
    [InlineData("UV-WRAP")]
    [InlineData("FULL-GEOMETRY")]
    public void AuthoredSpecification(string id)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/general-banner-presentation-contract-v1.json")));
        var c = doc.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("id").GetString() == id);
        var input = c.GetProperty("input"); var expected = c.GetProperty("expected");
        if (id == "PACKING")
        {
            byte[] pixels = new byte[0x6400]; pixels[0] = input.GetProperty("packedByte").GetByte();
            var unpacked = GeneralBannerDecoder.Unpack(pixels);
            Assert.Equal(expected.GetProperty("leftIndex").GetInt32(), unpacked[0]);
            Assert.Equal(expected.GetProperty("rightIndex").GetInt32(), unpacked[1]);
            Assert.Equal(51200, unpacked.Length);
        }
        else if (id == "CLUT")
        {
            var data = Input(); data.Image[0x6480] = 0xA7;
            var decoded = GeneralBannerDecoder.Decode(data.Image, data.Quads, data.Pages, [0, 6], data.Colors);
            Assert.Equal(0xA7, decoded.Palette[0]); Assert.Equal(expected.GetProperty("paletteBytes").GetInt32(), decoded.Palette.Length);
        }
        else
        {
            int[] rect = input.GetProperty("sourceRect").EnumerateArray().Select(n => n.GetInt32()).ToArray();
            int[] offset = input.TryGetProperty("sourceOffset", out var o) ? o.EnumerateArray().Select(n => n.GetInt32()).ToArray() : [0, 0];
            var n = GeneralBannerDecoder.Normalize(rect[0], rect[1], offset[0], offset[1]);
            if (id == "UV-WRAP") Assert.Equal(expected.GetProperty("uvRect").EnumerateArray().Select(n => n.GetInt32()), new[] { n.U, n.V, rect[2], rect[3] });
            else
            {
                Assert.Equal(new[] { 28, 70, 44, 86 }, new[] { n.X, n.Y, n.X + rect[2], n.Y + rect[3] });
                Assert.Equal((4, 2), (n.U, n.V));
            }
        }
    }

    [Theory]
    [InlineData("image")][InlineData("quads")][InlineData("pages")][InlineData("range")][InlineData("colors")]
    [InlineData("height")][InlineData("uv")][InlineData("screen")][InlineData("blend")][InlineData("gradient")]
    public void RejectsUnsupportedLayout(string variant)
    {
        var d = Input(); byte[] range = [0, 6];
        switch (variant)
        {
            case "image": d.Image = new byte[0x8000]; break;
            case "quads": d.Quads = new byte[96]; break;
            case "pages": d.Pages = []; break;
            case "range": range = [0, 6, 0, 0]; break;
            case "colors": d.Colors = []; break;
            case "height": d.Quads[6] = 30; break;
            case "uv": d.Quads[2] = 42; break; // (42-128)&255 = 170, ending beyond initialized row 199
            case "screen": BinaryPrimitives.WriteInt16LittleEndian(d.Quads.AsSpan(8), 32767); break;
            case "blend": d.Pages[2] = 0; break;
            case "gradient": d.Colors[4] = 127; break;
        }
        Assert.Throws<InvalidDataException>(() => GeneralBannerDecoder.Decode(d.Image, d.Quads, d.Pages, range, d.Colors));
    }

    [Fact]
    public void IgnoresUnusedBytesAndNeverDecodesReservationSuffix()
    {
        var d = Input(); Array.Fill(d.Image, (byte)0xff, 0x6500, 0x300);
        d.Colors[0] = 255; d.Pages[0] = 255; d.Pages[3] = 255;
        var decoded = GeneralBannerDecoder.Decode(d.Image, d.Quads, d.Pages, [0, 6], d.Colors);
        Assert.All(decoded.Indices, i => Assert.Equal(0, i));
        Assert.Equal(6, decoded.Pieces.Length);
        Assert.Equal("subtract", decoded.Pieces[0].Material); Assert.Equal("add", decoded.Pieces[2].Material);
    }

    private static (byte[] Image, byte[] Quads, byte[] Pages, byte[] Colors) Input()
    {
        var d = (Image: new byte[0x6800], Quads: new byte[72], Pages: new byte[24], Colors: new byte[120]);
        for (int i = 0; i < 6; i++)
        {
            short[] q = [0, (short)(i < 3 ? 0 : 32), 232, 31, -124, -100];
            for (int n = 0; n < 6; n++) BinaryPrimitives.WriteInt16LittleEndian(d.Quads.AsSpan(i * 12 + n * 2), q[n]);
            d.Pages[i * 4 + 2] = (byte)(i % 3 == 2 ? 0x20 : 0x40);
            for (int v = 0; v < 4; v++) for (int ch = 0; ch < 3; ch++) d.Colors[i * 20 + 4 + v * 4 + ch] = 128;
        }
        return d;
    }
}
