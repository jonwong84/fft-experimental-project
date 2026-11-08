namespace Zodiac.Framework.Ai;

/// <summary>One owned primary slot at the live actor's current origin; no cached candidate input.</summary>
public sealed record DefaultAbilityAiSearchRequest(BattleUnitId ActorId, int PrimarySlot);
