namespace Zodiac.Framework;

/// <summary>
/// Weapon-only input to physical evasion, before facing/Concentrate/other modifiers.
/// formula_evade_store_physical, decomp 611b504d25d4a9b03fab8023d7dbea75fe381d5d.
/// Assumes a human with a weapon in the selected hand; shields/accessories are separate.
/// </summary>
public static class PhysicalWeaponEvasion
{
    public static int GetContribution(int weaponEvade, bool hasWeaponGuard, bool actionUnrestricted)
    {
        if (weaponEvade is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(weaponEvade));
        return hasWeaponGuard && actionUnrestricted ? weaponEvade : 0;
    }
}
