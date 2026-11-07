using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public readonly record struct MagicResultTypes(bool HpDamage = false, bool HpHealing = false, bool PseudoStatus = false);
