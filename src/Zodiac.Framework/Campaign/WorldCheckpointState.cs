using System.Collections.Immutable;

namespace Zodiac.Framework.Campaign;

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
