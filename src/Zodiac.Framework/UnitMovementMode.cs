namespace Zodiac.Framework;

/// <summary>Normalized movement class selected from a unit's active movement abilities.</summary>
/// <remarks>The values are semantic C# values, not the original game's numeric enum values.</remarks>
public enum UnitMovementMode
{
    Normal,
    Fly,
    Teleport,
    Float,
    Lava,
    WaterSurface,
    WaterDepthOne,
    Underwater,
}
