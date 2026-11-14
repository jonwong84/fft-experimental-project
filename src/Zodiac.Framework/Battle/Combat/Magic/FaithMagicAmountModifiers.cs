namespace Zodiac.Framework;

public readonly record struct FaithMagicAmountModifiers(bool StrengthensElement = false, bool MagicAttackUp = false,
    bool MagicDefenseUp = false, bool TargetShell = false, bool TargetChickenOrFrog = false,
    MagicCompatibility Compatibility = MagicCompatibility.Neutral);
