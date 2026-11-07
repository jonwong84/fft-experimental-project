using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Pure self-action transition. The profile certifies the absence of unmodeled modifiers; no live RNG draws.</summary>
public static class ImmediateAbilityBattleSimulator
{
    public static AttackBattleResult Apply(AttackBattleSnapshot state, UseImmediateAbilityCommand command, ImmediateAbilityBattleProfile profile)
    {
        var invalid = ValidateCommand(state, command, profile);
        if (invalid != AttackCommandRejection.None) return new(false, invalid, TurnCycleRejection.None, BattleTransitionRejection.None, state, []);
        var actor = state.Combatants.Single(unit => unit.UnitId == command.ActorId);
        var action = TurnBattleSimulator.ConfirmAct(state.TurnBattle, actor.UnitId);
        if (!action.Accepted)
            return new(false, AttackCommandRejection.None, action.TurnRejection, action.MovementRejection, state, []);
        int pa = AccumulateFormula.IncreasePhysicalAttack(actor.PhysicalAttack);
        var next = new AttackBattleSnapshot(action.State, state.Combatants.Select(unit =>
            unit.UnitId == actor.UnitId ? unit.WithPhysicalAttack(pa) : unit));
        var events = action.Events.Select(ev => (AttackResultEvent)new AttackTurnBattleEvent(ev)).ToList();
        events.Add(new ImmediateAbilityResolvedEvent(actor.UnitId, command.AbilityId, actor.PhysicalAttack, pa));
        if (pa != actor.PhysicalAttack) events.Add(new AttackPhysicalAttackChangedEvent(actor.UnitId, actor.PhysicalAttack, pa));
        return new(true, AttackCommandRejection.None, TurnCycleRejection.None, BattleTransitionRejection.None, next, events.ToImmutableArray());
    }

    /// <summary>Pure current self-command admission shared by direct ACT and frozen Move-first intent checks.</summary>
    public static AttackCommandRejection ValidateCommand(AttackBattleSnapshot state, UseImmediateAbilityCommand command, ImmediateAbilityBattleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(profile);
        var actor = state.Combatants.FirstOrDefault(unit => unit.UnitId == command.ActorId);
        if (actor is null) return AttackCommandRejection.UnknownActor;
        if (state.TurnBattle.Turns.ActiveUnitId != actor.UnitId) return AttackCommandRejection.InactiveActor;
        if (state.TurnBattle.Turns.Units.Single(unit => unit.Id == actor.UnitId).ActionTaken)
            return AttackCommandRejection.ActionAlreadyTaken;
        if (!profile.Abilities.ContainsKey(command.AbilityId)) return AttackCommandRejection.UnknownAbility;
        if (!profile.Units.TryGetValue(actor.UnitId, out var grant) || !grant.Abilities.Contains(command.AbilityId))
            return AttackCommandRejection.AbilityNotGranted;
        try { profile.ValidateState(state); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        { return AttackCommandRejection.UnsupportedSetup; }
        return state.Combatants.Any(unit => unit.Status == AttackUnitStatus.Dead) ? AttackCommandRejection.UnsupportedSetup : AttackCommandRejection.None;
    }
}
