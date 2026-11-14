using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>A confirmed voluntary walk; final facing is carried by Path, even when unchanged.</summary>
public sealed record VoluntaryPathCompletedEvent(BattleUnitId ActorId, FlatMovementPath Path,
    BattleFacing PreviousFacing) : AttackResultEvent;
