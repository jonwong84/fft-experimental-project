using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>One caller pass: fresh remaining aims, continuous RNG and cumulative best entry/exit. No insertion occurs here.</summary>
public sealed record DefaultAbilityAiAbilityPass(DefaultAbilityAiOriginPass Pass, RandomState EntryRng, RandomState RngState,
    ImmutableArray<DefaultAbilityAiOriginTrace> Traces, ImmutableArray<DefaultAbilityAiAim> RemainingAims,
    DefaultAbilityAiAbilityBest? BestAtEntry, DefaultAbilityAiAbilityBest? BestAtExit);
