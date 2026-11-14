using System.Collections.Immutable;

namespace Zodiac.Framework.Campaign;

/// <summary>Invented idle-world proof budgets and definitions, not original campaign limits or rules.</summary>
public sealed class WorldCheckpointDefinition
{
    public const int MaximumParty = 64, MaximumStacks = 256, MaximumFunds = 1_000_000;
    public const long MaximumElapsedSeconds = 315_360_000;
    public const int TravelCost = 7, TravelSeconds = 11;
    public AssetId RouteId { get; }
    public AssetId ProgressFlagId { get; }
    public ImmutableArray<AssetId> PartyIds { get; }
    public ImmutableArray<WorldCheckpointJob> Jobs { get; }
    public ImmutableArray<WorldCheckpointItem> Items { get; }
    public ImmutableArray<AssetId> Locations { get; }

    public WorldCheckpointDefinition(AssetId routeId, AssetId progressFlagId, IEnumerable<AssetId> partyIds,
        IEnumerable<WorldCheckpointJob> jobs, IEnumerable<WorldCheckpointItem> items, IEnumerable<AssetId> locations)
    {
        CheckId(routeId); CheckId(progressFlagId);
        ArgumentNullException.ThrowIfNull(partyIds); ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(items); ArgumentNullException.ThrowIfNull(locations);
        PartyIds = partyIds.Take(MaximumParty + 1).ToImmutableArray();
        Jobs = jobs.Take(MaximumStacks + 1).ToImmutableArray(); Items = items.Take(MaximumStacks + 1).ToImmutableArray();
        Locations = locations.Take(MaximumStacks + 1).ToImmutableArray();
        Unique(PartyIds, MaximumParty); Unique(Jobs.Select(j => j?.Id ?? default), MaximumStacks);
        Unique(Items.Select(i => i?.Id ?? default), MaximumStacks); Unique(Locations, MaximumStacks);
        Unique(new[] { routeId, progressFlagId }.Concat(PartyIds).Concat(Jobs.Select(j => j.Id))
            .Concat(Items.Select(i => i.Id)).Concat(Locations), MaximumParty + 3 * MaximumStacks + 2);
        foreach (var item in Items)
            if (item.MaximumOwned is < 1 or > 9999) throw new ArgumentException("Item ownership limit must be 1..9999.");
        foreach (var job in Jobs)
        {
            if (job.AllowedEquipment.IsDefault || job.AllowedEquipment.Length > MaximumStacks || job.AllowedEquipment.Distinct().Count() != job.AllowedEquipment.Length)
                throw new ArgumentException("Equipment permissions must be explicit, bounded and unique.");
            foreach (var id in job.AllowedEquipment)
                if (!Items.Any(i => i.Id == id && i.Equippable)) throw new ArgumentException("Job permission references an unknown or unequippable item.");
        }
        (RouteId, ProgressFlagId) = (routeId, progressFlagId);
    }

    public WorldCheckpointState Restore(AssetId route, AssetId location, IEnumerable<WorldPartyMember> party,
        IEnumerable<WorldItemStack> inventory, int funds, WorldDate date, AssetId progressFlag, bool progress, long elapsedSeconds)
    {
        if (route != RouteId || progressFlag != ProgressFlagId || !Locations.Contains(location))
            throw new ArgumentException("Unsupported idle-world route, progress flag or location.");
        if (funds is < 0 or > MaximumFunds || date.Month is < 1 or > 12 || date.Day is < 1 or > 30 ||
            elapsedSeconds is < 0 or > MaximumElapsedSeconds) throw new ArgumentException("World scalar fields exceed the synthetic profile limits.");
        ArgumentNullException.ThrowIfNull(party); ArgumentNullException.ThrowIfNull(inventory);
        var members = party.Take(MaximumParty + 1).ToImmutableArray();
        var stacks = inventory.Take(MaximumStacks + 1).ToImmutableArray();
        if (members.Length != PartyIds.Length || stacks.Length > MaximumStacks) throw new ArgumentException("Unsupported party or inventory size.");
        var byId = new Dictionary<AssetId, WorldPartyMember>();
        var owned = new Dictionary<AssetId, int>();
        foreach (var stack in stacks)
        {
            var item = Items.FirstOrDefault(i => i.Id == stack.ItemId);
            if (item is null || stack.Quantity < 1 || stack.Quantity > item.MaximumOwned || !owned.TryAdd(stack.ItemId, stack.Quantity))
                throw new ArgumentException("Inventory requires unique known items with positive bounded quantities.");
        }
        foreach (var member in members)
        {
            if (member is null || !PartyIds.Contains(member.Id) || !byId.TryAdd(member.Id, member))
                throw new ArgumentException("Party requires exactly its distinct authored identities.");
            var job = Jobs.FirstOrDefault(j => j.Id == member.JobId) ?? throw new ArgumentException("Unknown party job.");
            if (member.EquippedItemId is { } equipped)
            {
                if (!job.AllowedEquipment.Contains(equipped)) throw new ArgumentException("Equipment is not permitted for this job.");
                owned[equipped] = owned.GetValueOrDefault(equipped) + 1;
                if (owned[equipped] > Items.Single(i => i.Id == equipped).MaximumOwned)
                    throw new ArgumentException("Carried plus equipped ownership exceeds the selected item limit.");
            }
        }
        return new(this, location, [.. PartyIds.Select(id => byId[id])], [.. stacks.OrderBy(i => i.ItemId.Value, StringComparer.Ordinal)],
            funds, date, progress, elapsedSeconds);
    }
    public static void CheckId(AssetId id)
    {
        if (id.Value is not { Length: > 0 and <= 256 }) throw new ArgumentException("A bounded stable ID is required.");
        _ = new AssetId(id.Value);
    }
    private static void Unique(IEnumerable<AssetId> values, int maximum)
    {
        var ids = values.ToArray();
        if (ids.Length == 0 || ids.Length > maximum || ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Definition IDs must be nonempty, unique and within the proof budget.");
        foreach (var id in ids) CheckId(id);
    }
}
