using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record OrdinarySpellAiMovementResult(bool Accepted, string? Rejection,
    OrdinarySpellAiMovementOutcome? Outcome, RandomState RngState);
