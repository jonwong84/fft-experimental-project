using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record FlatMovementResidual(TilePosition Tile, byte Remaining);
