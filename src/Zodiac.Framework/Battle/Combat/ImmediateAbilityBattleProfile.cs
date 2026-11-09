using System.Collections.Immutable;
using Zodiac.Framework.Formulas;

namespace Zodiac.Framework;

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
            if (!FormulaDamageBounds.FitsSigned16(0, (2L * 99 - 1) * weapon.Power))
                throw new NotSupportedException("Ability-enabled sword power exceeds the reachable PA-99 damage envelope.");
        }
    }
}
