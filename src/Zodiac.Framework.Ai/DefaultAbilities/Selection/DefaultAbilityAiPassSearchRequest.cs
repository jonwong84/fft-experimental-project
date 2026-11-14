namespace Zodiac.Framework.Ai;

/// <summary>One current owned slot, explicit caller pass and movement scenario; no cached hypothesis input.</summary>
public sealed record DefaultAbilityAiPassSearchRequest(BattleUnitId ActorId, int PrimarySlot,
    DefaultAbilityAiAimPassKind Pass, DefaultAbilityAiOriginScenario Scenario = DefaultAbilityAiOriginScenario.Main);
