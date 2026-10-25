namespace Zodiac.Framework;

[Flags]
public enum BattleTileMovementFlags
{
    None = 0,
    Blocked = 1 << 0,
    Untargetable = 1 << 1,
    CannotStop = 1 << 2,
}
