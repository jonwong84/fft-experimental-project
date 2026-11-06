namespace Zodiac.Framework;

/// <summary>Compatibility API for adjacent physical delivery. Shared admission and settlement own executable behavior.</summary>
public static class DashAbilityBattleSimulator
{
    public static AttackBattleResult ApplyRoster(AttackBattleSnapshot state, UseTargetedAbilityCommand command,
        DashAbilityBattleProfile profile, FlatRosterBattleProfile roster) =>
        PhysicalAbilityBattleSimulator.ApplyRoster(state, command, (profile ?? throw new ArgumentNullException(nameof(profile))).PhysicalProfile, roster);

    public static AttackCommandRejection ValidateAbilitySetup(AttackBattleSnapshot state, BattleUnitId actor, AssetId ability,
        DashAbilityBattleProfile profile, FlatRosterBattleProfile roster) =>
        PhysicalAbilityBattleSimulator.ValidateRosterAbilitySetup(state, actor, ability, (profile ?? throw new ArgumentNullException(nameof(profile))).PhysicalProfile, roster);

    public static AttackCommandRejection ValidateCommand(AttackBattleSnapshot state, UseTargetedAbilityCommand command,
        DashAbilityBattleProfile profile, FlatRosterBattleProfile roster) =>
        PhysicalAbilityBattleSimulator.ValidateRosterCommand(state, command, (profile ?? throw new ArgumentNullException(nameof(profile))).PhysicalProfile, roster);
}
