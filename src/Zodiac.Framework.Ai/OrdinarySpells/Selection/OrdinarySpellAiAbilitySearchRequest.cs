namespace Zodiac.Framework.Ai;

/// <summary>One owned slot and fresh movement scenario; no cached work or command authority.</summary>
public sealed record OrdinarySpellAiAbilitySearchRequest(BattleUnitId ActorId, int PrimarySlot,
    OrdinarySpellAiOriginScenario Scenario = OrdinarySpellAiOriginScenario.Main);
