using System.Collections.Immutable;

namespace Fft.Content.Definitions;

/// <summary>Immutable job metadata, without unit progression, equipment or effective battle stats.</summary>
public sealed class JobDefinition
{
    public JobId Id { get; }
    public SkillsetId PrimarySkillsetId { get; }
    public int BaseMove { get; }
    public int BaseJump { get; }
    public bool SteppingStone { get; }
    public ImmutableArray<AbilityId> InnateAbilityIds { get; }

    public JobDefinition(JobId id, SkillsetId primarySkillsetId, int baseMove, int baseJump,
        bool steppingStone, IEnumerable<AbilityId> innateAbilityIds)
    {
        DefinitionValidation.RequiredId(id.Value, nameof(id));
        DefinitionValidation.RequiredId(primarySkillsetId.Value, nameof(primarySkillsetId));
        DefinitionValidation.Range(baseMove, byte.MaxValue, nameof(baseMove));
        DefinitionValidation.Range(baseJump, 0x7f, nameof(baseJump));
        ArgumentNullException.ThrowIfNull(innateAbilityIds);
        ImmutableArray<AbilityId> innates = innateAbilityIds.ToImmutableArray();
        foreach (AbilityId innate in innates)
        {
            DefinitionValidation.RequiredId(innate.Value, nameof(innateAbilityIds));
        }

        Id = id;
        PrimarySkillsetId = primarySkillsetId;
        BaseMove = baseMove;
        BaseJump = baseJump;
        SteppingStone = steppingStone;
        InnateAbilityIds = innates;
    }
}
