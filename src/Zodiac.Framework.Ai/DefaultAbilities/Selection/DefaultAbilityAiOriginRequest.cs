namespace Zodiac.Framework.Ai;

public sealed record DefaultAbilityAiOriginRequest(BattleUnitId ActorId, int PrimarySlot,
    DefaultAbilityAiOriginScenario Scenario = DefaultAbilityAiOriginScenario.Main);
