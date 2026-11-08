using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>One isolated move-only hypothesis and its actual decision RNG interval.</summary>
public sealed record OrdinarySpellAiScoredMovement(TilePosition Destination, RandomState EntryRng,
    RandomState RngState, ImmutableArray<OrdinarySpellAiUnit> Units, OrdinarySpellAiClockOutcome Clock,
    OrdinarySpellAiScore Score)
{
    public short TilePriority => unchecked((short)-Score.Priority);
}
