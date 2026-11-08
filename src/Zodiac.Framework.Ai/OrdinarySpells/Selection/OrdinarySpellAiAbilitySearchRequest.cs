namespace Zodiac.Framework.Ai;

/// <summary>One owned slot, explicit profile declaration and fresh movement scenario; no cached work or command authority.</summary>
public sealed record OrdinarySpellAiAbilitySearchRequest(BattleUnitId ActorId, int PrimarySlot,
    OrdinarySpellAiMovementProfile Profile,
    OrdinarySpellAiOriginScenario Scenario = OrdinarySpellAiOriginScenario.Main);
