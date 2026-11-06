using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Compatibility view of the admitted direct-projectile physical profile.</summary>
/// <remarks>Pair admission remains distinct from roster trajectory admission.</remarks>
public sealed class TargetedAbilityBattleProfile
{
    public const string RulesId = "fft:rules/throw-stone-flat-v1";
    public PhysicalAbilityBattleProfile PhysicalProfile { get; }
    public TerrainSurfaceId OrdinaryGroundSurface => PhysicalProfile.Ground;
    public ImmutableDictionary<AssetId, BoundedThrowStoneAbility> Abilities { get; }
    public ImmutableDictionary<BattleUnitId, TargetedAbilityUnitGrant> Units => PhysicalProfile.Units;

    public TargetedAbilityBattleProfile(TerrainSurfaceId ordinaryGroundSurface,
        IEnumerable<BoundedThrowStoneAbility> abilities, IEnumerable<TargetedAbilityUnitGrant> units)
        : this(new PhysicalAbilityBattleProfile(ordinaryGroundSurface, PhysicalAbilityDelivery.DirectProjectile,
            (abilities ?? throw new ArgumentNullException(nameof(abilities))).Select(a =>
                (a ?? throw new ArgumentNullException(nameof(abilities))).Definition), units)) { }

    private TargetedAbilityBattleProfile(PhysicalAbilityBattleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Delivery != PhysicalAbilityDelivery.DirectProjectile)
            throw new NotSupportedException("Select the direct-projectile physical delivery profile.");
        PhysicalProfile = profile;
        Abilities = profile.Abilities.ToImmutableDictionary(p => p.Key, p => BoundedThrowStoneAbility.FromDefinition(p.Value));
    }

    public static TargetedAbilityBattleProfile FromPhysicalProfile(PhysicalAbilityBattleProfile profile) => new(profile);

    public static TargetedAbilityBattleProfile FromDefinitions(TerrainSurfaceId ground, IEnumerable<PhysicalAbilityDefinition> abilities,
        IEnumerable<TargetedAbilityUnitGrant> units) =>
        new(new PhysicalAbilityBattleProfile(ground, PhysicalAbilityDelivery.DirectProjectile, abilities, units));

    internal void ValidateState(AttackBattleSnapshot state)
    {
        state.ValidateSupportedGeometry();
        if (state.Combatants.Length != 2 || Units.Count != 2 || state.Combatants.Any(unit =>
                !Units.ContainsKey(unit.UnitId) || unit.PhysicalAttack is < 1 or > 99))
            throw new NotSupportedException("Exactly two ordinary zero-bonus units with current PA 1..99 are required.");
        var map = state.TurnBattle.Battle.Map;
        if (map.LayerCount != 1) throw new NotSupportedException("Only an open lower-layer rectangle is supported.");
        for (int y = 0; y < map.Depth; y++)
        for (int x = 0; x < map.Width; x++)
        {
            map.TryGetTile(new(x, y, 0), out var tile);
            if (tile.Surface != OrdinaryGroundSurface || tile.MovementFlags != BattleTileMovementFlags.None ||
                tile.Height != 10 || tile.HalfHeight != 0 || tile.WaterDepth != 0 ||
                tile.CeilingDepth != 0 || tile.Slope.ToPackedByte() != 0)
                throw new NotSupportedException("Throw Stone requires clear, dry, flat ordinary ground throughout the map.");
        }
    }
}
