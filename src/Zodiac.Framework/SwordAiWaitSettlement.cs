using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Certifies ordinary unrestricted turn end, with no unrepresented post-turn or event effects.</summary>
public sealed record SwordAiWaitSettlementProfile(bool OrdinaryTurnEndContext = false);
public enum SwordAiWaitSettlementRejection { None, UnsupportedContext, AuthorityRejected, FacingRejected, TransitionRejected }
/// <summary>Rejected operations retain the identical input state, no events and no partial facing.</summary>
public sealed record SwordAiWaitSettlementResult(bool Accepted, SwordAiWaitSettlementRejection Rejection,
    SwordAiWaitFacingRejection FacingRejection, FacingTurnResult Transition, SwordAiWaitFacingOutcome? Facing)
{
    public FacingTurnState State => Transition.State;
    public ImmutableArray<AttackResultEvent> Events => Transition.Events;
}

/// <summary>Opt-in fresh AI Wait-facing application through existing active/receipt authority.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df, ZOD-152/153. No selected plan, decision-RNG commit or host activation.</remarks>
public static class SwordAiWaitSettlement
{
    /// <summary>End the active turn once, including its unused-choice CT refund, then publish fresh facing.</summary>
    public static SwordAiWaitSettlementResult Wait(FacingTurnState state, BattleUnitId actorId, BattleUnitId targetId,
        FlatOrdinaryMovementProfile movementProfile, SwordAiWaitFacingProfile waitProfile,
        SwordAiWaitSettlementProfile settlementProfile) =>
        Apply(state, actorId, targetId, null, movementProfile, waitProfile, settlementProfile, completing: false);

    /// <summary>Finish an already-ended turn using its exact receipt; do not repeat Wait, damage or RNG.</summary>
    public static SwordAiWaitSettlementResult Finish(FacingTurnState state, BattleUnitId actorId, BattleUnitId targetId,
        FacingCompletionReceipt? receipt, FlatOrdinaryMovementProfile movementProfile, SwordAiWaitFacingProfile waitProfile,
        SwordAiWaitSettlementProfile settlementProfile) =>
        Apply(state, actorId, targetId, receipt, movementProfile, waitProfile, settlementProfile, completing: true);

    private static SwordAiWaitSettlementResult Apply(FacingTurnState state, BattleUnitId actorId, BattleUnitId targetId,
        FacingCompletionReceipt? receipt, FlatOrdinaryMovementProfile movementProfile, SwordAiWaitFacingProfile waitProfile,
        SwordAiWaitSettlementProfile settlementProfile, bool completing)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(movementProfile);
        ArgumentNullException.ThrowIfNull(waitProfile);
        ArgumentNullException.ThrowIfNull(settlementProfile);
        SwordAiWaitSettlementResult Reject(SwordAiWaitSettlementRejection reason,
            FacingTurnRejection authority = FacingTurnRejection.None, SwordAiWaitFacingRejection facing = SwordAiWaitFacingRejection.None) =>
            new(false, reason, facing, FacingTurnBattle.Reject(state, authority), null);
        if (!settlementProfile.OrdinaryTurnEndContext) return Reject(SwordAiWaitSettlementRejection.UnsupportedContext);
        var guard = completing ? FacingTurnBattle.ValidateCompletion(state, actorId, receipt) : FacingTurnBattle.ValidateWait(state, actorId);
        if (guard != FacingTurnRejection.None) return Reject(SwordAiWaitSettlementRejection.AuthorityRejected, guard);
        var query = completing
            ? SwordAiWaitFacing.EvaluateAuthorizedCompletion(state, actorId, targetId, receipt, movementProfile, waitProfile)
            : SwordAiWaitFacing.Evaluate(state.Battle, actorId, targetId, movementProfile, waitProfile, state.Battle.TurnBattle.Turns.RngState);
        if (!query.Accepted) return Reject(SwordAiWaitSettlementRejection.FacingRejected, facing: query.Rejection);
        var facing = query.Outcome!;
        var choice = new FacingChoice.Fixed(facing.Facing);
        var result = completing ? FacingTurnBattle.FinishFacing(state, actorId, receipt, choice)
            : FacingTurnBattle.WaitWithFacing(state, actorId, choice);
        return new(result.Accepted, result.Accepted ? SwordAiWaitSettlementRejection.None : SwordAiWaitSettlementRejection.TransitionRejected,
            SwordAiWaitFacingRejection.None, result, result.Accepted ? facing : null);
    }
}
