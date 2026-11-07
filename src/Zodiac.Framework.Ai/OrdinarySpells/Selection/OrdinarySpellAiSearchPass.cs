using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public sealed record OrdinarySpellAiSearchPass(OrdinarySpellAiAimPass Potential,
    ImmutableArray<OrdinarySpellAiAimEvaluation> Evaluations, bool OriginStoppedUnresolved);
