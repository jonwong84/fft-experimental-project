using System.Collections.Immutable;

namespace Zodiac.Framework;

internal enum FacingDecisionKind { Rejected, Attack, Wait }

/// <summary>An opaque proposal issued by one planning invocation; never a battle-state or receipt factory.</summary>
public sealed class FacingDecision<T>
{
    internal FacingDecisionContext Owner { get; }
    internal FacingDecisionKind Kind { get; }
    internal FftRandomState Rng { get; }
    internal AttackUnitCommand? Attack { get; }
    internal BoundedSwordWeapon? Weapon { get; }
    internal FacingChoice? Facing { get; }
    internal T? Diagnostic { get; }
    internal FacingDecision(FacingDecisionContext owner, FacingDecisionKind kind, FftRandomState rng,
        T? diagnostic, AttackUnitCommand? attack = null, BoundedSwordWeapon? weapon = null, FacingChoice? facing = null)
    { Owner = owner; Kind = kind; Rng = rng; Diagnostic = diagnostic; Attack = attack; Weapon = weapon; Facing = facing; }
}

/// <summary>Read-only planning input with invocation-bound proposal factories; valid only during the callback.</summary>
public sealed class FacingDecisionContext
{
    private bool closed;
    public AttackBattleSnapshot Battle { get; }
    public BattleUnitId ActorId { get; }
    public FftRandomState RngState => Battle.TurnBattle.Turns.RngState;
    internal FacingDecisionContext(AttackBattleSnapshot battle, BattleUnitId actorId) { Battle = battle; ActorId = actorId; }
    internal void Close() => closed = true;
    private void CheckOpen()
    { if (closed) throw new InvalidOperationException("The planning invocation has ended."); }
    public FacingDecision<T> Reject<T>()
    { CheckOpen(); return new(this, FacingDecisionKind.Rejected, RngState, default); }
    public FacingDecision<T> PrepareAttack<T>(AttackUnitCommand command, BoundedSwordWeapon? weapon, FftRandomState rng, T diagnostic)
    { CheckOpen(); ArgumentNullException.ThrowIfNull(command); return new(this, FacingDecisionKind.Attack, rng, diagnostic, command, weapon); }
    public FacingDecision<T> PrepareWait<T>(FacingChoice facing, FftRandomState rng, T diagnostic)
    { CheckOpen(); ArgumentNullException.ThrowIfNull(facing); return new(this, FacingDecisionKind.Wait, rng, diagnostic, facing: facing); }
}

public enum FacingDecisionRejection { None, AuthorityRejected, DecisionRejected, InvalidDecision, InvalidRandomProgress, CommandRejected }
public sealed record FacingDecisionResult<T>(FacingDecisionRejection Rejection, FacingTurnResult Transition, T? Diagnostic)
{
    public bool Accepted => Transition.Accepted;
    public FacingTurnState State => Transition.State;
    public ImmutableArray<AttackResultEvent> Events => Transition.Events;
}

/// <summary>Framework-owned fresh planning, RNG continuity and one authoritative command transaction.</summary>
public static class FacingDecisionBattle
{
    public static FacingDecisionResult<T> Execute<T>(FacingTurnState state, BattleUnitId actorId,
        Func<FacingDecisionContext, FacingDecision<T>> planner)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(planner);
        FacingDecisionResult<T> Reject(FacingDecisionRejection reason, FacingTurnResult? failed = null) =>
            new(reason, failed is null ? FacingTurnBattle.Reject(state, FacingTurnRejection.None)
                : failed with { State = state, Events = [] }, default);
        var guard = FacingTurnBattle.ValidateWait(state, actorId);
        if (guard != FacingTurnRejection.None)
            return Reject(FacingDecisionRejection.AuthorityRejected, FacingTurnBattle.Reject(state, guard));
        var context = new FacingDecisionContext(state.Battle, actorId);
        FacingDecision<T> decision;
        try { decision = planner(context); }
        finally { context.Close(); }
        if (decision is null || !ReferenceEquals(decision.Owner, context)) return Reject(FacingDecisionRejection.InvalidDecision);
        if (decision.Kind == FacingDecisionKind.Rejected) return Reject(FacingDecisionRejection.DecisionRejected);
        if (!FftBiosRandom.IsForwardCheckpoint(context.RngState, decision.Rng)) return Reject(FacingDecisionRejection.InvalidRandomProgress);
        if (decision.Kind == FacingDecisionKind.Attack && decision.Attack!.ActorId != actorId)
            return Reject(FacingDecisionRejection.AuthorityRejected, FacingTurnBattle.Reject(state, FacingTurnRejection.WrongActor));

        var old = state.Battle.TurnBattle;
        var battle = new BattleState(old.Battle.Map, old.Battle.Units, decision.Rng);
        var turns = new TurnCycleState(old.Turns.Units, decision.Rng, old.Turns.Ticks, old.Turns.Phase);
        var staged = new FacingTurnState(new(new(battle, turns), state.Battle.Combatants), state.Profile, state.Revision, null, state.Lineage);
        var result = decision.Kind == FacingDecisionKind.Attack ? FacingTurnBattle.Attack(staged, decision.Attack!, decision.Weapon)
            : FacingTurnBattle.WaitWithFacing(staged, actorId, decision.Facing!);
        return result.Accepted ? new(FacingDecisionRejection.None, result, decision.Diagnostic)
            : Reject(FacingDecisionRejection.CommandRejected, result);
    }
}
