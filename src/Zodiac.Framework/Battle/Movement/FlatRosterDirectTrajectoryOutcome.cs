using System.Collections.Immutable;
using System.Numerics;

namespace Zodiac.Framework;

public sealed record FlatRosterDirectTrajectoryOutcome(bool Clear, BattleUnitId? BlockingUnitId, int? BlockingSample,
    int TotalSamples, int DistanceQ12, FlatProjectilePoint? BlockingPoint)
{
    /// <summary>Live to-target tracing returns the intended target when clear, or the first hit unit when blocked.</summary>
    public BattleUnitId? ResolveTarget(BattleUnitId intendedTarget) => Clear ? intendedTarget : BlockingUnitId;
}
