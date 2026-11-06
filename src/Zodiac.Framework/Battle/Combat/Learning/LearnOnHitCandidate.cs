using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>A captured current-job row, ordered by the original unit slot.</summary>
public sealed record LearnOnHitCandidate(BattleUnitId UnitId, int SourceOrder,
    AbilityOutcomeMarker Marker, ImmutableArray<AbilityLearningSlot> PrimarySlots,
    ImmutableHashSet<int> LearnedSlots, bool IsMonster = false, bool PreventReaction = false);
