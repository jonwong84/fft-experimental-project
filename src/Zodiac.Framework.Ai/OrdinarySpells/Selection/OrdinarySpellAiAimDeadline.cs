namespace Zodiac.Framework.Ai;

/// <summary>Source turn-delay prediction, independent of movement range or the actual prepared charge counter.</summary>
public sealed record OrdinarySpellAiAimDeadline(BattleUnitId UnitId, int TurnDelay, bool CanMoveByChargeDeadline);
