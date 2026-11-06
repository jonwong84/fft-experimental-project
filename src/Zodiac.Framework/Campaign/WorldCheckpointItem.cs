using System.Collections.Immutable;

namespace Zodiac.Framework.Campaign;

public sealed record WorldCheckpointItem(AssetId Id, bool Equippable, int MaximumOwned);
