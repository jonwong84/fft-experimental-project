namespace Zodiac.Framework.Ai;

/// <summary>Select one potential origin/pass by current facts; no cached origin or candidate is accepted.</summary>
public sealed record DefaultAbilityAiOriginEvaluationRequest(BattleUnitId ActorId, int PrimarySlot,
    DefaultAbilityAiAimPassKind Pass, TilePosition CastOrigin, DefaultAbilityAiOriginScenario Scenario = DefaultAbilityAiOriginScenario.Main);
