namespace Zodiac.Framework;

public sealed record MagicDamageElementOutcome(short WeatherAmount, short Amount, bool Nullified,
    bool Absorbed, bool WeaknessApplied);
