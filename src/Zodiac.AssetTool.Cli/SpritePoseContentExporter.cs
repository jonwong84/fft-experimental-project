using Zodiac.Content;
using Zodiac.Framework;

namespace Zodiac.AssetTool.Cli;

/// <summary>Maps offline decoded data to the FFT-owned runtime format; Content never references AssetTool.</summary>
public static class SpritePoseContentExporter
{
    public static PreparedSpritePose Create(DecodedUnitPose pose, AssetId id, AssetId textureId, SpritePoseSource source)
    {
        ArgumentNullException.ThrowIfNull(pose);
        if (pose.Rgba.Length != 256 * 256 * 4) throw new InvalidDataException("Expected one ordinary RGBA8 atlas.");
        return new(id, textureId, pose.SequenceId, pose.FrameId, pose.Wait, pose.PaletteIndex, pose.RawPalette,
            pose.PartsBackToFront.Select(p => new SpritePosePart(p.SourceIndex, p.X, p.Y, p.U, p.V, p.Width, p.Height,
                p.FlipHorizontal, p.FlipVertical)), source);
    }
}
