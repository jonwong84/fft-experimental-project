using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Static potential passes. Actual evaluation may stop early or consume aims; this is not an execution plan.</summary>
public sealed record OrdinarySpellAiAimOutcome(OrdinarySpellAiConsidered Ability, TilePosition Origin,
    ImmutableArray<OrdinarySpellAiUsefulTarget> Usefulness, ImmutableArray<OrdinarySpellAiAimDeadline> Deadlines,
    ImmutableArray<OrdinarySpellAiAimPass> Passes);
