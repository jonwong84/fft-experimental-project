using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Typed admission, independent of raw opcodes and prepared-content formats.</summary>
public sealed record DisplayGeneralBanner(GeneralBannerDefinition Definition, AssetId ArtworkId, AssetId OwnerId);
