using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record DefaultAbilityBattleResult(bool Accepted, DefaultAbilityBattleState State,
    string? Rejection, ImmutableArray<DefaultAbilityEvent> Events);
