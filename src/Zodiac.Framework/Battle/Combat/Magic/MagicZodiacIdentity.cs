using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Independent source identity flags; do not normalize the male/female pair.</summary>
public readonly record struct MagicZodiacIdentity(MagicZodiacSign Sign, bool IsMale = false,
    bool IsFemale = false, bool IsMonster = false);
