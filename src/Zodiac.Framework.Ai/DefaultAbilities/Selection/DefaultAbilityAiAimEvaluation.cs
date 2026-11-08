using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Fresh simulation and RNG trace for an enumerated aim; it cannot be submitted as command authority.</summary>
public sealed record DefaultAbilityAiAimEvaluation(DefaultAbilityAiAim Aim, RandomState EntryRng,
    DefaultAbilityAiCandidateResult Candidate, bool ImprovedBest);
