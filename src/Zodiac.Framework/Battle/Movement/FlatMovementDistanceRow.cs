using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Every x in this lower-layer row; omitted rows are unqueried, not zero filled.</summary>
public sealed record FlatMovementDistanceRow(int Y, ImmutableArray<byte> Distances);
