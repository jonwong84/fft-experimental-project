using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record DefaultAbilityAiAbilitySearchResult(bool Accepted, string? Rejection,
    DefaultAbilityAiAbilitySearchOutcome? Outcome, RandomState RngState);
