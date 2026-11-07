namespace Zodiac.Framework;

/// <summary>Source ordinary targeting rules; independent of spell amount and animation.</summary>
public sealed record OrdinarySpellGeometry(byte Range, byte Area, byte Vertical, bool RangeUsesVertical = false,
    bool CannotTargetSelf = false, bool CannotHitCaster = false, bool TopDown = false,
    bool CanHitAllies = false, bool CanHitEnemies = false, bool NoFollowTarget = false, bool Auto = false);
