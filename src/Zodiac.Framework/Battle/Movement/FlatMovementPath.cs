using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Physical path including origin and endpoint. Heading is data, not a facing mutation.</summary>
public sealed record FlatMovementPath(ImmutableArray<TilePosition> Tiles, ImmutableArray<byte> PackedSteps, BattleFacing FinalHeading);
