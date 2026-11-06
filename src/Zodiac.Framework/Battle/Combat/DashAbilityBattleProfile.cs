using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Compatibility view of the admitted adjacent physical profile; definitions and grants have one owner.</summary>
public sealed class DashAbilityBattleProfile
{
    public PhysicalAbilityBattleProfile PhysicalProfile { get; }
    public TerrainSurfaceId Ground => PhysicalProfile.Ground;
    public ImmutableDictionary<AssetId, BoundedDashAbility> Abilities { get; }
    public ImmutableDictionary<BattleUnitId, TargetedAbilityUnitGrant> Units => PhysicalProfile.Units;

    public DashAbilityBattleProfile(TerrainSurfaceId ground, IEnumerable<BoundedDashAbility> abilities,
        IEnumerable<TargetedAbilityUnitGrant> units)
        : this(new PhysicalAbilityBattleProfile(ground, PhysicalAbilityDelivery.AdjacentUnit,
            (abilities ?? throw new ArgumentNullException(nameof(abilities))).Select(a =>
                (a ?? throw new ArgumentNullException(nameof(abilities))).Definition), units)) { }

    private DashAbilityBattleProfile(PhysicalAbilityBattleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Delivery != PhysicalAbilityDelivery.AdjacentUnit)
            throw new NotSupportedException("Select the adjacent physical delivery profile.");
        PhysicalProfile = profile;
        Abilities = profile.Abilities.ToImmutableDictionary(p => p.Key, p => BoundedDashAbility.FromDefinition(p.Value));
    }

    public static DashAbilityBattleProfile FromPhysicalProfile(PhysicalAbilityBattleProfile profile) => new(profile);

    public static DashAbilityBattleProfile FromDefinitions(TerrainSurfaceId ground, IEnumerable<PhysicalAbilityDefinition> abilities,
        IEnumerable<TargetedAbilityUnitGrant> units) =>
        new(new PhysicalAbilityBattleProfile(ground, PhysicalAbilityDelivery.AdjacentUnit, abilities, units));
}
