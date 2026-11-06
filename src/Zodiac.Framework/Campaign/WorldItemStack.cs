using System.Collections.Immutable;

namespace Zodiac.Framework.Campaign;

public readonly record struct WorldItemStack(AssetId ItemId, int Quantity);
