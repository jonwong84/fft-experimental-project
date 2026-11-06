using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed partial class RosterDecisionContext
{
    private RosterDecision<T> PlannedMove<T>(MoveUnitCommand command, BattleUnitId followupActor,
        Func<AttackBattleSnapshot, AttackCommandRejection> validate, Func<RosterFacingTurnState, RosterFacingResult> followup,
        RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command);
        return Issue(command.UnitId, rng, RosterDecisionChoices.BothUnused, state => {
            var moved = RosterFacingTurnBattle.Move(state, command);
            return Applied(moved, diagnostic) with { Continuation = moved.Accepted ? new(moved.State, ActorId, followup) : null };
        }, state => followupActor != ActorId ? AttackCommandRejection.InactiveActor : validate(state));
    }

    public RosterDecision<T> PrepareMoveThenAttack<T>(MoveUnitCommand move, AttackUnitCommand attack,
        BoundedSwordWeapon? weapon, RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(attack);
        return PlannedMove(move, attack.ActorId, state => KnownLivingTarget(state, attack.TargetId),
            state => RosterFacingTurnBattle.Attack(state, attack, weapon), rng, diagnostic);
    }
    public RosterDecision<T> PrepareMoveThenImmediateAbility<T>(MoveUnitCommand move, UseImmediateAbilityCommand command,
        ImmediateAbilityBattleProfile profile, RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile);
        return PlannedMove(move, command.ActorId, state => ImmediateAbilityBattleSimulator.ValidateCommand(state, command, profile),
            state => RosterFacingTurnBattle.UseImmediateAbility(state, command, profile), rng, diagnostic);
    }
    public RosterDecision<T> PrepareMoveThenTargetedAbility<T>(MoveUnitCommand move, UseTargetedAbilityCommand command,
        TargetedAbilityBattleProfile profile, FlatRosterProjectileHeights heights, RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(heights);
        return PlannedMove(move, command.ActorId, state => {
            var target = KnownLivingTarget(state, command.TargetId);
            if (target != AttackCommandRejection.None) return target;
            var setup = TargetedAbilityBattleSimulator.ValidateRosterAbilitySetup(state, ActorId, command.AbilityId, profile, Profile);
            if (setup != AttackCommandRejection.None) return setup;
            return heights.Units.Count == Profile.Units.Count && heights.Units.Keys.ToHashSet().SetEquals(Profile.Units.Keys)
                ? AttackCommandRejection.None : AttackCommandRejection.UnsupportedSetup;
        }, state => RosterFacingTurnBattle.UseTargetedAbility(state, command, profile, heights), rng, diagnostic);
    }
    public RosterDecision<T> PrepareMoveThenWait<T>(MoveUnitCommand move, BattleFacing? facing, RandomState rng, T diagnostic) =>
        PlannedMove(move, ActorId, _ => facing is { } f && !Enum.IsDefined(f) ? AttackCommandRejection.UnsupportedSetup : AttackCommandRejection.None,
            state => RosterFacingTurnBattle.WaitWithFacing(state, ActorId, facing), rng, diagnostic);

    public RosterDecision<T> PrepareMoveThenDashAbility<T>(MoveUnitCommand move, UseTargetedAbilityCommand command,
        DashAbilityBattleProfile profile, RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile);
        return PlannedMove(move, command.ActorId, state => {
            var target = KnownLivingTarget(state, command.TargetId);
            return target != AttackCommandRejection.None ? target : DashAbilityBattleSimulator.ValidateAbilitySetup(state, command.ActorId, command.AbilityId, profile, Profile);
        }, state => RosterFacingTurnBattle.UseDashAbility(state, command, profile), rng, diagnostic);
    }

    private AttackCommandRejection KnownLivingTarget(AttackBattleSnapshot state, BattleUnitId target)
    {
        if (target == ActorId) return AttackCommandRejection.SelfTarget;
        var unit = state.Combatants.FirstOrDefault(u => u.UnitId == target);
        return unit is null ? AttackCommandRejection.UnknownTarget : unit.Hp == 0 ? AttackCommandRejection.DeadTarget : AttackCommandRejection.None;
    }
}
