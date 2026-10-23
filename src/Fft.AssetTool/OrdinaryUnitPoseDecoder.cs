using System.Collections.Immutable;

namespace Fft.AssetTool;

public sealed record DecodedPosePart(int SourceIndex, int X, int Y, int U, int V, int Width, int Height,
    bool FlipHorizontal, bool FlipVertical);
public sealed record DecodedUnitPose(int SequenceId, int FrameId, int Wait, int PaletteIndex,
    ImmutableArray<ushort> RawPalette, ImmutableArray<DecodedPosePart> PartsBackToFront, ImmutableArray<byte> Rgba);

/// <summary>Bounded primary-sheet, neutral-color, opaque frame preparation. No attack decode or playback.
/// See docs/architecture/zod-027-chocobo-idle-pose.md, reference 11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
public static class OrdinaryUnitPoseDecoder
{
    public static DecodedUnitPose Decode(UnitSpriteSheet sprite, UnitShapeFile shape, UnitSequenceFile sequence,
        int sequenceId, int paletteIndex)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(sequence);
        if (paletteIndex is < 0 or > 5) throw new NotSupportedException("The selected runtime palette range is 0..5.");
        var pose = sequence.FirstPose(sequenceId);
        if (pose.Frame >= shape.Primary.Length || pose.Frame >= shape.FirstAttackFrame || pose.Frame >= shape.FirstSp2Frame)
            throw new NotSupportedException("Only ordinary primary SHP frames are supported.");
        var frame = shape.Primary[pose.Frame];
        if (frame.RotationIndex != 0 || frame.Flags != 0)
            throw new NotSupportedException("Rotated/blended frames are outside the selected pose slice.");
        var parts = ImmutableArray.CreateBuilder<DecodedPosePart>();
        for (int i = frame.Parts.Length - 1; i >= 0; i--)
        {
            var part = frame.Parts[i];
            // Only sizes used by the selected pose, from the pinned g_sprite_part_sizes table.
            var (width, height) = part.SizeIndex switch
            {
                0 => (8, 8), 2 => (16, 16), 5 => (24, 16), 6 => (24, 24),
                _ => throw new NotSupportedException($"Untraced/special SHP size index {part.SizeIndex}."),
            };
            if (part.U + width > 256 || part.V + height > 256)
                throw new InvalidDataException("SHP source rectangle extends outside the ordinary atlas.");
            parts.Add(new(i, part.X, part.Y, part.U, part.V, width, height, part.FlipHorizontal, part.FlipVertical));
        }
        var palette = Enumerable.Range(0, 16).Select(i => sprite.ReadPaletteColor(paletteIndex, i)).ToImmutableArray();
        if (palette[0] != 0) throw new NotSupportedException("The selected opaque profile requires a zero transparent color.");
        byte[] rgba = new byte[256 * 256 * 4];
        var pixels = sprite.MainPixels;
        for (int p = 0; p < 256 * 256; p++)
        {
            int index = (p & 1) == 0 ? pixels[p / 2] & 15 : pixels[p / 2] >> 4;
            // Native loading sets STP on entries 1..15. This primitive is opaque; STP does not mean RGBA alpha 128.
            ushort color = index == 0 ? palette[0] : (ushort)(palette[index] | 0x8000);
            rgba[p * 4] = Expand(color & 31);
            rgba[p * 4 + 1] = Expand((color >> 5) & 31);
            rgba[p * 4 + 2] = Expand((color >> 10) & 31);
            rgba[p * 4 + 3] = color == 0 ? (byte)0 : (byte)255;
        }
        return new(sequenceId, pose.Frame, pose.Wait, paletteIndex, palette, parts.ToImmutable(), [.. rgba]);
    }

    private static byte Expand(int channel) => (byte)((channel << 3) | (channel >> 2));
}
