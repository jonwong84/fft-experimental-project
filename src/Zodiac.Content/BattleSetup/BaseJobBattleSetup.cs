using System.Collections.Immutable;
using Zodiac.Content.Definitions;
using Zodiac.Framework;

namespace Zodiac.Content.BattleSetup;

/// <summary>Explicit scenario inputs not derived from the imported job's base movement bytes.</summary>
public sealed class BaseJobUnitPlacement
{
    public BattleUnitId UnitId { get; }
    public JobId JobId { get; }
    public TilePosition Position { get; }
    public int BodyHeight { get; }
    public ImmutableArray<KeyValuePair<TerrainSurfaceId, TerrainMovementCost>> TerrainCosts { get; }

    public BaseJobUnitPlacement(BattleUnitId unitId, JobId jobId, TilePosition position, int bodyHeight,
        IEnumerable<KeyValuePair<TerrainSurfaceId, TerrainMovementCost>> terrainCosts)
    {
        ArgumentNullException.ThrowIfNull(terrainCosts);
        if (string.IsNullOrWhiteSpace(unitId.Value) || string.IsNullOrWhiteSpace(jobId.Value))
            throw new ArgumentException("Unit and job IDs must be explicit valid IDs.");
        UnitId = unitId;
        JobId = jobId;
        Position = position;
        BodyHeight = bodyHeight;
        TerrainCosts = terrainCosts.ToImmutableArray();
    }
}

/// <summary>Immutable definition references; evolving unit state is held separately in BattleState.</summary>
public sealed record BattleUnitDefinitionBinding(BattleUnitId UnitId, JobDefinition Job,
    SkillsetDefinition PrimarySkillset, ImmutableArray<AbilityDefinition> Abilities);

/// <summary>Validated initial battle and resolved definition bindings for the base-job-only prototype.</summary>
public sealed class BaseJobBattleSetup
{
    private BaseJobBattleSetup(BattleState initialState, ImmutableArray<BattleUnitDefinitionBinding> definitions)
    {
        InitialState = initialState;
        Definitions = definitions;
    }

    public BattleState InitialState { get; }
    public ImmutableArray<BattleUnitDefinitionBinding> Definitions { get; }

    /// <summary>
    /// Copies base Move/Jump with no equipment, movement abilities or status adjustments.
    /// Body height and terrain costs are supplied scenario inputs, not computed retail stats.
    /// </summary>
    public static BaseJobBattleSetup Create(DefinitionCatalog catalog, BattleMapDefinition map,
        IEnumerable<BaseJobUnitPlacement> placements, FftRandomState? rngState = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(placements);
        BaseJobUnitPlacement[] inputs = placements.ToArray();
        if (inputs.Length == 0) throw new ArgumentException("At least one unit placement is required.", nameof(placements));
        var units = new List<BattleUnitState>();
        var bindings = ImmutableArray.CreateBuilder<BattleUnitDefinitionBinding>();
        var ids = new HashSet<BattleUnitId>();
        var positions = new HashSet<TilePosition>();
        foreach (BaseJobUnitPlacement placement in inputs)
        {
            ArgumentNullException.ThrowIfNull(placement);
            if (!ids.Add(placement.UnitId))
                throw new ArgumentException($"Duplicate unit ID '{placement.UnitId}'.", nameof(placements));
            if (!positions.Add(placement.Position))
                throw new ArgumentException($"Unit '{placement.UnitId}' shares a starting tile.", nameof(placements));
            if (!map.TryGetTile(placement.Position, out BattleTile origin) || origin.IsBlocked || origin.CannotStop || origin.IsUntargetable)
                throw new ArgumentException($"Unit '{placement.UnitId}' requires an unblocked, stoppable starting tile.", nameof(placements));
            JobDefinition job;
            try { job = catalog.GetJob(placement.JobId); }
            catch (KeyNotFoundException exception)
            {
                throw new ArgumentException($"Unit '{placement.UnitId}' references missing job '{placement.JobId}'.", nameof(placements), exception);
            }
            if (job.SteppingStone)
                throw new NotSupportedException($"Job '{job.Id}' requires stepping-stone movement, which this setup does not implement.");
            if (placement.BodyHeight != 6)
                throw new NotSupportedException($"Unit '{placement.UnitId}' requires explicit ordinary body height 6 for this prototype.");
            var profile = new UnitMovementProfile(job.BaseMove, job.BaseJump, placement.BodyHeight,
                UnitMovementMode.Normal, UnitMovementTraits.None, placement.TerrainCosts);
            for (int layer = 0; layer < map.LayerCount; layer++)
            for (int y = 0; y < map.Depth; y++)
            for (int x = 0; x < map.Width; x++)
            {
                map.TryGetTile(new(x, y, layer), out BattleTile tile);
                if (!tile.IsBlocked && !profile.TryGetTerrainCost(tile.Surface, out _))
                    throw new ArgumentException($"Unit '{placement.UnitId}' has no explicit cost for terrain '{tile.Surface}'.", nameof(placements));
            }
            if (!profile.TryGetTerrainCost(origin.Surface, out TerrainMovementCost originCost) || originCost.IsBlocked)
                throw new ArgumentException($"Unit '{placement.UnitId}' cannot occupy its configured starting terrain.", nameof(placements));

            SkillsetDefinition skillset = catalog.GetSkillset(job.PrimarySkillsetId);
            ImmutableArray<AbilityDefinition> abilities = skillset.Slots.Where(slot => slot.AbilityId is not null)
                .Select(slot => slot.AbilityId!.Value).Concat(job.InnateAbilityIds).Distinct()
                .Select(catalog.GetAbility).ToImmutableArray();
            bindings.Add(new(placement.UnitId, job, skillset, abilities));
            units.Add(new BattleUnitState(placement.UnitId, placement.Position, profile));
        }
        return new(new BattleState(map, units, rngState), bindings.ToImmutable());
    }
}
