using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Closed ordinary flat roster Dash with explicit learned grants and zero equipment PA bonus.</summary>
public sealed class DashAbilityBattleProfile
{
    public TerrainSurfaceId Ground { get; }
    public ImmutableDictionary<AssetId, BoundedDashAbility> Abilities { get; }
    public ImmutableDictionary<BattleUnitId, TargetedAbilityUnitGrant> Units { get; }
    public DashAbilityBattleProfile(TerrainSurfaceId ground, IEnumerable<BoundedDashAbility> abilities, IEnumerable<TargetedAbilityUnitGrant> units)
    {
        if (string.IsNullOrWhiteSpace(ground.Value)) throw new ArgumentException("Explicit ordinary ground is required.", nameof(ground));
        ArgumentNullException.ThrowIfNull(abilities); ArgumentNullException.ThrowIfNull(units);
        var effects = abilities.ToImmutableDictionary(a => a.Id); var grants = units.ToImmutableDictionary(u => u.UnitId);
        if (grants.Values.Any(u => u.Abilities.Any(a => !effects.ContainsKey(a)))) throw new ArgumentException("Unresolved Dash grant.", nameof(units));
        Ground = ground; Abilities = effects; Units = grants;
    }
}
