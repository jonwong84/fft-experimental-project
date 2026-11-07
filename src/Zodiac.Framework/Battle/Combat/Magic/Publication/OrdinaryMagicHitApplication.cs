namespace Zodiac.Framework;

/// <summary>Share the admitted battle's HP/Critical/Dead kernel with hypothetical callers.</summary>
public static class OrdinaryMagicHitApplication
{
    public static OrdinaryMagicHitOutcome Project(MagicCurrentUnit current, MagicUnitVitals vitals, MagicResultFrame frame)
    {
        // Counter/marker bookkeeping is intentionally discarded; this is not a complete AI action or live result.
        var applied = OrdinaryMagicStatusRules.Apply(current, vitals, 0, 3, AbilityOutcomeMarker.None, frame);
        return new(applied.Current, applied.Vitals, applied.Current != current || applied.Vitals != vitals);
    }
}
