using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record FacingDecisionResult<T>(FacingDecisionRejection Rejection, FacingTurnResult Transition, T? Diagnostic)
{
    public bool Accepted => Transition.Accepted;
    public FacingTurnState State => Transition.State;
    public ImmutableArray<AttackResultEvent> Events => Transition.Events;
    public FlatMovementPath? Path { get; init; }
    public FlatMovementRejection PathRejection { get; init; }
    public FacingMoveContinuation? Continuation { get; init; }
    public FacingMoveRejection MoveRejection { get; init; }
}
