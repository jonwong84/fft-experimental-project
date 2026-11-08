namespace Zodiac.Framework.Ai;

/// <summary>One current owned slot, explicit caller pass and movement scenario; no cached hypothesis input.</summary>
public sealed record OrdinarySpellAiPassSearchRequest(BattleUnitId ActorId, int PrimarySlot,
    OrdinarySpellAiAimPassKind Pass, OrdinarySpellAiOriginScenario Scenario = OrdinarySpellAiOriginScenario.Main);
