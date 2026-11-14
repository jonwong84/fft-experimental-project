namespace Zodiac.Framework;

public sealed record ActionJobPointProjection(BattleUnitId UnitId, int JobSlot, int Award, JobPointBalance Balance);
