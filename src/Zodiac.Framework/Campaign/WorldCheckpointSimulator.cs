using System.Collections.Immutable;

namespace Zodiac.Framework.Campaign;

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
