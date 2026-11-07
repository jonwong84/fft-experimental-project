using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Fresh simulation and RNG trace for an enumerated aim; it cannot be submitted as command authority.</summary>
public sealed record OrdinarySpellAiAimEvaluation(OrdinarySpellAiAim Aim, RandomState EntryRng,
    OrdinarySpellAiCandidateResult Candidate, bool ImprovedBest);
