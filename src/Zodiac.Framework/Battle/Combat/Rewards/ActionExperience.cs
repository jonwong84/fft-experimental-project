namespace Zodiac.Framework;

/// <summary>Accumulate source EXP work, not career EXP. Primary context is the guard, not evaluation mode.</summary>
public static class ActionExperience
{
    public static byte Accumulate(byte current, byte actorLevel, byte targetLevel, byte targetKoCount,
        ActionTargetOutcome outcome, bool primaryContext, bool gainedExpUp)
    {
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        if (!primaryContext || outcome == ActionTargetOutcome.Unchanged) return current;
        int basis = outcome == ActionTargetOutcome.Changed ? 10 : targetKoCount == 0 ? 20 : 20 / targetKoCount;
        int award = Math.Max(1, basis + targetLevel - actorLevel);
        if (gainedExpUp) award *= 2;
        return (byte)Math.Max(current, Math.Min(99, award));
    }
}
