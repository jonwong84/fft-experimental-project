using System.Collections.Immutable;
using Zodiac.Content.Definitions.Job;

namespace Zodiac.Content.Definitions;

/// <summary>Immutable job metadata, without unit progression, equipment or effective battle stats.</summary>
public sealed class JobDefinition
{
    public JobId Id { get; }
    public SkillsetId PrimarySkillsetId { get; }
    public JobStats JobStats { get; }
    public bool SteppingStone { get; }
    public ImmutableArray<AbilityId> InnateAbilityIds { get; }

    public JobDefinition(
        JobId id,
        SkillsetId primarySkillsetId,
        JobStats jobStats,
        bool steppingStone,
        IEnumerable<AbilityId> innateAbilityIds)
    {
        DefinitionValidation.RequiredId(id.Value, nameof(id));
        DefinitionValidation.RequiredId(primarySkillsetId.Value, nameof(primarySkillsetId));
        ArgumentNullException.ThrowIfNull(jobStats);
        ArgumentNullException.ThrowIfNull(innateAbilityIds);
        ImmutableArray<AbilityId> innates = innateAbilityIds.ToImmutableArray();
        foreach (AbilityId innate in innates)
        {
            DefinitionValidation.RequiredId(innate.Value, nameof(innateAbilityIds));
        }

        Id = id;
        PrimarySkillsetId = primarySkillsetId;
        JobStats = jobStats;
        SteppingStone = steppingStone;
        InnateAbilityIds = innates;
    }
}
