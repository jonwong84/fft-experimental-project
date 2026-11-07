namespace Zodiac.Framework;

public sealed record MagicMpActor(ushort Mp, bool HalfMp, bool IsMime, bool Frog, bool Silence,
    bool DontAct, MagicActionRestriction Restriction);
