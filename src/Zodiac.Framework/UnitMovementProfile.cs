using System.Collections.ObjectModel;

namespace Zodiac.Framework;

/// <summary>
/// Effective movement inputs for one battle unit, including its terrain costs for the
/// current environment.
/// </summary>
/// <remarks>
/// This is an immutable simulation input, not a job/content definition. It does not yet
/// implement the original game's ability/status resolution or weather-dependent cost selection.
/// </remarks>
public sealed class UnitMovementProfile : IEquatable<UnitMovementProfile>
{
    private const UnitMovementTraits KnownTraits = UnitMovementTraits.IgnoreHeight
        | UnitMovementTraits.AnyGround
        | UnitMovementTraits.AnyWeather
        | UnitMovementTraits.CannotEnterWater
        | UnitMovementTraits.TeleportPlusTwo;

    private readonly ReadOnlyDictionary<TerrainSurfaceId, TerrainMovementCost> _terrainCosts;

    public UnitMovementProfile(
        int move,
        int jump,
        int bodyHeight,
        UnitMovementMode mode,
        UnitMovementTraits traits,
        IEnumerable<KeyValuePair<TerrainSurfaceId, TerrainMovementCost>> terrainCosts)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(move);
        ArgumentOutOfRangeException.ThrowIfNegative(jump);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bodyHeight);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown movement mode.");
        }

        ArgumentNullException.ThrowIfNull(terrainCosts);
        if ((traits & ~KnownTraits) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(traits), traits, "Unknown movement trait.");
        }

        var costs = new Dictionary<TerrainSurfaceId, TerrainMovementCost>();
        foreach ((TerrainSurfaceId surface, TerrainMovementCost cost) in terrainCosts)
        {
            if (string.IsNullOrWhiteSpace(surface.Value))
            {
                throw new ArgumentException("Terrain costs require valid surface IDs.", nameof(terrainCosts));
            }

            if (cost.MovePoints < 0 || (!cost.IsBlocked && cost.MovePoints == 0))
            {
                throw new ArgumentException("Traversable terrain costs must be positive; blocked costs may be zero.", nameof(terrainCosts));
            }

            if (!costs.TryAdd(surface, cost))
            {
                throw new ArgumentException($"Duplicate terrain movement cost for '{surface}'.", nameof(terrainCosts));
            }
        }

        Move = move;
        Jump = jump;
        BodyHeight = bodyHeight;
        Mode = mode;
        Traits = traits;
        _terrainCosts = new ReadOnlyDictionary<TerrainSurfaceId, TerrainMovementCost>(costs);
    }

    public int Move { get; }

    public int Jump { get; }

    public int BodyHeight { get; }

    public UnitMovementMode Mode { get; }

    public UnitMovementTraits Traits { get; }

    public IReadOnlyDictionary<TerrainSurfaceId, TerrainMovementCost> TerrainCosts => _terrainCosts;

    public bool TryGetTerrainCost(TerrainSurfaceId surface, out TerrainMovementCost cost) =>
        _terrainCosts.TryGetValue(surface, out cost);

    public bool Equals(UnitMovementProfile? other) =>
        other is not null
        && Move == other.Move
        && Jump == other.Jump
        && BodyHeight == other.BodyHeight
        && Mode == other.Mode
        && Traits == other.Traits
        && _terrainCosts.Count == other._terrainCosts.Count
        && _terrainCosts.All(pair => other._terrainCosts.TryGetValue(pair.Key, out TerrainMovementCost cost)
            && pair.Value == cost);

    public override bool Equals(object? obj) => obj is UnitMovementProfile other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Move);
        hash.Add(Jump);
        hash.Add(BodyHeight);
        hash.Add(Mode);
        hash.Add(Traits);
        foreach (KeyValuePair<TerrainSurfaceId, TerrainMovementCost> pair in
                 _terrainCosts.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal))
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value);
        }

        return hash.ToHashCode();
    }

    public static UnitMovementProfile ForFlatGridPrototype(int move) =>
        new(move, jump: 0, bodyHeight: 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            Array.Empty<KeyValuePair<TerrainSurfaceId, TerrainMovementCost>>());
}
