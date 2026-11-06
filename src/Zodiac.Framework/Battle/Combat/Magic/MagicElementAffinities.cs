namespace Zodiac.Framework;

public readonly record struct MagicElementAffinities(MagicElement Absorb = MagicElement.None,
    MagicElement Nullify = MagicElement.None, MagicElement Half = MagicElement.None, MagicElement Weak = MagicElement.None);
