using System.Collections.Immutable;

namespace Zodiac.Framework.Campaign;

public sealed record WorldCheckpointJob(AssetId Id, ImmutableArray<AssetId> AllowedEquipment);
