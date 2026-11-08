using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Fresh potential masks, evaluations and the strict best at one origin. No executable plan or lottery.</summary>
public sealed record DefaultAbilityAiSearchOutcome(DefaultAbilityAiAimOutcome Aims,
    ImmutableArray<DefaultAbilityAiSearchPass> Passes, DefaultAbilityAiAimEvaluation? Best);
