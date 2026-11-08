using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record DefaultAbilityAiOriginResult(bool Accepted, string? Rejection,
    DefaultAbilityAiOriginOutcome? Outcome, RandomState RngState);
