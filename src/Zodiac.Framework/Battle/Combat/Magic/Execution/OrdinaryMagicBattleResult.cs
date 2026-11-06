using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record OrdinaryMagicBattleResult(bool Accepted, OrdinaryMagicBattleState State,
    string? Rejection, ImmutableArray<OrdinaryMagicEvent> Events);
