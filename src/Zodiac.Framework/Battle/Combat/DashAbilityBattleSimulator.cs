using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Actual roster Dash admission, sharing formula 0x37 results, occupancy and choice settlement with Stone.</summary>
/// <remarks>ZOD-319; decomp 11464943bbdbd2a0af022d90cb7f10455cf911df. Flat living roster only; no reactions/supports or height expansion.</remarks>
public static class DashAbilityBattleSimulator
{
    public static AttackBattleResult ApplyRoster(AttackBattleSnapshot state, UseTargetedAbilityCommand command,
        DashAbilityBattleProfile profile, FlatRosterBattleProfile roster)
    {
        var invalid = ValidateCommand(state, command, profile, roster);
        return invalid == AttackCommandRejection.None ? TargetedAbilityBattleSimulator.ApplyRandomPa(state, command, dash: true)
            : new(false, invalid, TurnCycleRejection.None, BattleTransitionRejection.None, state, []);
    }
    public static AttackCommandRejection ValidateAbilitySetup(AttackBattleSnapshot state, BattleUnitId actor, AssetId ability,
        DashAbilityBattleProfile profile, FlatRosterBattleProfile roster)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(roster);
        if (!state.Combatants.Any(u => u.UnitId == actor)) return AttackCommandRejection.UnknownActor;
        if (!profile.Abilities.ContainsKey(ability)) return AttackCommandRejection.UnknownAbility;
        if (!profile.Units.TryGetValue(actor, out var grant) || !grant.Abilities.Contains(ability)) return AttackCommandRejection.AbilityNotGranted;
        try { roster.ValidateState(state); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException) { return AttackCommandRejection.UnsupportedSetup; }
        return profile.Ground != roster.Ground || !profile.Units.Keys.ToHashSet().SetEquals(roster.Units.Keys) ||
            state.Combatants.Any(u => u.Hp == 0 || u.PhysicalAttack is < 1 or > 99) ? AttackCommandRejection.UnsupportedSetup : AttackCommandRejection.None;
    }
    public static AttackCommandRejection ValidateCommand(AttackBattleSnapshot state, UseTargetedAbilityCommand command,
        DashAbilityBattleProfile profile, FlatRosterBattleProfile roster)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(command);
        if (!state.Combatants.Any(u => u.UnitId == command.ActorId)) return AttackCommandRejection.UnknownActor;
        var target = state.Combatants.FirstOrDefault(u => u.UnitId == command.TargetId);
        if (target is null) return AttackCommandRejection.UnknownTarget;
        if (state.TurnBattle.Turns.ActiveUnitId != command.ActorId) return AttackCommandRejection.InactiveActor;
        if (state.TurnBattle.Turns.Units.Single(u => u.Id == command.ActorId).ActionTaken) return AttackCommandRejection.ActionAlreadyTaken;
        if (command.ActorId == command.TargetId) return AttackCommandRejection.SelfTarget;
        if (target.Status == AttackUnitStatus.Dead) return AttackCommandRejection.DeadTarget;
        var setup = ValidateAbilitySetup(state, command.ActorId, command.AbilityId, profile, roster);
        if (setup != AttackCommandRejection.None) return setup;
        var origin = state.TurnBattle.Battle.Units.Single(u => u.Id == command.ActorId).Position;
        var to = state.TurnBattle.Battle.Units.Single(u => u.Id == command.TargetId).Position;
        return Math.Abs(origin.X - to.X) + Math.Abs(origin.Y - to.Y) == 1 ? AttackCommandRejection.None : AttackCommandRejection.OutOfRange;
    }
}
