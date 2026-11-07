using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Atomic diagnostic result. Unsupported work restores entry RNG and returns no partial best.</summary>
public sealed record OrdinarySpellAiSearchResult(bool Accepted, string? Rejection,
    OrdinarySpellAiSearchOutcome? Outcome, RandomState RngState);
