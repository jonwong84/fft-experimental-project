using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Source ordinary targeting rules; independent of spell amount and animation.</summary>
public sealed record OrdinarySpellGeometry(byte Range, byte Area, byte Vertical, bool RangeUsesVertical = false,
    bool CannotTargetSelf = false, bool CannotHitCaster = false, bool TopDown = false,
    bool CanHitAllies = false, bool CanHitEnemies = false);

/// <summary>Pure original-compatible map queries, with no transient marks or live state.</summary>
public static class OrdinarySpellTargeting
{
    private static bool SourceTile(BattleTile tile) => tile.Height <= 255 && tile.HalfHeight <= 31 && tile.WaterDepth <= 7;
    private static int Height(BattleTile tile) => tile.Height * 2 + tile.HalfHeight + tile.WaterDepth * 2;

    /// <summary>The original vertical helper receives an unsigned-byte reference after addition.</summary>
    public static byte ReferenceHeight(BattleTile tile)
    {
        if (!SourceTile(tile)) throw new ArgumentOutOfRangeException(nameof(tile));
        return unchecked((byte)Height(tile));
    }

    /// <summary>Choose one sheet, preserving blocked -255 comparisons and lower-sheet ties.</summary>
    public static int? SelectLayer(BattleTile lower, BattleTile? upper, byte reference, byte tolerance, bool topDown = false)
    {
        if (!SourceTile(lower) || upper is { } t && !SourceTile(t)) throw new ArgumentOutOfRangeException(nameof(lower));
        bool a = !lower.IsBlocked, b = upper is { IsBlocked: false };
        int ah = a ? Height(lower) : -255, bh = b ? Height(upper!.Value) : -255;
        if (topDown)
        {
            if (ah >= bh) { b = false; bh = -255; }
            else { a = false; ah = -255; }
        }
        int ad = Math.Abs(ah - reference), bd = Math.Abs(bh - reference);
        return bd < ad ? b && bd <= tolerance * 2 ? 1 : null : a && ad <= tolerance * 2 ? 0 : null;
    }
}
