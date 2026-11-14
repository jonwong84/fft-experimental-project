using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record LearnOnHitResult(ImmutableArray<LearnOnHitCandidate> Units,
    BattleUnitId? LearnerId, RandomState RngState);
