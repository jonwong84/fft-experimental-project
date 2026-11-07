using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public sealed record OrdinarySpellAiSlot(int Index, MagicCastDefinition Spell, AiAbilityBehavior Behavior, bool UsableByAi);
public sealed record OrdinarySpellAiSkillset(byte OriginalId, AssetId Id, ImmutableArray<OrdinarySpellAiSlot> Slots);
public sealed record OrdinarySpellAiListEntry(byte OriginalSkillsetId, AssetId SkillsetId, OrdinarySpellAiSlot Slot);

/// <summary>Owned selected default-menu spell entries; structural Attack/Frog statistics are separate.</summary>
public sealed class OrdinarySpellAiList
{
    public ImmutableArray<OrdinarySpellAiListEntry> Entries { get; }
    internal OrdinarySpellAiList(ImmutableArray<OrdinarySpellAiListEntry> entries) => Entries = entries;
}

/// <summary>Source ability_usable_from_skillset_add / ai_ability_is_known at revision11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
public static class OrdinarySpellAiLists
{
    public static int? LearnedRow(byte skillset, byte primary, byte baseSkillset, bool monster = false) =>
        monster ? null : skillset is >= 5 and < 24 ? skillset - 5 : skillset == primary || skillset == baseSkillset ? 0 : null;

    public static bool IsKnown(byte skillset, byte primary, byte baseSkillset, int index,
        ImmutableDictionary<int, ImmutableHashSet<int>> learnedRows, bool monster = false)
    {
        ArgumentNullException.ThrowIfNull(learnedRows);
        if (index is < 0 or > 23) throw new ArgumentOutOfRangeException(nameof(index));
        if (LearnedRow(skillset, primary, baseSkillset, monster) is not { } row) return true;
        if (!learnedRows.TryGetValue(row, out var known) || known is null || known.Any(i => i is < 0 or > 23))
            throw new ArgumentException("An explicit original learned row is required.", nameof(learnedRows));
        return known.Contains(index);
    }

    public static OrdinarySpellAiList Build(OrdinarySpellAiSkillset primary, OrdinarySpellAiSkillset? secondary,
        byte baseSkillset, ImmutableDictionary<int, ImmutableHashSet<int>> learnedRows, ushort maxMp, bool halfMp = false)
    {
        ArgumentNullException.ThrowIfNull(primary); ArgumentNullException.ThrowIfNull(learnedRows);
        var entries = ImmutableArray.CreateBuilder<OrdinarySpellAiListEntry>();
        void Append(OrdinarySpellAiSkillset skillset)
        {
            if (skillset.OriginalId == 0) return;
            OrdinarySpellAiParameters.RequireDefaultMenu(skillset.OriginalId);
            if (string.IsNullOrWhiteSpace(skillset.Id.Value) || skillset.Slots.IsDefault ||
                skillset.Slots.Any(s => s is null || s.Spell is null || s.Behavior is null || s.Index is < 0 or > 21 || s.Spell.MpCost < 0) ||
                skillset.Slots.Select(s => s.Index).Distinct().Count() != skillset.Slots.Length)
                throw new ArgumentException("Explicit distinct source skillset slots are required.", nameof(skillset));
            foreach (var slot in skillset.Slots.OrderBy(s => s.Index))
            {
                // Source action filtering is by ID range, never serialized primary class bits.
                if (slot.Index >= 16 || slot.Spell.OriginalAbilityId is <= 0 or >= 422 || !slot.UsableByAi) continue;
                if (slot.Spell.OriginalAbilityId >= 368)
                    throw new NotSupportedException("Only ordinary default-menu spell records are admitted.");
                int cost = halfMp ? slot.Spell.MpCost >> 1 : slot.Spell.MpCost;
                if (maxMp < cost || !IsKnown(skillset.OriginalId, primary.OriginalId, baseSkillset, slot.Index, learnedRows)) continue;
                entries.Add(new(skillset.OriginalId, skillset.Id, slot));
            }
        }
        Append(primary);
        if (secondary is not null) Append(secondary);
        return new(entries.ToImmutable());
    }
}
