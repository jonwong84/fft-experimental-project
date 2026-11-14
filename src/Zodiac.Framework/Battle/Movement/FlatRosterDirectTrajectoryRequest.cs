using System.Collections.Immutable;
using System.Numerics;

namespace Zodiac.Framework;

public sealed record FlatRosterDirectTrajectoryRequest(BattleUnitId ActorId, BattleUnitId TargetId, TilePosition Origin);
