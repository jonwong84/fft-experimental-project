using System.Buffers.Binary;
using Xunit;

namespace Zodiac.AssetTool.Tests;

public sealed class DarkScreenDecoderTests
{
    private static byte[] Table()
    {
        byte[] table = new byte[384];
        for (int i = 0; i < 192; i++) BinaryPrimitives.WriteInt16LittleEndian(table.AsSpan(i * 2), 4096);
        return table;
    }
    [Theory]
    [InlineData(0,0)] [InlineData(1,1)] [InlineData(4,2)] [InlineData(64,8)]
    public void Authored_normalized_factors_have_source_width_results(int value, int expected)
        => Assert.Equal(expected, DarkScreenDecoder.ApproximateRoot(value, Table()));
    [Fact]
    public void Authored_nontrivial_factor_and_shift_are_not_exact_sqrt()
    {
        var table = Table(); BinaryPrimitives.WriteInt16LittleEndian(table.AsSpan((81 - 64) * 2), 4608);
        Assert.Equal(9, DarkScreenDecoder.ApproximateRoot(81, table));
        Assert.Equal(144, DarkScreenDecoder.ApproximateRoot(81 * 256, table));
    }
    [Theory]
    [InlineData(-1)] [InlineData(127297)]
    public void Unsupported_distance_envelope_rejects(int value) => Assert.Throws<InvalidDataException>(() => DarkScreenDecoder.ApproximateRoot(value, Table()));
    [Fact]
    public void Short_or_invalid_factor_tables_reject()
    {
        Assert.Throws<InvalidDataException>(() => DarkScreenDecoder.ApproximateRoot(1, new byte[383]));
        Assert.Throws<InvalidDataException>(() => DarkScreenDecoder.ApproximateRoot(0, new byte[384]));
    }
    [Fact]
    public void Layout_and_axis_selection_use_all_bounded_samples_without_mutating_input()
    {
        var sqrt = Table(); var sincos = new byte[16384];
        for (int i = 0; i < 9; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(sincos.AsSpan((512 + i * 64) * 4), 4096);
            BinaryPrimitives.WriteInt16LittleEndian(sincos.AsSpan((1536 + i * 64) * 4 + 2), -4096);
        }
        var before = sincos.ToArray(); var result = DarkScreenDecoder.Decode(sqrt, sincos);
        Assert.Equal((288, 9), (result.Tiles.Length, result.Rotations.Length));
        Assert.Equal((-12, -12), (result.Tiles[0].X, result.Tiles[0].Y));
        Assert.Equal((0, -24), (result.Tiles[1].X, result.Tiles[1].Y));
        Assert.Equal((264, 240), (result.Tiles[^1].X, result.Tiles[^1].Y));
        Assert.All(result.Rotations, r => Assert.Equal((0,4096,-4096,0), (r.AX,r.AY,r.BX,r.BY)));
        Assert.Equal(before, sincos);
    }
    [Fact]
    public void Unsupported_rotation_lengths_or_coefficients_reject()
    {
        Assert.Throws<InvalidDataException>(() => DarkScreenDecoder.Decode(Table(), new byte[1]));
        var sincos = new byte[16384]; BinaryPrimitives.WriteInt16LittleEndian(sincos.AsSpan(512 * 4), 5000);
        Assert.Throws<InvalidDataException>(() => DarkScreenDecoder.Decode(Table(), sincos));
    }
}
