using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record DefaultAbilityAiOriginEvaluationResult(bool Accepted, string? Rejection,
    DefaultAbilityAiOriginEvaluationOutcome? Outcome, RandomState RngState);
