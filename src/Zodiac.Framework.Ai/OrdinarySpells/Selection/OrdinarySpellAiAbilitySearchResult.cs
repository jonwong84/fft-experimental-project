using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record OrdinarySpellAiAbilitySearchResult(bool Accepted, string? Rejection,
    OrdinarySpellAiAbilitySearchOutcome? Outcome, RandomState RngState);
