using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Atomic diagnostic result. Unsupported work restores entry RNG and returns no partial best.</summary>
public sealed record DefaultAbilityAiSearchResult(bool Accepted, string? Rejection,
    DefaultAbilityAiSearchOutcome? Outcome, RandomState RngState);
