using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Selected ability metadata and primary slots, copied at the content boundary.</summary>
public sealed class DefaultAbilityLearningDefinition
{
    public ImmutableDictionary<AssetId, AbilityLearningRule> Abilities { get; }
    public ImmutableDictionary<AssetId, ImmutableArray<AbilityLearningSlot>> PrimarySlotsByJob { get; }

    public DefaultAbilityLearningDefinition(IEnumerable<AbilityLearningRule> abilities,
        IEnumerable<KeyValuePair<AssetId, ImmutableArray<AbilityLearningSlot>>> primarySlotsByJob)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentNullException.ThrowIfNull(primarySlotsByJob);
        Abilities = abilities.ToImmutableDictionary(a => a.AbilityId);
        PrimarySlotsByJob = primarySlotsByJob.ToImmutableDictionary();
        if (Abilities.Any(p => p.Value is null || string.IsNullOrWhiteSpace(p.Key.Value)) ||
            PrimarySlotsByJob.Any(p => string.IsNullOrWhiteSpace(p.Key.Value) || p.Value.IsDefault ||
                p.Value.Any(s => s is null || s.Index is < 0 or > 21 || !Abilities.ContainsKey(s.AbilityId)) ||
                p.Value.Select(s => s.Index).Distinct().Count() != p.Value.Length))
            throw new ArgumentException("Learning requires explicit selected abilities and unique original human slot indices.");
    }

    public ImmutableArray<AbilityLearningSlot> PrimarySlots(AssetId jobId) =>
        PrimarySlotsByJob.GetValueOrDefault(jobId, []);
}
