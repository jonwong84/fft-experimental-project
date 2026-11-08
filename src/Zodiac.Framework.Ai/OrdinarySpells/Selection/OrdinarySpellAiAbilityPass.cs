using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>One caller pass: fresh remaining aims, continuous RNG and cumulative best entry/exit. No insertion occurs here.</summary>
public sealed record OrdinarySpellAiAbilityPass(OrdinarySpellAiOriginPass Pass, RandomState EntryRng, RandomState RngState,
    ImmutableArray<OrdinarySpellAiOriginTrace> Traces, ImmutableArray<OrdinarySpellAiAim> RemainingAims,
    OrdinarySpellAiAbilityBest? BestAtEntry, OrdinarySpellAiAbilityBest? BestAtExit);
