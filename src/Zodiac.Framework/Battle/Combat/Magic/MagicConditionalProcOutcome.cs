using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Status-call decision and a flag-set delta, not a final flag value or applied status.</summary>
public sealed record MagicConditionalProcOutcome(MagicProcDisposition Disposition, bool SetAddedEffectFlag,
    uint? Raw = null, int? Percent = null)
{
    public bool Invoked => Disposition is MagicProcDisposition.Passed or MagicProcDisposition.Failed;
    public bool ShouldApplyStatus => Disposition == MagicProcDisposition.Passed;
}
