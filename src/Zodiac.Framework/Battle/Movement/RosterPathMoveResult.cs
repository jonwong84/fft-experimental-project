using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record RosterPathMoveResult(bool Accepted, TurnCycleRejection TurnRejection,
    FlatMovementRejection PathRejection, AttackBattleSnapshot State, ImmutableArray<AttackResultEvent> Events, FlatMovementPath? Path);
