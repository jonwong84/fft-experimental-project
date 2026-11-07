using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Read-only live input and concrete command factories. No public constructor or custom command delegate.</summary>
public sealed partial class RosterDecisionContext
{
    private bool closed;
    public AttackBattleSnapshot Battle { get; }
    public FlatRosterBattleProfile Profile { get; }
    public BattleUnitId ActorId { get; }
    public RandomState RngState => Battle.TurnBattle.Turns.RngState;
    internal RosterDecisionContext(RosterFacingTurnState state, BattleUnitId actorId)
    { Battle = state.Battle; Profile = state.Profile; ActorId = actorId; }
    internal void Close() => closed = true;
    private void CheckOpen() { if (closed) throw new InvalidOperationException("The roster planning invocation has ended."); }
    internal RosterDecision<T> Issue<T>(BattleUnitId actor, RandomState rng, RosterDecisionChoices choices,
        Func<RosterFacingTurnState, RosterDecisionResult<T>> application,
        Func<AttackBattleSnapshot, AttackCommandRejection>? admission = null)
    { CheckOpen(); return new(this, actor, rng, choices, application, admission); }
    internal static RosterDecisionResult<T> Applied<T>(RosterFacingResult result, T diagnostic) =>
        new(result.Accepted ? FacingDecisionRejection.None : FacingDecisionRejection.CommandRejected, result, result.Accepted ? diagnostic : default);

    public RosterDecision<T> Reject<T>() { CheckOpen(); return new(this, ActorId, RngState, RosterDecisionChoices.Any, null); }
    public RosterDecision<T> PrepareAttack<T>(AttackUnitCommand command, BoundedSwordWeapon? weapon, RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command);
        return Issue(command.ActorId, rng, RosterDecisionChoices.Any, state => Applied(RosterFacingTurnBattle.Attack(state, command, weapon), diagnostic));
    }
    public RosterDecision<T> PrepareImmediateAbility<T>(UseImmediateAbilityCommand command, ImmediateAbilityBattleProfile profile, RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile);
        return Issue(command.ActorId, rng, RosterDecisionChoices.Any, state => Applied(RosterFacingTurnBattle.UseImmediateAbility(state, command, profile), diagnostic));
    }
    public RosterDecision<T> PrepareTargetedAbility<T>(UseTargetedAbilityCommand command, TargetedAbilityBattleProfile profile,
        FlatRosterProjectileHeights heights, RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(heights);
        return Issue(command.ActorId, rng, RosterDecisionChoices.Any, state => Applied(RosterFacingTurnBattle.UseTargetedAbility(state, command, profile, heights), diagnostic));
    }
    public RosterDecision<T> PrepareWait<T>(BattleFacing? facing, RandomState rng, T diagnostic) =>
        Issue(ActorId, rng, RosterDecisionChoices.Any, state => Applied(RosterFacingTurnBattle.WaitWithFacing(state, ActorId, facing), diagnostic));

    public RosterDecision<T> PrepareDashAbility<T>(UseTargetedAbilityCommand command, DashAbilityBattleProfile profile, RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile);
        return Issue(command.ActorId, rng, RosterDecisionChoices.Any, state => Applied(RosterFacingTurnBattle.UseDashAbility(state, command, profile), diagnostic));
    }

    /// <summary>Atomically publish a post-ACT actual path and its receipt-bound explicit facing.</summary>
    public RosterDecision<T> PrepareTurnEndingMove<T>(MoveUnitCommand command, BattleFacing? facing, RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command);
        return Issue(command.UnitId, rng, RosterDecisionChoices.ActUsedMoveUnused, state => {
            var move = RosterFacingTurnBattle.Move(state, command);
            if (!move.Accepted) return Applied(move, diagnostic);
            var finished = RosterFacingTurnBattle.FinishFacing(move.State, ActorId, move.State.PendingFacing, facing);
            return finished.Accepted ? Applied(finished with { Events = move.Events.AddRange(finished.Events), Path = move.Path }, diagnostic)
                : Applied(finished, diagnostic);
        });
    }
}
