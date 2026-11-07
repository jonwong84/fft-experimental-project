namespace Zodiac.Framework;

public sealed record RosterMoveContinueResult(FacingMoveRejection Rejection, RosterFacingResult Transition)
{
    public bool Accepted => Rejection == FacingMoveRejection.None && Transition.Accepted;
    public RosterFacingTurnState State => Transition.State;
    public System.Collections.Immutable.ImmutableArray<AttackResultEvent> Events => Transition.Events;
}
