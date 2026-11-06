using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Bounded formula result and separate evade diagnostics, never a live-state transition.</summary>
public sealed record FaithMagicResolutionResult(bool Accepted, FaithMagicResolutionRejection Rejection,
    MagicResultFrame? Outcome, MagicEvasionOutcome? Evasion, MagicConditionalProcOutcome? Proc, RandomState RngState);
