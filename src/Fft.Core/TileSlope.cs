namespace Fft.Core;

/// <summary>Encoded two-bit slope values for the four tile edges.</summary>
/// <remarks>
/// The original tile stores four two-bit edge values in east, west, south, north order.
/// This managed value exposes the sides individually while retaining a packed-byte bridge
/// for import and source-trace fixtures.
/// </remarks>
public readonly record struct TileSlope
{
    public TileSlope(int east, int west, int south, int north)
    {
        ValidateEdge(east, nameof(east));
        ValidateEdge(west, nameof(west));
        ValidateEdge(south, nameof(south));
        ValidateEdge(north, nameof(north));
        East = east;
        West = west;
        South = south;
        North = north;
    }

    public int East { get; }

    public int West { get; }

    public int South { get; }

    public int North { get; }

    public static TileSlope Flat => new(0, 0, 0, 0);

    public static TileSlope FromPackedByte(byte value) => new(
        value & 0b11,
        (value >> 2) & 0b11,
        (value >> 4) & 0b11,
        (value >> 6) & 0b11);

    public byte ToPackedByte() => (byte)(East | (West << 2) | (South << 4) | (North << 6));

    private static void ValidateEdge(int value, string parameterName)
    {
        if (value is < 0 or > 3)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "An encoded slope edge must fit in two bits.");
        }
    }
}
