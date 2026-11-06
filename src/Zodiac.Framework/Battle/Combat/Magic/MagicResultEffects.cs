using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public readonly record struct MagicResultEffects(bool Absorption = false, bool Weakness = false, bool AddedEffect = false)
{
    public bool Any => Absorption || Weakness || AddedEffect;
}
