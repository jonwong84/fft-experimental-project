using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record TwoUnitBattleCompletedEvent(AssetId ScenarioId, BattleUnitId WinnerId, BattleUnitId LoserId) : TwoUnitBattleEvent;
