using Zodiac.AssetTool;
using Xunit;

namespace Zodiac.AssetTool.Tests;

public sealed class UnitSpriteSheetTests
{
    [Fact]
    public void Parse_exposes_fixed_blocks_and_variable_attack_tail()
    {
        byte[] source = new byte[UnitSpriteSheet.FixedLength + 3];
        source[0] = 0x34;
        source[1] = 0x12;
        source[UnitSpriteSheet.MainPixelsOffset] = 0x56;
        source[UnitSpriteSheet.PortraitAndBodyPixelsOffset] = 0x78;
        source[UnitSpriteSheet.AttackImageOffset] = 0x9a;

        UnitSpriteSheet sheet = UnitSpriteSheet.Parse(source);
        source[UnitSpriteSheet.MainPixelsOffset] = 0;

        Assert.Equal(0x1234, sheet.ReadPaletteColor(0, 0));
        Assert.Equal(0x56, sheet.MainPixels[0]);
        Assert.Equal(0x78, sheet.PortraitAndBodyPixels[0]);
        Assert.Equal(new byte[] { 0x9a, 0, 0 }, sheet.CompressedAttackImage.ToArray());
    }

    [Fact]
    public void Parse_accepts_an_exact_fixed_prefix_with_no_compressed_tail()
    {
        UnitSpriteSheet sheet = UnitSpriteSheet.Parse(new byte[UnitSpriteSheet.FixedLength]);

        Assert.Empty(sheet.CompressedAttackImage.ToArray());
        Assert.Equal(UnitSpriteSheet.FixedLength, sheet.FileLength);
    }

    [Fact]
    public void Parse_rejects_a_truncated_fixed_prefix()
    {
        Assert.Throws<InvalidDataException>(() => UnitSpriteSheet.Parse(new byte[UnitSpriteSheet.FixedLength - 1]));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(16, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 16)]
    public void ReadPaletteColor_rejects_out_of_range_indices(int paletteIndex, int colorIndex)
    {
        UnitSpriteSheet sheet = UnitSpriteSheet.Parse(new byte[UnitSpriteSheet.FixedLength]);

        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.ReadPaletteColor(paletteIndex, colorIndex));
    }
}
