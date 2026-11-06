using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record FacingMoveContinueResult(FacingMoveRejection Rejection, FacingTurnResult Transition)
{
    public bool Accepted => Rejection == FacingMoveRejection.None && Transition.Accepted;
    public FacingTurnState State => Transition.State;
    public ImmutableArray<AttackResultEvent> Events => Transition.Events;
}
