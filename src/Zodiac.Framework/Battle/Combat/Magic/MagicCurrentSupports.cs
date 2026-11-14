using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record MagicCurrentSupports(bool MagicAttackUp = false, bool MagicDefenseUp = false,
    bool Abandon = false, bool HalfMp = false, bool ShortCharge = false, bool NonCharge = false);
