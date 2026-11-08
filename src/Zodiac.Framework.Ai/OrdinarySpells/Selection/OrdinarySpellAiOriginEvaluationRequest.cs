namespace Zodiac.Framework.Ai;

/// <summary>Select one potential origin/pass by current facts; no cached origin or candidate is accepted.</summary>
public sealed record OrdinarySpellAiOriginEvaluationRequest(BattleUnitId ActorId, int PrimarySlot,
    OrdinarySpellAiAimPassKind Pass, TilePosition CastOrigin, OrdinarySpellAiOriginScenario Scenario = OrdinarySpellAiOriginScenario.Main);
