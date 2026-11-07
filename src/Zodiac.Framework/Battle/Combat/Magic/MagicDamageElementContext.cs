namespace Zodiac.Framework;

public sealed record MagicDamageElementContext(MagicElement Elements, MagicDamageWeather EffectiveWeather,
    MagicElementAffinities TargetAffinities, MagicDamageElementProfile Profile);
