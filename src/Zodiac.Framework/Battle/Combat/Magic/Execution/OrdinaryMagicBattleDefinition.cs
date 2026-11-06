using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Explicit no-equipment/RSM ordinary spell encounter. First KO is an invented demo objective.</summary>
public sealed class OrdinaryMagicBattleDefinition
{
    public BattleMapDefinition Map { get; }
    public ImmutableArray<OrdinaryMagicUnitDefinition> Units { get; }
    public ImmutableDictionary<AssetId, MagicCastDefinition> Spells { get; }
    public JobUnlockCatalog Jobs { get; }
    public ImmutableArray<ushort> JobThresholds { get; }
    public RandomState InitialRng { get; }

    public static MagicEvadeEquipmentInput BareEquipment => new(false,
        new(255, 0, false), new(255, 0, false), new(255, 0, false));

    public OrdinaryMagicBattleDefinition(BattleMapDefinition map, IEnumerable<OrdinaryMagicUnitDefinition> units,
        IEnumerable<MagicCastDefinition> spells, JobUnlockCatalog jobs, ImmutableArray<ushort> jobThresholds,
        RandomState initialRng)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(jobs);
        if (jobThresholds.IsDefaultOrEmpty || jobThresholds.Length > 15)
            throw new ArgumentException("Explicit original-compatible job thresholds are required.", nameof(jobThresholds));
        if (map.LayerCount != 1 || map.Width > 255 || map.Depth > 255 || (long)map.Width * map.Depth > 256)
            throw new NotSupportedException("The ordinary spell encounter requires a source-sized lower-layer map.");
        map.TryGetTile(new(0, 0, 0), out var first);
        for (int y = 0; y < map.Depth; y++)
        for (int x = 0; x < map.Width; x++)
        {
            map.TryGetTile(new(x, y, 0), out var tile);
            if (tile.IsBlocked || tile.IsUntargetable || tile.CannotStop || tile.Height != first.Height ||
                tile.HalfHeight != 0 || tile.WaterDepth != 0 || tile.Slope.ToPackedByte() != 0 || tile.CeilingDepth != 0)
                throw new NotSupportedException("This encounter admits dry, flat, targetable terrain without special movement.");
        }
        var selected = ImmutableDictionary.CreateBuilder<AssetId, MagicCastDefinition>();
        foreach (var spell in spells)
        {
            ArgumentNullException.ThrowIfNull(spell);
            if (string.IsNullOrWhiteSpace(spell.Id.Value) || !selected.TryAdd(spell.Id, spell) ||
                spell.OriginalAbilityId is < 1 or >= 368 || spell.OriginalAbilityId == 329 || spell.Persevere ||
                spell.Targeting is null || spell.Targeting.Auto || spell.MpCost < 0 || spell.Spell is null ||
                !Enum.IsDefined(spell.Spell.Kind))
                throw new NotSupportedException("Select explicit single-strike ordinary empty-status spells.");
        }
        if (selected.Count == 0)
            throw new ArgumentException("At least one spell must be selected.", nameof(spells));
        var inputUnits = units.ToImmutableArray();
        if (inputUnits.Any(u => u is null || u.Occupant is null || u.Growth is null))
            throw new ArgumentException("Explicit unit, occupant and growth captures are required.", nameof(units));
        var captured = inputUnits.OrderBy(u => u.Occupant.SourceOrder).ToImmutableArray();
        if (captured.Length < 2)
            throw new ArgumentException("A spell encounter needs at least two explicit units.", nameof(units));
        var ids = new HashSet<BattleUnitId>();
        var orders = new HashSet<int>();
        var positions = new HashSet<TilePosition>();
        foreach (var u in captured)
        {
            ArgumentNullException.ThrowIfNull(u);
            OrdinaryMagicStatusRules.Validate(u.Current, u.Vitals);
            var o = u.Occupant;
            if (o.UnitId != u.Current.UnitId || !ids.Add(o.UnitId) || !orders.Add(o.SourceOrder) || o.SourceOrder < 0 ||
                !o.Active || o.Crystal || o.Treasure || o.Jump || o.Mount || o.InitialTeam != o.CurrentTeam ||
                !Enum.IsDefined(o.InitialTeam) || !positions.Add(o.Position) || !map.TryGetTile(o.Position, out _) ||
                u.Current.Statuses != new MagicCurrentStatuses() || u.Current.Supports != new MagicCurrentSupports() ||
                u.Current.Equipment != BareEquipment || u.Current.Identity.IsMonster || u.Current.HasRider || u.Current.IsMime ||
                !u.Current.ActionUnrestricted || u.Current.Strengthens != MagicElement.None || u.Current.Affinities != default ||
                u.Current.Faith > 100 || u.Level is < 1 or > 99 || u.Experience > 99 ||
                u.BaseAttributes.Speed == 0 || u.BaseAttributes.MagicAttack == 0 ||
                u.Current.MagicAttack != Math.Min(99, (int)u.BaseAttributes.MagicAttack))
                throw new NotSupportedException("Unit setup exceeds the ordinary human, bare-equipment, status-free encounter profile.");
            int slot = JobPointArithmetic.OriginalRewardSlot(u.OriginalJobId);
            if (slot >= jobs.Jobs.Length || jobs.Jobs[slot].JobId != u.RewardJobId ||
                u.JobPoints is null || u.JobPoints.Count != jobs.Jobs.Length || u.LearnedSpells is null ||
                u.LearnedSpells.Any(id => !selected.ContainsKey(id)))
                throw new ArgumentException("Explicit known job captures and learned spell grants are required.", nameof(units));
            foreach (var job in jobs.Jobs)
            {
                if (!u.JobPoints.TryGetValue(job.JobId, out var balance) || balance is null ||
                    balance.Current > balance.Total || balance.Total > 9999 || balance.JobLevel is < 0 or > 15)
                    throw new ArgumentException("Every career slot requires coherent current/total JP and a nibble level.", nameof(units));
            }
            var derived = Mode1BattleAttributes.CalculateWithoutEquipment(u.RawStats, u.Multipliers,
                u.Vitals.Hp, u.Current.Mp, u.BaseAttributes);
            if (derived.MaxHp != u.Vitals.MaxHp || derived.MaxMp != u.Vitals.MaxMp || derived.Hp != u.Vitals.Hp || derived.Mp != u.Current.Mp)
                throw new ArgumentException("Raw, multiplier and vital captures must agree.", nameof(units));
        }
        Map = map;
        Units = captured;
        Spells = selected.ToImmutable();
        Jobs = jobs;
        JobThresholds = jobThresholds;
        InitialRng = initialRng;
    }

    public OrdinaryMagicBattleState Start()
    {
        var units = Units.Select(u => new OrdinaryMagicUnitState(u, u.Occupant.Position, u.Current, u.Vitals,
            u.RawStats, u.Level, u.Experience, u.JobPoints,
            Jobs.Calculate(u.JobPoints.ToImmutableDictionary(p => p.Key, p => (byte)p.Value.JobLevel),
                u.Current.Identity.IsMale, u.Current.Identity.IsFemale))).ToImmutableArray();
        var turns = new TurnCycleState(units.Select(u => new TurnUnitState(u.Id,
            Math.Min(50, (int)u.Definition.BaseAttributes.Speed), 0, u.Occupant.SourceOrder)), InitialRng);
        return new(this, units, turns, ImmutableDictionary<BattleUnitId, PendingMagicCast>.Empty,
            OrdinaryMagicClockPhase.AddCt, 0, false, false);
    }
}
