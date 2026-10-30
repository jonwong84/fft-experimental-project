namespace Zodiac.Framework.Ai;

/// <summary>The two admitted source markers; other nonzero source bytes have different guard semantics and reject.</summary>
public enum SwordAiDecisionMarker { Undecided = 0, Decided = 0xff }
public readonly record struct SwordAiSavedChoices(BattleUnitId ActorId, bool MovementTaken, bool ActionTaken);

/// <summary>Descriptive selected data only, without scores, executable continuation or provenance authority.</summary>
public sealed record SwordAiDispatchSelection(SwordAiSelectedPlanKind Kind, BattleUnitId ActorId,
    TilePosition Destination, SwordAiCandidate? Attack);
public sealed record SwordAiDispatchRequest(BattleUnitId ActorId, BattleUnitId TargetId, SwordAiDecisionMarker Marker,
    SwordAiSavedChoices SavedChoices, SwordAiDispatchSelection? Selection);

/// <summary>Unrepresented program-counter and source facts require explicit certification.</summary>
/// <remarks>Ordinary continuation excludes mounts, restrictions, control changes and invalidating post-move effects.</remarks>
public sealed record SwordAiCommandDispatchProfile(bool InitializedActorContext = false,
    bool PresentCompactRecords = false, bool CanonicalCoordinates = false, bool OrdinaryContinuation = false,
    bool FreshSelectionCompleted = false);
public enum SwordAiDispatchKind { SelectionRequired, Move, Attack, EndTurn }
public enum SwordAiDispatchRejection
{
    None, UnsupportedContext, UnknownActor, UnknownTarget, InvalidTarget, UnavailableTurn,
    StaleActorContext, UnsupportedDecisionMarker, InvalidSelection, UnsupportedProfile
}

/// <summary>Commands are data, not authority. RecordedChoices describes normal saved-choice/history updates.</summary>
public sealed record SwordAiDispatchOutcome(SwordAiDispatchKind Kind, MoveUnitCommand? Move, AttackUnitCommand? Attack,
    SwordAiSavedChoices? RecordedChoices, bool EndedForNoProgress)
{
    public bool RecordCommand => RecordedChoices is not null;
}
public sealed record SwordAiDispatchResult(bool Accepted, SwordAiDispatchRejection Rejection,
    SwordAiDispatchOutcome? Outcome, FftRandomState RngState);

/// <summary>Pure ordinary command-builder classification; no selection, path execution or live continuation authority.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-165/166. Adds no RNG/tick/revision work.</remarks>
public static class SwordAiCommandDispatch
{
    public static SwordAiDispatchResult InspectEntry(AttackBattleSnapshot state, SwordAiDispatchRequest request,
        FlatOrdinaryMovementProfile movementProfile, SwordAiCommandDispatchProfile dispatchProfile) =>
        Evaluate(state, request, movementProfile, dispatchProfile, afterSelection: false);

    /// <summary>Dispatches within the freshly completed selection's invocation, without repeating its earlier guard.</summary>
    public static SwordAiDispatchResult DispatchFreshSelection(AttackBattleSnapshot state, SwordAiDispatchRequest request,
        FlatOrdinaryMovementProfile movementProfile, SwordAiCommandDispatchProfile dispatchProfile) =>
        Evaluate(state, request, movementProfile, dispatchProfile, afterSelection: true);

    private static SwordAiDispatchResult Evaluate(AttackBattleSnapshot state, SwordAiDispatchRequest request,
        FlatOrdinaryMovementProfile movementProfile, SwordAiCommandDispatchProfile profile, bool afterSelection)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(movementProfile);
        ArgumentNullException.ThrowIfNull(profile);
        var rng = state.TurnBattle.Turns.RngState;
        SwordAiDispatchResult Reject(SwordAiDispatchRejection reason) => new(false, reason, null, rng);
        SwordAiDispatchResult Complete(SwordAiDispatchKind kind, SwordAiSavedChoices? recorded = null,
            bool noProgress = false, MoveUnitCommand? move = null, AttackUnitCommand? attack = null) =>
            new(true, SwordAiDispatchRejection.None, new(kind, move, attack, recorded, noProgress), rng);

