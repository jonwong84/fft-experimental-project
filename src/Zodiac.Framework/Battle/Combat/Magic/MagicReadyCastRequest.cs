using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record MagicReadyCastRequest(MagicCurrentUnit Caster, MagicCastDefinition Definition,
    MagicDamageWeather Weather, MagicFormulaEvaluationMode Mode, MagicResultFinalizationProfile? ResultProfile,
    MagicReadyCastProfile? ReadyProfile);
