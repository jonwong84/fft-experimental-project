using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>One pass's actual origin scan, with separate best rank and baseline admission. No command authority.</summary>
public sealed record OrdinarySpellAiPassSearchOutcome(OrdinarySpellAiOriginOutcome Origins, OrdinarySpellAiOriginPass Pass,
    ImmutableArray<OrdinarySpellAiOriginTrace> Traces, OrdinarySpellAiPassBest? Best,
    ImmutableArray<OrdinarySpellAiAim> RemainingAims, bool PassesBaseline);
