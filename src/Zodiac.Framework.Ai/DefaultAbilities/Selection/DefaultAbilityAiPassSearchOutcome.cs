using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>One pass's actual origin scan, with separate best rank and baseline admission. No command authority.</summary>
public sealed record DefaultAbilityAiPassSearchOutcome(DefaultAbilityAiOriginOutcome Origins, DefaultAbilityAiOriginPass Pass,
    ImmutableArray<DefaultAbilityAiOriginTrace> Traces, DefaultAbilityAiPassBest? Best,
    ImmutableArray<DefaultAbilityAiAim> RemainingAims, bool PassesBaseline);
