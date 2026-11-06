using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record MagicResultFinalizationResult(bool Accepted, MagicResultFinalizationRejection Rejection,
    MagicResultFrame? Outcome);
