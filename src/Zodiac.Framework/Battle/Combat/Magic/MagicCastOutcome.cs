using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record MagicCastOutcome(ushort CandidateMp, int RecordedCost, ImmutableArray<MagicCastTargetResult> Targets);
