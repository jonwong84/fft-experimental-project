using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public readonly record struct DarkScreenTileDraw(int TileIndex, int Angle, int Radius);
