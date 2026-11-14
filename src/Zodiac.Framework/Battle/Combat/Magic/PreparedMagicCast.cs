using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Derived immutable MP proposal for one certified ready action; no command or live-state authority.</summary>
public sealed record PreparedMagicCast
{
    public MagicCurrentUnit Caster { get; }
    public MagicCastDefinition Definition { get; }
    public MagicDamageWeather Weather { get; }
    public MagicFormulaEvaluationMode Mode { get; }
    public MagicResultFinalizationProfile ResultProfile { get; }
    public MagicMpOutcome Resource { get; }
    internal PreparedMagicCast(MagicReadyCastRequest r, MagicMpOutcome resource)
    { Caster = r.Caster; Definition = r.Definition; Weather = r.Weather; Mode = r.Mode; ResultProfile = r.ResultProfile!; Resource = resource; }
}
