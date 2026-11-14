using System.Collections.Immutable;

namespace Zodiac.Framework.Campaign;

public sealed record WorldTravelEvent(AssetId From, AssetId To, int FundsSpent, int ElapsedSeconds);
