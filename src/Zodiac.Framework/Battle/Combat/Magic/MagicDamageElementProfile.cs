namespace Zodiac.Framework;

/// <summary>Remaining source work/status facts; false means uncertified, never neutral.</summary>
public sealed record MagicDamageElementProfile(bool FreshElementWork = false, bool FireTargetHasNoOil = false,
    bool EarthTargetHasNoFloatOrRider = false);
