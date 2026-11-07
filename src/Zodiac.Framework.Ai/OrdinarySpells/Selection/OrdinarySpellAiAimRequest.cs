namespace Zodiac.Framework.Ai;

/// <summary>A fresh owned primary slot; callers cannot supply trusted masks, deadlines or cached candidates.</summary>
public sealed record OrdinarySpellAiAimRequest(BattleUnitId ActorId, int PrimarySlot);
