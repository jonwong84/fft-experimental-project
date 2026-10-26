using System.Collections.Immutable;

namespace Zodiac.Framework.Campaign;

public sealed record WorldCheckpointJob(AssetId Id, ImmutableArray<AssetId> AllowedEquipment);
public sealed record WorldCheckpointItem(AssetId Id, bool Equippable, int MaximumOwned);
public sealed record WorldPartyMember(AssetId Id, AssetId JobId, AssetId? EquippedItemId);
public readonly record struct WorldItemStack(AssetId ItemId, int Quantity);
public readonly record struct WorldDate(int Month, int Day);

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

public sealed class WorldCheckpointState
{
    public WorldCheckpointDefinition Definition { get; }
    public AssetId LocationId { get; }
    public ImmutableArray<WorldPartyMember> Party { get; }
    public ImmutableArray<WorldItemStack> Inventory { get; }
    public int Funds { get; }
    public WorldDate Date { get; }
    public bool Progress { get; }
    public long ElapsedSeconds { get; }
    internal WorldCheckpointState(WorldCheckpointDefinition definition, AssetId location, ImmutableArray<WorldPartyMember> party,
        ImmutableArray<WorldItemStack> inventory, int funds, WorldDate date, bool progress, long elapsedSeconds)
        => (Definition, LocationId, Party, Inventory, Funds, Date, Progress, ElapsedSeconds) =
            (definition, location, party, inventory, funds, date, progress, elapsedSeconds);
}

public enum WorldTravelRejection { None, UnknownLocation, AlreadyThere, InsufficientFunds, ElapsedLimit }
public sealed record WorldTravelEvent(AssetId From, AssetId To, int FundsSpent, int ElapsedSeconds);
public sealed record WorldTravelResult(WorldCheckpointState State, WorldTravelRejection Rejection, ImmutableArray<WorldTravelEvent> Events)
{
    public bool Accepted => Rejection == WorldTravelRejection.None;
}
/// <summary>Invented continuation command for persistence proofs; no original travel, date advancement or encounters.</summary>
public static class WorldCheckpointSimulator
{
    public static WorldTravelResult Travel(WorldCheckpointState state, AssetId destination)
    {
        ArgumentNullException.ThrowIfNull(state);
        WorldTravelResult Reject(WorldTravelRejection rejection) => new(state, rejection, []);
        if (!state.Definition.Locations.Contains(destination)) return Reject(WorldTravelRejection.UnknownLocation);
        if (destination == state.LocationId) return Reject(WorldTravelRejection.AlreadyThere);
        if (state.Funds < WorldCheckpointDefinition.TravelCost) return Reject(WorldTravelRejection.InsufficientFunds);
        if (state.ElapsedSeconds > WorldCheckpointDefinition.MaximumElapsedSeconds - WorldCheckpointDefinition.TravelSeconds)
            return Reject(WorldTravelRejection.ElapsedLimit);
        var next = state.Definition.Restore(state.Definition.RouteId, destination, state.Party, state.Inventory,
            state.Funds - WorldCheckpointDefinition.TravelCost, state.Date, state.Definition.ProgressFlagId, state.Progress,
            state.ElapsedSeconds + WorldCheckpointDefinition.TravelSeconds);
        return new(next, WorldTravelRejection.None, [new(state.LocationId, destination, WorldCheckpointDefinition.TravelCost, WorldCheckpointDefinition.TravelSeconds)]);
    }
}
