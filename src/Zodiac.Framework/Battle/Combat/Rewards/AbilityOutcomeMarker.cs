namespace Zodiac.Framework;

/// <summary>Source outcome stores replace these values; they are not cumulative flags.</summary>
public enum AbilityOutcomeMarker : byte
{
    None = 0,
    Targeted = 1,
    Affected = 2,
    LearnChecked = 4
}