        if (!profile.InitializedActorContext || !profile.PresentCompactRecords || !profile.CanonicalCoordinates ||
            !profile.OrdinaryContinuation || afterSelection && !profile.FreshSelectionCompleted)
            return Reject(SwordAiDispatchRejection.UnsupportedContext);
        if (!Enum.IsDefined(request.Marker) || afterSelection && request.Marker != SwordAiDecisionMarker.Decided)
            return Reject(SwordAiDispatchRejection.UnsupportedDecisionMarker);
        var battle = state.TurnBattle.Battle;
        if (!battle.TryGetUnit(request.ActorId, out var actor)) return Reject(SwordAiDispatchRejection.UnknownActor);
        if (!battle.TryGetUnit(request.TargetId, out var target)) return Reject(SwordAiDispatchRejection.UnknownTarget);
        if (request.ActorId == request.TargetId) return Reject(SwordAiDispatchRejection.InvalidTarget);
        var turns = state.TurnBattle.Turns;
        if (turns.Phase != TurnCyclePhase.UnitTurn || turns.ActiveUnitId != request.ActorId)
            return Reject(SwordAiDispatchRejection.UnavailableTurn);
        if (request.SavedChoices.ActorId != request.ActorId) return Reject(SwordAiDispatchRejection.StaleActorContext);
        if (!movementProfile.Supports(state)) return Reject(SwordAiDispatchRejection.UnsupportedProfile);
        var turn = turns.Units.Single(u => u.Id == request.ActorId);
        var current = new SwordAiSavedChoices(request.ActorId, turn.MovementTaken, turn.ActionTaken);
        if (!afterSelection)
        {
            // This guard precedes both ACT-triggered reselection and any read of the selected command payload.
            if (request.Marker == SwordAiDecisionMarker.Decided && current == request.SavedChoices)
                return Complete(SwordAiDispatchKind.EndTurn, noProgress: true);
            if (request.Marker == SwordAiDecisionMarker.Undecided || turn.ActionTaken)
                return Complete(SwordAiDispatchKind.SelectionRequired);
        }

        var selected = request.Selection;
        if (selected is null || selected.ActorId != request.ActorId || !Enum.IsDefined(selected.Kind) ||
            selected.Destination.Layer != 0 || !battle.Map.TryGetTile(selected.Destination, out _) ||
            selected.Destination == target!.Position)
            return Reject(SwordAiDispatchRejection.InvalidSelection);
        bool actFirst;
        switch (selected.Kind)
        {
            case SwordAiSelectedPlanKind.Attack:
                if (selected.Attack is not { } candidate || candidate.ActorId != request.ActorId ||
                    candidate.TargetId != request.TargetId || candidate.Destination != selected.Destination || !Enum.IsDefined(candidate.Order))
                    return Reject(SwordAiDispatchRejection.InvalidSelection);
                actFirst = candidate.Order == SwordAiCandidateOrder.ActThenMove;
                break;
            case SwordAiSelectedPlanKind.MoveOnly:
                if (selected.Attack is not null) return Reject(SwordAiDispatchRejection.InvalidSelection);
                actFirst = false;
                break;
            default: return Reject(SwordAiDispatchRejection.InvalidSelection);
        }
        if (!turn.MovementTaken && (turn.ActionTaken || !actFirst) && selected.Destination != actor!.Position)
            return Complete(SwordAiDispatchKind.Move, current, move: new(request.ActorId, selected.Destination));
        if (turn.ActionTaken || selected.Kind == SwordAiSelectedPlanKind.MoveOnly)
            return Complete(SwordAiDispatchKind.EndTurn, current);
        return Complete(SwordAiDispatchKind.Attack, current, attack: new(request.ActorId, request.TargetId));
    }
}
