using Zodiac.Framework.Random;
using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Immutable opt-in for two ordinary units on an explicitly selected open, dry ground surface.</summary>
/// <remarks>The caller certifies the selected surface as ordinary ground; raw original terrain IDs are not inferred.</remarks>
public sealed class TargetedAbilityBattleProfile
{
    public const string RulesId = "fft:rules/throw-stone-flat-v1";
    public TerrainSurfaceId OrdinaryGroundSurface { get; }
    public ImmutableDictionary<AssetId, BoundedThrowStoneAbility> Abilities { get; }
    public ImmutableDictionary<BattleUnitId, TargetedAbilityUnitGrant> Units { get; }

    public TargetedAbilityBattleProfile(TerrainSurfaceId ordinaryGroundSurface,
        IEnumerable<BoundedThrowStoneAbility> abilities, IEnumerable<TargetedAbilityUnitGrant> units)
    {
        if (string.IsNullOrWhiteSpace(ordinaryGroundSurface.Value))
            throw new ArgumentException("Select an ordinary ground surface explicitly.", nameof(ordinaryGroundSurface));
        ArgumentNullException.ThrowIfNull(abilities); ArgumentNullException.ThrowIfNull(units);
        var effects = ImmutableDictionary.CreateBuilder<AssetId, BoundedThrowStoneAbility>();
        foreach (var ability in abilities)
        {
            ArgumentNullException.ThrowIfNull(ability);
            if (!effects.TryAdd(ability.Id, ability)) throw new ArgumentException("Duplicate executable ability.", nameof(abilities));
        }
        var grants = ImmutableDictionary.CreateBuilder<BattleUnitId, TargetedAbilityUnitGrant>();
        foreach (var unit in units)
        {
            ArgumentNullException.ThrowIfNull(unit);
            if (!grants.TryAdd(unit.UnitId, unit)) throw new ArgumentException("Duplicate unit grant binding.", nameof(units));
            if (unit.Abilities.Any(id => !effects.ContainsKey(id))) throw new ArgumentException("Unresolved executable ability grant.", nameof(units));
        }
        OrdinaryGroundSurface = ordinaryGroundSurface;
        Abilities = effects.ToImmutable(); Units = grants.ToImmutable();
    }

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
