using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record FaithMagicCurrentRequest(MagicCurrentUnit Caster, MagicCurrentUnit Target, FaithMagicSpell Spell,
    MagicDamageWeather Weather, MagicFormulaEvaluationMode Mode, MagicResultFinalizationProfile? Profile,
    FaithMagicEntryProfile? Entry);
