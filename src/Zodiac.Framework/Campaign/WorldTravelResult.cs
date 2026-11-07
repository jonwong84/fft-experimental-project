using System.Collections.Immutable;

namespace Zodiac.Framework.Campaign;

public sealed record WorldTravelResult(WorldCheckpointState State, WorldTravelRejection Rejection, ImmutableArray<WorldTravelEvent> Events)
{
    public bool Accepted => Rejection == WorldTravelRejection.None;
}
