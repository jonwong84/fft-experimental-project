using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>A trusted composition-root assertion. It carries logical identities, never artwork or I/O.</summary>
public sealed record PreloadedBannerBinding(AssetId DefinitionId, AssetId ArtworkId, AssetId OwnerId);
