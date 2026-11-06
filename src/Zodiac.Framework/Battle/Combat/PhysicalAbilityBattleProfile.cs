using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>One immutable physical definition/grant owner for the selected adjacent or projectile delivery.</summary>
public sealed class PhysicalAbilityBattleProfile
{
    public TerrainSurfaceId Ground { get; }
    public PhysicalAbilityDelivery Delivery { get; }
    public ImmutableDictionary<AssetId, PhysicalAbilityDefinition> Abilities { get; }
    public ImmutableDictionary<BattleUnitId, TargetedAbilityUnitGrant> Units { get; }

    public PhysicalAbilityBattleProfile(TerrainSurfaceId ground, PhysicalAbilityDelivery delivery,
        IEnumerable<PhysicalAbilityDefinition> abilities, IEnumerable<TargetedAbilityUnitGrant> units)
    {
        if (string.IsNullOrWhiteSpace(ground.Value)) throw new ArgumentException("Explicit ordinary ground is required.", nameof(ground));
        if (!Enum.IsDefined(delivery)) throw new NotSupportedException("Select an admitted physical delivery profile.");
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentNullException.ThrowIfNull(units);
        var effects = ImmutableDictionary.CreateBuilder<AssetId, PhysicalAbilityDefinition>();
        foreach (var ability in abilities)
        {
            ArgumentNullException.ThrowIfNull(ability);
            if (!ability.SupportsDelivery(delivery)) throw new NotSupportedException("The definition does not match the selected physical delivery.");
            if (!effects.TryAdd(ability.Id, ability)) throw new ArgumentException("Duplicate executable ability.", nameof(abilities));
        }
        var grants = ImmutableDictionary.CreateBuilder<BattleUnitId, TargetedAbilityUnitGrant>();
        foreach (var unit in units)
        {
            ArgumentNullException.ThrowIfNull(unit);
            if (!grants.TryAdd(unit.UnitId, unit)) throw new ArgumentException("Duplicate unit grant binding.", nameof(units));
            if (unit.Abilities.Any(id => !effects.ContainsKey(id))) throw new ArgumentException("Unresolved executable ability grant.", nameof(units));
        }
        Ground = ground;
        Delivery = delivery;
        Abilities = effects.ToImmutable();
        Units = grants.ToImmutable();
    }
}
