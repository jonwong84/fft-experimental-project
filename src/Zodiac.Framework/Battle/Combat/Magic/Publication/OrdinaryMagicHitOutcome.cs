namespace Zodiac.Framework;

/// <summary>Private HP/status projection only; no career, command, KO counter or live adoption authority.</summary>
public sealed record OrdinaryMagicHitOutcome(MagicCurrentUnit Current, MagicUnitVitals Vitals, bool Changed);
