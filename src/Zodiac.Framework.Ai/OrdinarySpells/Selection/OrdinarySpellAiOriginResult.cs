using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record OrdinarySpellAiOriginResult(bool Accepted, string? Rejection,
    OrdinarySpellAiOriginOutcome? Outcome, RandomState RngState);
