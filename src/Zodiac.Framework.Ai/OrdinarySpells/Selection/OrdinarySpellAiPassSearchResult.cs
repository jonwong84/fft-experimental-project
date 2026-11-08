using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record OrdinarySpellAiPassSearchResult(bool Accepted, string? Rejection,
    OrdinarySpellAiPassSearchOutcome? Outcome, RandomState RngState);
