using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record FacingMoveResult<T>(FacingMoveRejection Rejection, FacingTurnRejection FacingRejection,
    TurnCycleRejection TurnRejection, FlatMovementRejection PathRejection, FacingTurnState State,
    ImmutableArray<AttackResultEvent> Events, FlatMovementPath? Path, FacingMoveContinuation? Continuation, T? Diagnostic)
{
    public bool Accepted => Rejection == FacingMoveRejection.None;
}
