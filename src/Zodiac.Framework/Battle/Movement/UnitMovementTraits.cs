namespace Zodiac.Framework;

[Flags]
public enum UnitMovementTraits
{
    None = 0,
    IgnoreHeight = 1 << 0,
    AnyGround = 1 << 1,
    AnyWeather = 1 << 2,
    CannotEnterWater = 1 << 3,
    TeleportPlusTwo = 1 << 4,
}
