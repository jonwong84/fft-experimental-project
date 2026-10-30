using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Validated projection of the closed immediate self PA+1 effect; not a raw formula record.</summary>
public sealed record BoundedImmediateAbility
{
    public AssetId Id { get; }
    public BoundedImmediateAbility(AssetId id)
    {
        if (string.IsNullOrWhiteSpace(id.Value)) throw new ArgumentException("An ability ID is required.", nameof(id));
        Id = id;
    }
}

/// <summary>Explicit learned-action grants and zero equipment PA bonus for this closed profile.</summary>
public sealed class ImmediateAbilityUnitGrant
{
    public BattleUnitId UnitId { get; }
    public ImmutableHashSet<AssetId> Abilities { get; }

    public ImmediateAbilityUnitGrant(BattleUnitId unitId, IEnumerable<AssetId> abilities, int equipmentPhysicalAttackBonus)
    {
        if (string.IsNullOrWhiteSpace(unitId.Value)) throw new ArgumentException("A unit ID is required.", nameof(unitId));
        if (equipmentPhysicalAttackBonus != 0) throw new NotSupportedException("This profile requires zero equipment PA bonus.");
        ArgumentNullException.ThrowIfNull(abilities);
        var selected = ImmutableHashSet.CreateBuilder<AssetId>();
        foreach (var id in abilities)
            if (string.IsNullOrWhiteSpace(id.Value) || !selected.Add(id))
                throw new ArgumentException("Ability grants require distinct explicit IDs.", nameof(abilities));
        UnitId = unitId;
        Abilities = selected.ToImmutable();
    }
}

/// <summary>Immutable opt-in binding for ordinary zero-bonus PA, immediate self actions and explicit grants.</summary>
public sealed class ImmediateAbilityBattleProfile
{
    public const string RulesId = "fft:rules/immediate-self-pa-v1";
    public ImmutableDictionary<AssetId, BoundedImmediateAbility> Abilities { get; }
    public ImmutableDictionary<BattleUnitId, ImmediateAbilityUnitGrant> Units { get; }

    public ImmediateAbilityBattleProfile(IEnumerable<BoundedImmediateAbility> abilities, IEnumerable<ImmediateAbilityUnitGrant> units)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentNullException.ThrowIfNull(units);
        var effects = ImmutableDictionary.CreateBuilder<AssetId, BoundedImmediateAbility>();
        foreach (var ability in abilities)
        {
            ArgumentNullException.ThrowIfNull(ability);
            if (!effects.TryAdd(ability.Id, ability)) throw new ArgumentException("Duplicate executable ability.", nameof(abilities));
        }
        var grants = ImmutableDictionary.CreateBuilder<BattleUnitId, ImmediateAbilityUnitGrant>();
        foreach (var unit in units)
        {
            ArgumentNullException.ThrowIfNull(unit);
            if (!grants.TryAdd(unit.UnitId, unit)) throw new ArgumentException("Duplicate unit grant binding.", nameof(units));
            if (unit.Abilities.Any(id => !effects.ContainsKey(id))) throw new ArgumentException("Unresolved executable ability grant.", nameof(units));
        }
        Abilities = effects.ToImmutable();
        Units = grants.ToImmutable();
    }

    internal void ValidateState(AttackBattleSnapshot state)
    {
        state.ValidateSupportedGeometry();
        if (state.Combatants.Length != Units.Count || state.Combatants.Any(unit =>
            !Units.ContainsKey(unit.UnitId) || unit.PhysicalAttack is < 1 or > 99))
            throw new NotSupportedException("Every unit requires a zero-bonus binding and current PA 1..99.");
    }

    internal void ValidateSetup(AttackBattleSnapshot state, IReadOnlyDictionary<AssetId, BoundedSwordWeapon> weapons)
    {
        ValidateState(state);
        foreach (var unit in state.Combatants)
        {
            var weapon = weapons[unit.RightHandWeaponId!.Value];
            if ((2 * 99 - 1) * weapon.Power > short.MaxValue)
                throw new NotSupportedException("Ability-enabled sword power exceeds the reachable PA-99 damage envelope.");
        }
    }
}

/// <summary>Source-derived PA arithmetic for Y=1 and zero equipment bonus; decomp 11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
public static class AccumulateFormula
{
    public static int IncreasePhysicalAttack(int currentPhysicalAttack)
    {
        if (currentPhysicalAttack is < 1 or > 99) throw new ArgumentOutOfRangeException(nameof(currentPhysicalAttack));
        return Math.Min(99, currentPhysicalAttack + 1);
    }
}

public sealed record UseImmediateAbilityCommand(BattleUnitId ActorId, AssetId AbilityId);
public sealed record ImmediateAbilityResolvedEvent(BattleUnitId ActorId, AssetId AbilityId, int PhysicalAttackBefore, int PhysicalAttackAfter) : AttackResultEvent;
public sealed record AttackPhysicalAttackChangedEvent(BattleUnitId UnitId, int Before, int After) : AttackResultEvent;

/// <summary>Pure self-action transition. The profile certifies the absence of unmodeled modifiers; no live RNG draws.</summary>
public static class ImmediateAbilityBattleSimulator
{
    public static AttackBattleResult Apply(AttackBattleSnapshot state, UseImmediateAbilityCommand command, ImmediateAbilityBattleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(profile);
        var actor = state.Combatants.FirstOrDefault(unit => unit.UnitId == command.ActorId);
        if (actor is null) return Reject(AttackCommandRejection.UnknownActor);
        if (state.TurnBattle.Turns.ActiveUnitId != actor.UnitId) return Reject(AttackCommandRejection.InactiveActor);
        if (state.TurnBattle.Turns.Units.Single(unit => unit.Id == actor.UnitId).ActionTaken)
            return Reject(AttackCommandRejection.ActionAlreadyTaken);
        if (!profile.Abilities.ContainsKey(command.AbilityId)) return Reject(AttackCommandRejection.UnknownAbility);
        if (!profile.Units.TryGetValue(actor.UnitId, out var grant) || !grant.Abilities.Contains(command.AbilityId))
            return Reject(AttackCommandRejection.AbilityNotGranted);
        try { profile.ValidateState(state); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        { return Reject(AttackCommandRejection.UnsupportedSetup); }
        if (state.Combatants.Any(unit => unit.Status == AttackUnitStatus.Dead)) return Reject(AttackCommandRejection.UnsupportedSetup);

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

        AttackBattleResult Reject(AttackCommandRejection reason) =>
            new(false, reason, TurnCycleRejection.None, BattleTransitionRejection.None, state, []);
    }
}
