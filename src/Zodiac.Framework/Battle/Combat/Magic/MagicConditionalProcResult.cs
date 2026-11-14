using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record MagicConditionalProcResult(bool Accepted, MagicProcRejection Rejection,
    MagicConditionalProcOutcome? Outcome, RandomState RngState);
