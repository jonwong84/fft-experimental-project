namespace Zodiac.Framework.Ai;

/// <summary>One owned slot, explicit profile declaration and fresh movement scenario; no cached work or command authority.</summary>
public sealed record DefaultAbilityAiAbilitySearchRequest(BattleUnitId ActorId, int PrimarySlot,
    DefaultAbilityAiMovementProfile Profile,
    DefaultAbilityAiOriginScenario Scenario = DefaultAbilityAiOriginScenario.Main);
