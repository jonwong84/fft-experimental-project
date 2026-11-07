namespace Zodiac.Framework;

/// <summary>Raw formula work, before absorption/undead routing, procs, 999 cap or HP publication.</summary>
public sealed record FaithMagicAmount(short ModifiedXa, short StoredProduct, short Amount,
    byte EffectiveCasterFaith, byte EffectiveTargetFaith);
