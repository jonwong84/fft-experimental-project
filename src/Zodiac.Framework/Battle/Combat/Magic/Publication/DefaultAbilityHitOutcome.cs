namespace Zodiac.Framework;

/// <summary>Private HP/status projection only; no career, command, KO counter or live adoption authority.</summary>
public sealed record DefaultAbilityHitOutcome(MagicCurrentUnit Current, MagicUnitVitals Vitals, bool Changed);
