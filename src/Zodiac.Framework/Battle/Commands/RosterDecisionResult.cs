using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed partial record RosterDecisionResult<T>(FacingDecisionRejection Rejection, RosterFacingResult Transition, T? Diagnostic)
{
    public bool Accepted => Rejection == FacingDecisionRejection.None && Transition.Accepted;
    public RosterFacingTurnState State => Transition.State;
    public System.Collections.Immutable.ImmutableArray<AttackResultEvent> Events => Transition.Events;
    public FlatMovementPath? Path => Transition.Path;
}
