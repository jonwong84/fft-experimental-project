using System.Collections.Immutable;
using Fft.Core;

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

    /// <summary>160x80 diamonds, with the map's geometric center at (480,340).
    /// Increasing X projects down/right; increasing Y projects down/left. This is an authored view.</summary>
    public static PixelPoint TileCenter(int x, int y, int mapWidth, int mapDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapDepth);
        if (x < 0 || x >= mapWidth) throw new ArgumentOutOfRangeException(nameof(x));
        if (y < 0 || y >= mapDepth) throw new ArgumentOutOfRangeException(nameof(y));
        long centerX = 480 + 80L * ((long)x - y) - 40L * ((long)mapWidth - mapDepth);
        long centerY = 340 + 40L * ((long)x + y) - 20L * ((long)mapWidth + mapDepth - 2);
        return new(checked((int)centerX), checked((int)centerY));
    }

    public static ImmutableArray<SceneUnit> Units(AttackBattleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var battle = snapshot.TurnBattle.Battle;
        var combatants = snapshot.Combatants.ToDictionary(unit => unit.UnitId);
        return battle.Units.OrderBy(unit => (long)unit.Position.X + unit.Position.Y)
            .ThenBy(unit => unit.Position.X)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .Select(unit => new SceneUnit(unit.Id,
                TileCenter(unit.Position.X, unit.Position.Y, battle.Map.Width, battle.Map.Depth),
                combatants[unit.Id].Hp, combatants[unit.Id].MaxHp))
            .ToImmutableArray();
    }
}
