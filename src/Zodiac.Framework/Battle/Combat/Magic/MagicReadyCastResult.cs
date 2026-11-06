using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record MagicReadyCastResult(bool Accepted, MagicMpOutcome? Resource, PreparedMagicCast? Cast);
