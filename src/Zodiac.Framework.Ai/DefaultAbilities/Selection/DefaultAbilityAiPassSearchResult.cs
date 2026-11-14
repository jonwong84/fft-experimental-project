using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record DefaultAbilityAiPassSearchResult(bool Accepted, string? Rejection,
    DefaultAbilityAiPassSearchOutcome? Outcome, RandomState RngState);
