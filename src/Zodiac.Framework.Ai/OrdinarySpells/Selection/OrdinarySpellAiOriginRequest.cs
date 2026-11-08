namespace Zodiac.Framework.Ai;

public sealed record OrdinarySpellAiOriginRequest(BattleUnitId ActorId, int PrimarySlot,
    OrdinarySpellAiOriginScenario Scenario = OrdinarySpellAiOriginScenario.Main);
