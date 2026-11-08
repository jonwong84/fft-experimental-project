using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Static potential passes. Actual evaluation may stop early or consume aims; this is not an execution plan.</summary>
public sealed record DefaultAbilityAiAimOutcome(DefaultAbilityAiConsidered Ability, TilePosition Origin,
    ImmutableArray<DefaultAbilityAiUsefulTarget> Usefulness, ImmutableArray<DefaultAbilityAiAimDeadline> Deadlines,
    ImmutableArray<DefaultAbilityAiAimPass> Passes);
