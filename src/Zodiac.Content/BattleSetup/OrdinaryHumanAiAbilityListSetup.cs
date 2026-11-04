using System.Collections.Immutable;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Zodiac.Framework;
using Zodiac.Framework.Ai;

namespace Zodiac.Content.BattleSetup;

/// <summary>Explicit resolved known-slot fact; does not infer original career rows or grant learning from a job.</summary>
public sealed record KnownAiActionSlot(SkillsetId SkillsetId, int ActionSlot);
public sealed class OrdinaryHumanAiUnitSelection
{
    public BattleUnitId UnitId { get; }
    public SkillsetId? SecondarySkillsetId { get; }
    public ImmutableArray<KnownAiActionSlot> KnownSlots { get; }
    public OrdinaryHumanAiUnitSelection(BattleUnitId unitId, IEnumerable<KnownAiActionSlot> knownSlots, SkillsetId? secondarySkillsetId = null)
    {
        if (string.IsNullOrWhiteSpace(unitId.Value)) throw new ArgumentException("An explicit unit is required.", nameof(unitId));
        ArgumentNullException.ThrowIfNull(knownSlots);
        KnownSlots = knownSlots.ToImmutableArray();
        if (KnownSlots.Any(s => s is null || s.ActionSlot < 0 || string.IsNullOrWhiteSpace(s.SkillsetId.Value)) ||
            KnownSlots.Distinct().Count() != KnownSlots.Length || secondarySkillsetId is { } second && string.IsNullOrWhiteSpace(second.Value))
            throw new ArgumentException("Distinct explicit known slots and skillsets are required.", nameof(knownSlots));
        UnitId = unitId; SecondarySkillsetId = secondarySkillsetId;
    }
}

/// <summary>Pure outer adapter from existing metadata/effects and explicit known slots to semantic AI input.</summary>
/// <remarks>ZOD-261/263. No file I/O, raw learned rows, menu inference, flag-draft consolidation or command authority.</remarks>
public static class OrdinaryHumanAiAbilityListSetup
{
    // Observed original compatibility traversal, not a limit on SkillsetDefinition or managed catalogs.
    private const int OriginalHumanActionPositions = 16;

    public static AiHumanAbilityLists Create(ContentPackage metadata, ImmediateAbilityContentPackage immediate,
        TargetedAbilityContentPackage targeted, BaseJobBattleSetup setup, IEnumerable<OrdinaryHumanAiUnitSelection> selections,
        AiHumanAbilityListProfile profile)
    {
        ArgumentNullException.ThrowIfNull(metadata); ArgumentNullException.ThrowIfNull(immediate);
        ArgumentNullException.ThrowIfNull(targeted); ArgumentNullException.ThrowIfNull(setup); ArgumentNullException.ThrowIfNull(selections);
        immediate.ValidateAgainst(metadata); targeted.ValidateAgainst(metadata);
        if (immediate.Abilities.Length != 1 || targeted.Abilities.Length != 1 || immediate.Abilities[0].Id == targeted.Abilities[0].Id)
            throw new NotSupportedException("Select one self PA+1 and one distinct Throw Stone effect binding.");
        var self = immediate.Abilities[0].Id; var stone = targeted.Abilities[0].Id;
        var skills = metadata.Skillsets.ToDictionary(s => s.Id); var abilities = metadata.Abilities.ToDictionary(a => a.Id);
        var selected = new Dictionary<BattleUnitId, OrdinaryHumanAiUnitSelection>();
        foreach (var selection in selections)
        {
            ArgumentNullException.ThrowIfNull(selection);
            if (!selected.TryAdd(selection.UnitId, selection)) throw new ArgumentException("Duplicate unit selection.", nameof(selections));
        }
        if (selected.Count != setup.Definitions.Length || setup.Definitions.Any(b => !selected.ContainsKey(b.UnitId)))
            throw new ArgumentException("Select known-slot state for exactly the setup units.", nameof(selections));
        var sources = new List<AiHumanUnitAbilitySource>();
        foreach (var binding in setup.Definitions)
        {
            if (!ReferenceEquals(metadata.Jobs.SingleOrDefault(j => j.Id == binding.Job.Id), binding.Job) ||
                !skills.TryGetValue(binding.PrimarySkillset.Id, out var primary) || !ReferenceEquals(primary, binding.PrimarySkillset))
                throw new ArgumentException("Setup must resolve the selected metadata instances.", nameof(setup));
            var input = selected[binding.UnitId];
            SkillsetDefinition? secondary = null;
            if (input.SecondarySkillsetId is { } secondaryId && !skills.TryGetValue(secondaryId, out secondary))
                throw new ArgumentException("Secondary skillset is unresolved.", nameof(selections));
            var known = input.KnownSlots.Select(s => (s.SkillsetId, s.ActionSlot)).ToHashSet();
            foreach (var slot in input.KnownSlots)
            {
                var source = slot.SkillsetId == primary.Id ? primary : secondary?.Id == slot.SkillsetId ? secondary : null;
                if (source is null || slot.ActionSlot >= OriginalHumanActionPositions ||
                    !source.Slots.Any(s => s.Slot == slot.ActionSlot && s.AbilityId is not null))
                    throw new ArgumentException("Known action must resolve a nonempty selected original action position.", nameof(selections));
            }
            AiHumanSkillset Convert(SkillsetDefinition definition)
            {
                var slots = definition.Slots.Where(s => s.Slot < OriginalHumanActionPositions).Select(slot => {
                    bool learned = known.Contains((definition.Id, slot.Slot));
                    if (slot.AbilityId is not { } id) return new AiHumanActionSlot(slot.Slot, null, AiHumanAbilityKind.Unsupported, false, false);
                    var data = abilities[id]; bool usable = (data.AiFlagsRaw[3] & 0x80) != 0;
                    var kind = id == self ? AiHumanAbilityKind.Accumulate : id == stone ? AiHumanAbilityKind.ThrowStone : AiHumanAbilityKind.Unsupported;
                    if (learned && usable && kind != AiHumanAbilityKind.Unsupported)
                    {
                        byte[] expected = kind == AiHumanAbilityKind.Accumulate ? [9, 128, 0] : [130, 2, 65];
                        if (!data.AiFlagsRaw.Take(3).SequenceEqual(expected) || (data.AiFlagsRaw[3] & ~0x81) != 0)
                            throw new NotSupportedException("Executable effects require the traced ordinary AI metadata; effect identity alone is insufficient.");
                    }
                    return new AiHumanActionSlot(slot.Slot, new(id.Value), kind, learned, usable);
                });
                return new(new(definition.Id.Value), slots);
            }
            sources.Add(new(binding.UnitId, Convert(primary), secondary is null ? null : Convert(secondary)));
        }
        return AiHumanAbilityLists.Build(sources, profile);
    }
}
