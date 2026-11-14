using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Raw growth only. An accepted result still requires derived/status refresh and caller-owned level bookkeeping.</summary>
public sealed record BattleRawGrowthResult(bool Accepted, BattleRawStats? Stats, RandomState RngState);
