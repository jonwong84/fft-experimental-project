using System.Collections.Immutable;

namespace Zodiac.Framework.Campaign;

public sealed record WorldPartyMember(AssetId Id, AssetId JobId, AssetId? EquippedItemId);
