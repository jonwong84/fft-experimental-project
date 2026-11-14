using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record DefaultAbilityAiMovementResult(bool Accepted, string? Rejection,
    DefaultAbilityAiMovementOutcome? Outcome, RandomState RngState);
