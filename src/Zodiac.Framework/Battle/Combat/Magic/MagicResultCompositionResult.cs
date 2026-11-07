using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record MagicResultCompositionResult(MagicResultFinalizationResult Finalization,
    MagicConditionalProcOutcome? Proc, RandomState RngState, MagicProcRejection ProcRejection = MagicProcRejection.None)
{
    public bool Accepted => Finalization.Accepted;
}
