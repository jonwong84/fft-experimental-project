using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public sealed record DefaultAbilityAiSearchPass(DefaultAbilityAiAimPass Potential,
    ImmutableArray<DefaultAbilityAiAimEvaluation> Evaluations, bool OriginStoppedUnresolved);
