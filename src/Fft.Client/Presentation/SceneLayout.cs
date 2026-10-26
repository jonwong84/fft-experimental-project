using System.Collections.Immutable;
using Zodiac.Framework;

namespace Fft.Client.Presentation;

public readonly record struct PixelPoint(int X, int Y);
public readonly record struct PixelRect(int X, int Y, int Width, int Height);
public sealed record SceneUnit(BattleUnitId Id, PixelPoint Feet, int Hp, int MaxHp);

/// <summary>Synthetic presentation coordinates; no original FFT camera or simulation rules.</summary>
public static class SceneLayout
{
    public const int CanvasWidth = 960;
    public const int CanvasHeight = 640;

    /// <summary>Centered 3:2 viewport, floored to pixels; subpixel dimensions have a one-pixel minimum.</summary>
    public static PixelRect Fit(int outputWidth, int outputHeight)
    {
        if (outputWidth <= 0 || outputHeight <= 0) return new(0, 0, 0, 0);
        int width, height;
        if ((long)outputWidth * CanvasHeight <= (long)outputHeight * CanvasWidth)
        {
            width = outputWidth;
            height = (int)Math.Max(1, (long)width * CanvasHeight / CanvasWidth);
        }
        else
        {
            height = outputHeight;
            width = (int)Math.Max(1, (long)height * CanvasWidth / CanvasHeight);
        }
        return new((outputWidth - width) / 2, (outputHeight - height) / 2, width, height);
    }

    /// <summary>160x80 diamonds (64x32 in the compact view), centered at (480,340).
    /// Increasing X projects down/right; increasing Y projects down/left. This is an authored view.</summary>
    public static PixelPoint TileCenter(int x, int y, int mapWidth, int mapDepth, bool compact = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapDepth);
        if (x < 0 || x >= mapWidth) throw new ArgumentOutOfRangeException(nameof(x));
        if (y < 0 || y >= mapDepth) throw new ArgumentOutOfRangeException(nameof(y));
        int halfWidth = compact ? 32 : 80, halfHeight = compact ? 16 : 40;
        long centerX = 480 + (long)halfWidth * ((long)x - y) - halfWidth / 2L * ((long)mapWidth - mapDepth);
        long centerY = 340 + (long)halfHeight * ((long)x + y) - halfHeight / 2L * ((long)mapWidth + mapDepth - 2);
        return new(checked((int)centerX), checked((int)centerY));
    }

    public static ImmutableArray<SceneUnit> Units(AttackBattleSnapshot snapshot, bool compact = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var battle = snapshot.TurnBattle.Battle;
        var combatants = snapshot.Combatants.ToDictionary(unit => unit.UnitId);
        return battle.Units.OrderBy(unit => (long)unit.Position.X + unit.Position.Y)
            .ThenBy(unit => unit.Position.X)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .Select(unit => new SceneUnit(unit.Id,
                TileCenter(unit.Position.X, unit.Position.Y, battle.Map.Width, battle.Map.Depth, compact),
                combatants[unit.Id].Hp, combatants[unit.Id].MaxHp))
            .ToImmutableArray();
    }
}
