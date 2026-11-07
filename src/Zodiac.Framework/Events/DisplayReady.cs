namespace Zodiac.Framework.Events;

public sealed record DisplayReady(ReadyDisplayDefinition Definition, AssetId ArtworkId, AssetId OwnerId);
