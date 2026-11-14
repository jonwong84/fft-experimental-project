using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>One isolated move-only hypothesis and its actual decision RNG interval.</summary>
public sealed record DefaultAbilityAiScoredMovement(TilePosition Destination, RandomState EntryRng,
    RandomState RngState, ImmutableArray<DefaultAbilityAiUnit> Units, DefaultAbilityAiClockOutcome Clock,
    DefaultAbilityAiScore Score)
{
    public short TilePriority => unchecked((short)-Score.Priority);
}
