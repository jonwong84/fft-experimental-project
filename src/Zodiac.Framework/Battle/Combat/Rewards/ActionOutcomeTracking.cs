namespace Zodiac.Framework;

/// <summary>Normal-return target branch only; excludes early KO and dead-conversion application.</summary>
public static class ActionOutcomeTracking
{
    public static AbilityOutcomeMarker ApplyOrdinary(AbilityOutcomeMarker previous, bool primaryContext,
        bool hit, bool changed)
    {
        var marker = primaryContext ? AbilityOutcomeMarker.Targeted : previous;
        return hit && changed ? AbilityOutcomeMarker.Affected : marker;
    }
}
