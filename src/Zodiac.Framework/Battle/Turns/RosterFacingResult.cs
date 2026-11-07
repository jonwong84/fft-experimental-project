using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record RosterFacingResult(bool Accepted, FacingTurnRejection FacingRejection,
    AttackCommandRejection AttackRejection, TurnCycleRejection TurnRejection, FlatMovementRejection PathRejection,
    RosterFacingTurnState State, ImmutableArray<AttackResultEvent> Events, FlatMovementPath? Path = null);
