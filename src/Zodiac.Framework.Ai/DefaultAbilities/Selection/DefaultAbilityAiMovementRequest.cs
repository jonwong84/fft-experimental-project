namespace Zodiac.Framework.Ai;

/// <summary>Fresh owned spell slot; no caller-supplied ranks, focus or retained workspace.</summary>
public sealed record DefaultAbilityAiMovementRequest(BattleUnitId ActorId, int PrimarySlot);
