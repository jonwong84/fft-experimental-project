using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Rejections retain the identical wrapper, empty events and no partial path.</summary>
public sealed record PathMoveResult(bool Accepted, FacingTurnRejection FacingRejection,
    TurnCycleRejection TurnRejection, FlatMovementRejection PathRejection, FacingTurnState State,
    ImmutableArray<AttackResultEvent> Events, FlatMovementPath? Path);
