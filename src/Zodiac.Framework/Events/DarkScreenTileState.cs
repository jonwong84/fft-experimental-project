using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public readonly record struct DarkScreenTileState(int RemainingDelay, int Angle, int Radius);
