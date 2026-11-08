using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>One fresh ability/scenario's ordinary caller passes and final rank-only baseline gate. No ranked insertion or commands.</summary>
public sealed record OrdinarySpellAiAbilitySearchOutcome(OrdinarySpellAiOriginOutcome Origins,
    ImmutableArray<OrdinarySpellAiAbilityPass> Passes, OrdinarySpellAiAbilityBest? Best, bool PassesBaseline);
