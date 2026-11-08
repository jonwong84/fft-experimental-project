namespace Zodiac.Framework.Ai;

/// <summary>A fresh owned primary slot; callers cannot supply trusted masks, deadlines or cached candidates.</summary>
public sealed record DefaultAbilityAiAimRequest(BattleUnitId ActorId, int PrimarySlot);
