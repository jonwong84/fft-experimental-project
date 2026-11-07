using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record FlatRosterUnit(BattleUnitId UnitId, int StableTeam, int SourceOrder);
