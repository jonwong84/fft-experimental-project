using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record MagicEvasionResult(bool Accepted, MagicEvasionRejection Rejection,
    MagicEvasionOutcome? Outcome, RandomState RngState);
