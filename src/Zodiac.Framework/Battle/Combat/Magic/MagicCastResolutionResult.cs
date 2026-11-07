using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record MagicCastResolutionResult(bool Accepted, FaithMagicResolutionRejection Rejection,
    MagicCastOutcome? Outcome, RandomState RngState);
