using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Pure first-KO lifecycle over the established bounded movement, clock and attack rules.</summary>
public static class TwoUnitBattleScenario
{
    public static TwoUnitBattleResult Apply(TwoUnitBattleState state, TwoUnitBattleCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        // Completion takes precedence for every subsequent command; no clock/body/death processing runs.
        if (state.Outcome != TwoUnitBattleOutcome.InProgress) return Reject(state, TwoUnitBattleRejection.AlreadyCompleted);
        AttackBattleResult result;
        switch (command)
        {
            case TwoUnitBattleCommand.UseTargetedAbility targeted:
                if (state.Definition.TargetedAbilities is not { } targetedProfile)
                    return Reject(state, TwoUnitBattleRejection.UnsupportedCommand);
                result = TargetedAbilityBattleSimulator.Apply(state.Battle, new(targeted.ActorId, targeted.AbilityId, targeted.TargetId), targetedProfile); break;
            case TwoUnitBattleCommand.UseAbility ability:
                if (state.Definition.ImmediateAbilities is not { } profile)
                    return Reject(state, TwoUnitBattleRejection.UnsupportedCommand);
                result = ImmediateAbilityBattleSimulator.Apply(state.Battle, new(ability.ActorId, ability.AbilityId), profile); break;
            case TwoUnitBattleCommand.Advance:
                result = AttackBattleSimulator.Advance(state.Battle); break;
            case TwoUnitBattleCommand.Move move:
                result = AttackBattleSimulator.Move(state.Battle, new(move.UnitId, move.Destination), state.Definition.MovementPolicy); break;
            case TwoUnitBattleCommand.Wait wait:
                result = AttackBattleSimulator.Wait(state.Battle, wait.UnitId); break;
            case TwoUnitBattleCommand.Attack attack:
                var actor = state.Battle.Combatants.FirstOrDefault(unit => unit.UnitId == attack.ActorId);
                BoundedSwordWeapon? weapon = actor?.RightHandWeaponId is { } weaponId
                    ? state.Definition.Weapons.GetValueOrDefault(weaponId) : null;
                result = AttackBattleSimulator.Attack(state.Battle, new(attack.ActorId, attack.TargetId), weapon); break;
            default: return Reject(state, TwoUnitBattleRejection.UnsupportedCommand);
        }
        if (!result.Accepted)
            return new(false, TwoUnitBattleRejection.None, result.AttackRejection, result.TurnRejection, result.MovementRejection, state, []);

        var first = result.State.Combatants.Single(unit => unit.UnitId == state.Definition.FirstUnitId);
        var second = result.State.Combatants.Single(unit => unit.UnitId == state.Definition.SecondUnitId);
        var outcome = first.Hp == 0 ? TwoUnitBattleOutcome.SecondUnitWon : second.Hp == 0 ? TwoUnitBattleOutcome.FirstUnitWon : TwoUnitBattleOutcome.InProgress;
        var next = new TwoUnitBattleState(state.Definition, result.State, outcome);
        var events = result.Events.Select(ev => (TwoUnitBattleEvent)new TwoUnitBattleTransitionEvent(ev)).ToList();
        if (next.WinnerId is { } winner)
            events.Add(new TwoUnitBattleCompletedEvent(state.Definition.Id, winner,
                winner == state.Definition.FirstUnitId ? state.Definition.SecondUnitId : state.Definition.FirstUnitId));
        return new(true, TwoUnitBattleRejection.None, AttackCommandRejection.None, TurnCycleRejection.None,
            BattleTransitionRejection.None, next, events.ToImmutableArray());
    }

    private static TwoUnitBattleResult Reject(TwoUnitBattleState state, TwoUnitBattleRejection rejection) =>
        new(false, rejection, AttackCommandRejection.None, TurnCycleRejection.None, BattleTransitionRejection.None, state, []);
}
