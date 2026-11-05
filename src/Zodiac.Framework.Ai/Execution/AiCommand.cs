using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public enum AiCommandKind { Move, Attack, ThrowStone, Accumulate, End, Dash }
public enum AiCommandRejection { UnavailableChoices, UnsupportedDispatchContext, SelectionRejected, DispatchMismatch, BoundaryRejected }

public sealed record AiDispatchProfile(bool InitializedActorContext = false,
    bool PresentCompactRecords = false, bool CanonicalCoordinates = false, bool OrdinaryContinuation = false);
public sealed record AiCommandFailure(AiCommandRejection Reason,
    AiDecisionFailure? Selection = null, FacingDecisionRejection Boundary = FacingDecisionRejection.None);
public sealed record AiCommandOutcome(AiDecision Decision,
    RandomState DecisionRng, AiCommandKind Kind, bool AfterAct);
public sealed record AiCommandResult(AiCommandFailure? Failure,
    RosterDecisionResult<AiCommandOutcome> Execution, AiPriorityWorkspace Workspace)
{
    public bool Accepted => Execution.Accepted;
    public RosterFacingTurnState State => Execution.State;
    public ImmutableArray<AttackResultEvent> Events => Execution.Events;
    public FlatMovementPath? Path => Execution.Path;
    public RosterMoveContinuation? Continuation => Execution.Continuation;
    public AiCommandOutcome? Outcome => Execution.Diagnostic;
}
