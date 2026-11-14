namespace Zodiac.Framework;

/// <summary>Captured reward facts; JP fields refer to the requested actor job slot, not each recipient's current job.</summary>
public sealed record ActionRewardUnit(BattleUnitId UnitId, int SourceOrder, SpellTargetTeam InitialTeam,
    SpellTargetTeam CurrentTeam, int JobSlot, byte Level, byte Experience, byte JobLevel,
    ushort CurrentJobPoints, ushort TotalJobPoints, bool Active = true, bool Crystal = false,
    bool Dead = false, bool Petrify = false, bool Treasure = false, bool IsMonster = false,
    bool IsMale = false, bool IsFemale = false, bool GainedJpUp = false);
