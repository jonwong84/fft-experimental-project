using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record FaithMagicSpell(FaithMagicAmountKind Kind, byte Power, MagicElement Elements, bool SecondaryEvadeable);
