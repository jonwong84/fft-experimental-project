using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>One visited origin's immutable remaining-aim and RNG interval; empty visits spend no draws.</summary>
public sealed record DefaultAbilityAiOriginTrace(DefaultAbilityAiOrigin Origin, RandomState EntryRng, RandomState RngState,
    ImmutableArray<DefaultAbilityAiAim> RemainingAtEntry, ImmutableArray<DefaultAbilityAiAim> RemainingAtExit,
    ImmutableArray<DefaultAbilityAiAim> ConsumedAims, ImmutableArray<DefaultAbilityAiAimEvaluation> Evaluations,
    bool OriginStoppedUnresolved);
