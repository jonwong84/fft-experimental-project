using System.Collections.Immutable;

namespace Zodiac.Content.Definitions;

/// <summary>An explicit ordered slot. Null denotes an empty slot, not an unresolved reference.</summary>
public sealed class SkillsetSlot
{
    public int Slot { get; }
    public AbilityId? AbilityId { get; }

    public SkillsetSlot(int slot, AbilityId? abilityId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        if (abilityId is { } reference)
        {
            DefinitionValidation.RequiredId(reference.Value, nameof(abilityId));
        }

        Slot = slot;
        AbilityId = abilityId;
    }
}

/// <summary>Immutable skillset slots in caller-specified order, including empty and repeated references.</summary>
public sealed class SkillsetDefinition
{
    public SkillsetId Id { get; }
    public ImmutableArray<SkillsetSlot> Slots { get; }

    public SkillsetDefinition(SkillsetId id, IEnumerable<SkillsetSlot> slots)
    {
        DefinitionValidation.RequiredId(id.Value, nameof(id));
        ArgumentNullException.ThrowIfNull(slots);
        var snapshot = ImmutableArray.CreateBuilder<SkillsetSlot>();
        var indices = new HashSet<int>();
        foreach (SkillsetSlot slot in slots)
        {
            ArgumentNullException.ThrowIfNull(slot);
            if (!indices.Add(slot.Slot))
            {
                throw new ArgumentException($"Skillset '{id}' contains duplicate slot {slot.Slot}.", nameof(slots));
            }

            snapshot.Add(slot);
        }

        Id = id;
        Slots = snapshot.ToImmutable();
    }
}
