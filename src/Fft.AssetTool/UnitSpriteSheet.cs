using System.Buffers.Binary;

namespace Fft.AssetTool;

/// <summary>A validated view of the fixed blocks and variable tail of a unit SPR file.</summary>
public sealed class UnitSpriteSheet
{
    public const int PaletteOffset = 0x0000;
    public const int PaletteLength = 0x0200;
    public const int MainPixelsOffset = 0x0200;
    public const int MainPixelsLength = 0x8000;
    public const int PortraitAndBodyPixelsOffset = 0x8200;
    public const int PortraitAndBodyPixelsLength = 0x1000;
    public const int AttackImageOffset = 0x9200;
    public const int FixedLength = AttackImageOffset;

    private readonly byte[] _bytes;

    public int FileLength => _bytes.Length;
    public ReadOnlySpan<byte> PaletteBytes => _bytes.AsSpan(PaletteOffset, PaletteLength);
    public ReadOnlySpan<byte> MainPixels => _bytes.AsSpan(MainPixelsOffset, MainPixelsLength);
    public ReadOnlySpan<byte> PortraitAndBodyPixels => _bytes.AsSpan(PortraitAndBodyPixelsOffset, PortraitAndBodyPixelsLength);
    public ReadOnlySpan<byte> CompressedAttackImage => _bytes.AsSpan(AttackImageOffset);

    private UnitSpriteSheet(byte[] bytes) => _bytes = bytes;

    public static UnitSpriteSheet Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < FixedLength)
        {
            throw new InvalidDataException(
                $"A unit SPR file must contain its 0x{FixedLength:X}-byte fixed prefix; found {bytes.Length} bytes.");
        }

        return new UnitSpriteSheet(bytes.ToArray());
    }

    /// <summary>Reads one raw little-endian 16-bit color from the 16-by-16 palette block.</summary>
    public ushort ReadPaletteColor(int paletteIndex, int colorIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(paletteIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(paletteIndex, 16);
        ArgumentOutOfRangeException.ThrowIfNegative(colorIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(colorIndex, 16);
        int offset = (paletteIndex * 16 + colorIndex) * sizeof(ushort);
        return BinaryPrimitives.ReadUInt16LittleEndian(PaletteBytes.Slice(offset, sizeof(ushort)));
    }

    public byte[] DecodeAttackImage() => SpriteAttackImageDecoder.Decode(CompressedAttackImage);
}
