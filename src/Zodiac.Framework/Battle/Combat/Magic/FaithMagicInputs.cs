namespace Zodiac.Framework;

public enum MagicZodiacSign
{
    Aries, Taurus, Gemini, Cancer, Leo, Virgo, Libra, Scorpio, Sagittarius, Capricorn, Aquarius, Pisces, Serpentarius
}

/// <summary>Independent source identity flags; do not normalize the male/female pair.</summary>
public readonly record struct MagicZodiacIdentity(MagicZodiacSign Sign, bool IsMale = false,
    bool IsFemale = false, bool IsMonster = false);

/// <summary>Current-input projection for the shared magic owner, source 11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
public static class FaithMagicInputs
{
    public static MagicCompatibility ClassifyCompatibility(MagicZodiacIdentity caster, MagicZodiacIdentity target)
    {
        if (!Enum.IsDefined(caster.Sign)) throw new ArgumentOutOfRangeException(nameof(caster));
        if (!Enum.IsDefined(target.Sign)) throw new ArgumentOutOfRangeException(nameof(target));
        if (caster.Sign == MagicZodiacSign.Serpentarius || target.Sign == MagicZodiacSign.Serpentarius)
            return MagicCompatibility.Serpentarius;
        int difference = Math.Abs((int)caster.Sign - (int)target.Sign);
        if (difference == 6)
        {
            if (caster.IsMonster || target.IsMonster) return MagicCompatibility.Bad;
            return caster.IsMale == target.IsMale && caster.IsFemale == target.IsFemale
                ? MagicCompatibility.Worst : MagicCompatibility.Best;
        }
        return difference switch
        {
            3 or 9 => MagicCompatibility.Bad,
            4 or 8 => MagicCompatibility.Good,
            _ => MagicCompatibility.Neutral
        };
    }
}
