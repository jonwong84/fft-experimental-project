using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record FlatMovementOutcome(int EffectiveMove, int JumpHalf,
    ImmutableArray<TilePosition> PhysicalDestinations, ImmutableArray<FlatMovementResidual> RemainingRanges,
    ImmutableArray<TilePosition> AiOrigins, ImmutableArray<FlatMovementDistanceRow> TargetDistanceRows, FlatMovementPath? Path);
