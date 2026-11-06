using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Calculated reward fields, not a complete career or permission to adopt them.</summary>
public sealed record ActionRewardProjection(ActionRewardAmounts Amounts, ActionExperienceBalance ActorExperience,
    byte LevelForDisplay, byte JobLevelForDisplay, ImmutableArray<ActionJobPointProjection> JobPoints)
{
    public bool RequiresUnlockRecalculation => !JobPoints.IsEmpty;
}
