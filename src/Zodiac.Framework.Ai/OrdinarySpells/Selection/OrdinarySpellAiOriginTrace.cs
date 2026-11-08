using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>One visited origin's immutable remaining-aim and RNG interval; empty visits spend no draws.</summary>
public sealed record OrdinarySpellAiOriginTrace(OrdinarySpellAiOrigin Origin, RandomState EntryRng, RandomState RngState,
    ImmutableArray<OrdinarySpellAiAim> RemainingAtEntry, ImmutableArray<OrdinarySpellAiAim> RemainingAtExit,
    ImmutableArray<OrdinarySpellAiAim> ConsumedAims, ImmutableArray<OrdinarySpellAiAimEvaluation> Evaluations,
    bool OriginStoppedUnresolved);
