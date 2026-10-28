namespace Zodiac.Framework.Ai;

/// <summary>Unrepresented original Wait/control and targetability facts, not team bits or command authority.</summary>
public sealed record SwordAiWaitFacingProfile(bool OriginalAiChooseAtWaitContext = false, bool EligibleOpponent = false);
public enum SwordAiWaitFacingRejection
{
    None, UnsupportedContext, UnknownActor, UnknownTarget, InvalidTarget, UnavailableTurn,
    UnsupportedProfile, OverlappingPositions
}
public sealed record SwordAiWaitFacingOutcome(BattleUnitId ActorId, BattleUnitId TargetId,
    TilePosition ActorPosition, TilePosition TargetPosition, BattleFacing Facing);
/// <summary>Every result retains the input RNG. No facing, choices, CT or ticks are applied.</summary>
public sealed record SwordAiWaitFacingResult(bool Accepted, SwordAiWaitFacingRejection Rejection,
    SwordAiWaitFacingOutcome? Outcome, FftRandomState RngState);

/// <summary>Pure ChooseAtWait resolution for two settled living ordinary opponents on clear flat ground.</summary>
/// <remarks>Reference 11464943bbdbd2a0af022d90cb7f10455cf911df, ZOD-150/151. No selected-plan inputs or host activation.</remarks>
public static class SwordAiWaitFacing
{
    public static SwordAiWaitFacingResult Evaluate(AttackBattleSnapshot state, BattleUnitId actorId, BattleUnitId targetId,
        FlatOrdinaryMovementProfile movementProfile, SwordAiWaitFacingProfile waitProfile, FftRandomState rng) =>
        EvaluateCore(state, actorId, targetId, movementProfile, waitProfile, rng, requireActive: true);

    // Only a real session completion can admit an ended turn. The public snapshot query stays active-only.
    internal static SwordAiWaitFacingResult EvaluateAuthorizedCompletion(FacingTurnState state, BattleUnitId actorId,
        BattleUnitId targetId, FacingCompletionReceipt? receipt, FlatOrdinaryMovementProfile movementProfile,
        SwordAiWaitFacingProfile waitProfile)
    {
        var rng = state.Battle.TurnBattle.Turns.RngState;
        if (FacingTurnBattle.ValidateCompletion(state, actorId, receipt) != FacingTurnRejection.None)
            return new(false, SwordAiWaitFacingRejection.UnavailableTurn, null, rng);
        return EvaluateCore(state.Battle, actorId, targetId, movementProfile, waitProfile, rng, requireActive: false);
    }

    private static SwordAiWaitFacingResult EvaluateCore(AttackBattleSnapshot state, BattleUnitId actorId, BattleUnitId targetId,
        FlatOrdinaryMovementProfile movementProfile, SwordAiWaitFacingProfile waitProfile, FftRandomState rng, bool requireActive)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(movementProfile);
        ArgumentNullException.ThrowIfNull(waitProfile);
        SwordAiWaitFacingResult Reject(SwordAiWaitFacingRejection reason) => new(false, reason, null, rng);
        if (!waitProfile.OriginalAiChooseAtWaitContext || !waitProfile.EligibleOpponent)
            return Reject(SwordAiWaitFacingRejection.UnsupportedContext);
        var battle = state.TurnBattle.Battle;
        if (!battle.TryGetUnit(actorId, out var actor)) return Reject(SwordAiWaitFacingRejection.UnknownActor);
        if (actorId == targetId) return Reject(SwordAiWaitFacingRejection.InvalidTarget);
        if (!battle.TryGetUnit(targetId, out var target)) return Reject(SwordAiWaitFacingRejection.UnknownTarget);
        var turns = state.TurnBattle.Turns;
        if (requireActive ? turns.Phase != TurnCyclePhase.UnitTurn || turns.ActiveUnitId != actorId
            : turns.Phase != TurnCyclePhase.PickUnit || turns.ActiveUnitId is not null)
            return Reject(SwordAiWaitFacingRejection.UnavailableTurn);
        if (actor!.Position == target!.Position) return Reject(SwordAiWaitFacingRejection.OverlappingPositions);
        if (!movementProfile.Supports(state)) return Reject(SwordAiWaitFacingRejection.UnsupportedProfile);

        // The one eligible enemy is returned by imminent or ordinary search. Its directional neighbor
        // lies inside the pair's bounding box and is viable on this flat rectangle, even if occupied.
        // No movement-destination lookup or recomputation of the source's cached threat delay is needed.
        var facing = FacingTurnBattle.DirectionToward(actor.Position, target.Position);
        return facing is null ? Reject(SwordAiWaitFacingRejection.OverlappingPositions)
            : new(true, SwordAiWaitFacingRejection.None, new(actorId, targetId, actor.Position, target.Position, facing.Value), rng);
    }
}
