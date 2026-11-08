using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record OrdinarySpellAiOriginEvaluationResult(bool Accepted, string? Rejection,
    OrdinarySpellAiOriginEvaluationOutcome? Outcome, RandomState RngState);
