using System.Collections.Immutable;

namespace Zodiac.Content.Definitions;

/// <summary>A complete, validated graph of the supplied metadata definitions, independent of original table capacity.</summary>
public sealed class DefinitionCatalog
{
    private readonly ImmutableDictionary<JobId, JobDefinition> jobsById;
    private readonly ImmutableDictionary<SkillsetId, SkillsetDefinition> skillsetsById;
    private readonly ImmutableDictionary<AbilityId, AbilityDefinition> abilitiesById;

    /// <summary>Jobs in supplied order; ordering does not establish identity.</summary>
    public ImmutableArray<JobDefinition> Jobs { get; }
    public ImmutableArray<SkillsetDefinition> Skillsets { get; }
    public ImmutableArray<AbilityDefinition> Abilities { get; }

    public DefinitionCatalog(IEnumerable<JobDefinition> jobs, IEnumerable<SkillsetDefinition> skillsets,
        IEnumerable<AbilityDefinition> abilities)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(skillsets);
        ArgumentNullException.ThrowIfNull(abilities);
        Jobs = jobs.ToImmutableArray();
        Skillsets = skillsets.ToImmutableArray();
        Abilities = abilities.ToImmutableArray();
        jobsById = Index(Jobs, job => job.Id, "job");
        skillsetsById = Index(Skillsets, skillset => skillset.Id, "skillset");
        abilitiesById = Index(Abilities, ability => ability.Id, "ability");

        foreach (JobDefinition job in Jobs)
        {
            if (!skillsetsById.ContainsKey(job.PrimarySkillsetId))
            {
                throw new ArgumentException($"Job '{job.Id}' references unresolved primary skillset '{job.PrimarySkillsetId}'.", nameof(jobs));
            }

            for (int index = 0; index < job.InnateAbilityIds.Length; index++)
            {
                if (!abilitiesById.ContainsKey(job.InnateAbilityIds[index]))
                {
                    throw new ArgumentException($"Job '{job.Id}' innate ability {index} references unresolved ability '{job.InnateAbilityIds[index]}'.", nameof(jobs));
                }
            }
        }

        foreach (SkillsetDefinition skillset in Skillsets)
        {
            foreach (SkillsetSlot slot in skillset.Slots)
            {
                if (slot.AbilityId is { } id && !abilitiesById.ContainsKey(id))
                {
                    throw new ArgumentException($"Skillset '{skillset.Id}' slot {slot.Slot} references unresolved ability '{id}'.", nameof(skillsets));
                }
            }
        }
    }

    public JobDefinition GetJob(JobId id) => jobsById[id];
    public SkillsetDefinition GetSkillset(SkillsetId id) => skillsetsById[id];
    public AbilityDefinition GetAbility(AbilityId id) => abilitiesById[id];

    private static ImmutableDictionary<TKey, TValue> Index<TKey, TValue>(ImmutableArray<TValue> entries,
        Func<TValue, TKey> getId, string kind) where TKey : notnull where TValue : class
    {
        var index = ImmutableDictionary.CreateBuilder<TKey, TValue>();
        foreach (TValue entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            TKey id = getId(entry);
            if (!index.TryAdd(id, entry))
            {
                throw new ArgumentException($"Catalog contains duplicate {kind} ID '{id}'.", nameof(entries));
            }
        }

        return index.ToImmutable();
    }
}
