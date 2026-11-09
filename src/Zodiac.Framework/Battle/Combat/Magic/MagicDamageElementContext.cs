namespace Zodiac.Framework;

public sealed record MagicDamageElementContext(ElementToggles Elements, MagicDamageWeather EffectiveWeather,
    MagicElementAffinities TargetAffinities, MagicDamageElementProfile Profile);
