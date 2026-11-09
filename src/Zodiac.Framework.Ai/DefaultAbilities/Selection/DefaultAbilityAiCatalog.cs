using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Definition-bound selected primary spell facts. No secondary loadout is inferred from global grants.</summary>
public sealed class DefaultAbilityAiCatalog
{
    public DefaultAbilityBattleDefinition Definition { get; }
    public ImmutableDictionary<AssetId, DefaultAbilityAiSkillset> PrimaryByJob { get; }

    public DefaultAbilityAiCatalog(DefaultAbilityBattleDefinition definition,
        IEnumerable<KeyValuePair<AssetId, DefaultAbilityAiSkillset>> primaryByJob)
    {
        ArgumentNullException.ThrowIfNull(definition); ArgumentNullException.ThrowIfNull(primaryByJob);
        if (definition.Learning is null) throw new NotSupportedException("Explicit owned learned rows and metadata are required.");
        PrimaryByJob = primaryByJob.ToImmutableDictionary();
        if (!PrimaryByJob.Keys.ToHashSet().SetEquals(definition.Learning.PrimarySlotsByJob.Keys))
            throw new ArgumentException("Every selected job primary must be explicitly bound.", nameof(primaryByJob));
        foreach (var pair in PrimaryByJob)
        {
            var primary = pair.Value;
            if (primary is null || primary.Slots.IsDefault || primary.OriginalId is < 5 or > 23 ||
                primary.Slots.Any(s => s is null || s.Spell is null || s.Behavior is null || s.Index is < 0 or > 21 ||
                    !definition.Spells.TryGetValue(s.Spell.Id, out var owned) || !ReferenceEquals(owned, s.Spell)) ||
                primary.Slots.Select(s => s.Index).Distinct().Count() != primary.Slots.Length ||
                !primary.Slots.OrderBy(s => s.Index).Select(s => (s.Index, s.Spell.Id)).SequenceEqual(
                    definition.Learning.PrimarySlots(pair.Key).OrderBy(s => s.Index).Select(s => (s.Index, s.AbilityId))))
                throw new ArgumentException("Primary metadata must match the owned selected spell objects and learned slots.", nameof(primaryByJob));
            DefaultAbilityAiParameters.RequireDefaultMenu(primary.OriginalId);
        }
        foreach (var unit in definition.Units)
        {
            var primary = PrimaryByJob[unit.RewardJobId];
            if (unit.OriginalJobId is < 74 or > 92 || primary.OriginalId != unit.OriginalJobId - 69 ||
                unit.LearnedSpells.Any(id => !primary.Slots.Any(s => s.Spell.Id == id)))
                throw new NotSupportedException("This AI slice requires generic primary-only grants; secondary/special learned rows need an explicit loadout model.");
        }
        Definition = definition;
    }

    internal DefaultAbilityAiList List(DefaultAbilityBattleState state, DefaultAbilityUnitState unit)
    {
        var rows = Definition.Jobs.Jobs.Select((job, index) => KeyValuePair.Create(index, unit.LearnedAbilitySlots[job.JobId])).ToImmutableDictionary();
        return DefaultAbilityAiLists.Build(PrimaryByJob[unit.Definition.RewardJobId], null, 5, rows, unit.Vitals.MaxMp);
    }
}
