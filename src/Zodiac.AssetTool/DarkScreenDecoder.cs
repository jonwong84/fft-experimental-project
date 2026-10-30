using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Zodiac.AssetTool;

public sealed record DecodedDarkScreenTile(int X, int Y, int InitialDelay);
public sealed record DecodedDarkScreenRotation(int Angle, int AX, int AY, int BX, int BY);
public sealed record DecodedDarkScreen(ImmutableArray<DecodedDarkScreenTile> Tiles, ImmutableArray<DecodedDarkScreenRotation> Rotations);

/// <summary>Offline selected SquareRoot0/layout and Q12 sample conversion. No runtime PS1 math library.</summary>
public static class DarkScreenDecoder
{
    public static int ApproximateRoot(int value, ReadOnlySpan<byte> table)
    {
        if (value is < 0 or > 127296 || table.Length != 384) throw new InvalidDataException("Unsupported sqrt input/table.");
        for (int i = 0; i < 192; i++)
            if (BinaryPrimitives.ReadInt16LittleEndian(table[(i * 2)..]) <= 0) throw new InvalidDataException("Invalid sqrt factor.");
        if (value == 0) return 0;
        int even = BitOperations.LeadingZeroCount((uint)value) & ~1;
        int exponent = (31 - even) >> 1, difference = even - 24;
        int normalized = difference >= 0 ? value << difference : value >> -difference;
        uint factor = (uint)BinaryPrimitives.ReadInt16LittleEndian(table[((normalized - 64) * 2)..]);
        return (int)((factor << exponent) / 4096);
    }

    public static DecodedDarkScreen Decode(ReadOnlySpan<byte> sqrt, ReadOnlySpan<byte> sincos)
    {
        if (sincos.Length != 16384) throw new InvalidDataException("Expected original packed sin/cos table.");
        var tiles = ImmutableArray.CreateBuilder<DecodedDarkScreenTile>();
        for (int row = 0; row < 12; row++)
            for (int column = 0; column < 24; column++)
            {
                int x = column * 12 - 12, y = row * 24 - (column % 2 == 0 ? 12 : 24);
                int root = ApproximateRoot(x * x + y * y, sqrt);
                int delay = checked(root * root) / 3072;
                if (delay is < 0 or > 41) throw new InvalidDataException("Selected delay exceeds its traced envelope.");
                tiles.Add(new(x, y, delay));
            }
        var axes = ImmutableArray.CreateBuilder<DecodedDarkScreenRotation>();
        for (int angle = 0; angle <= 512; angle += 64)
        {
            int a = (angle + 512) * 4, b = (angle + 1536) * 4;
            int ax = BinaryPrimitives.ReadInt16LittleEndian(sincos[(a + 2)..]), ay = BinaryPrimitives.ReadInt16LittleEndian(sincos[a..]);
            int bx = BinaryPrimitives.ReadInt16LittleEndian(sincos[(b + 2)..]), by = BinaryPrimitives.ReadInt16LittleEndian(sincos[b..]);
            if (new[] { ax, ay, bx, by }.Any(x => x is < -4096 or > 4096)) throw new InvalidDataException("Invalid Q12 coefficient.");
            axes.Add(new(angle, ax, ay, bx, by));
        }
        return new(tiles.ToImmutable(), axes.ToImmutable());
    }
}
