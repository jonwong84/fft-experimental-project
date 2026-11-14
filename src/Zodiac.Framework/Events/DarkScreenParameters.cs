using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed record DarkScreenParameters(int Reserved = 0, int Mode = 1, int Divisor = 12,
    int AngleStep = 64, int RadiusStep = 4, int Image = 0);
