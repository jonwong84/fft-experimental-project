using Xunit;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AiHumanAbilityListTests
{
    private static readonly BattleUnitId Unit = new("sample:unit/a");
    private static readonly AssetId Self = new("sample:ability/accumulate"), Stone = new("sample:ability/stone");
    private static readonly AiHumanAbilityListProfile Profile = new(true, true);
    private static AiHumanSkillset Skill(params AiHumanActionSlot[] slots) => new(new("sample:skillset/basic"), slots);
    private static AiHumanActionSlot Slot(int slot, bool self = false, bool known = true, bool usable = true) =>
        new(slot, self ? Self : Stone, self ? AiHumanAbilityKind.Accumulate : AiHumanAbilityKind.ThrowStone, known, usable);

    [Fact]
    public void Primary_secondary_slot_order_holes_and_duplicate_abilities_are_preserved_with_Frog_in_denominator()
    {
        var primary = Skill(Slot(2), new(1, null, AiHumanAbilityKind.Unsupported, false, false), Slot(0, self: true));
        var secondary = new AiHumanSkillset(new("sample:skillset/secondary"), [Slot(7, self: true)]);
        var list = AiHumanAbilityLists.Build([new(Unit, primary, secondary)], Profile).Units[Unit];
        Assert.Equal(new[] { AiHumanAbilityKind.Attack, AiHumanAbilityKind.Accumulate, AiHumanAbilityKind.ThrowStone,
            AiHumanAbilityKind.Accumulate, AiHumanAbilityKind.FrogAttack }, list.Entries.Select(e => e.Kind));
        Assert.Equal(new int?[] { null, 0, 2, 7, null }, list.Entries.Select(e => e.Slot));
        Assert.Equal(AiHumanEntrySource.Secondary, list.Entries[3].Source); Assert.Equal(secondary.Id, list.Entries[3].SkillsetId);
        Assert.Equal(5, list.InitializedEntryCount); Assert.Equal(1, list.EvadeQuarterShare);
        Assert.Equal(2, list.HelpfulCount); Assert.Equal(2, list.HarmfulCount); Assert.True(list.HasRangedAbility);
        Assert.False(list.Entries[^1].OrdinaryUsable);
        // Frog's primary AI evasion flag is clear, but its secondary evadeable bit still selects exhaustive origins at init.
        Assert.False(list.Entries[^1].Evadeable); Assert.True(list.Entries[^1].ExhaustiveOriginScan);
        Assert.Equal(0, list.MpQuarterShare); Assert.Equal(0, list.SilenceQuarterShare);
    }

    [Fact]
    public void The_same_skillset_in_both_positions_is_not_deduplicated()
    {
        var skill = Skill(Slot(0, self: true), Slot(2)); var list = AiHumanAbilityLists.Build([new(Unit, skill, skill)], Profile).Units[Unit];
        Assert.Equal(6, list.InitializedEntryCount); Assert.Equal(2, list.EvadeQuarterShare);
        Assert.Equal(2, list.HelpfulCount); Assert.Equal(3, list.HarmfulCount);
        Assert.Equal(2, list.Entries.Count(e => e.Kind == AiHumanAbilityKind.Accumulate));
    }

    [Fact]
    public void Known_AI_disabled_and_unsupported_actions_are_distinct_and_no_eligible_action_is_silently_dropped()
    {
        var skill = Skill(Slot(0, self: true, known: false), Slot(2, usable: false),
            new(3, new("sample:ability/other"), AiHumanAbilityKind.Unsupported, false, true));
        var list = AiHumanAbilityLists.Build([new(Unit, skill)], Profile).Units[Unit];
        Assert.Equal(2, list.InitializedEntryCount); Assert.Equal(2, list.EvadeQuarterShare); Assert.False(list.HasRangedAbility);
        Assert.Equal(1, list.HarmfulCount); Assert.Equal(0, list.HelpfulCount);
        Assert.Throws<NotSupportedException>(() => AiHumanAbilityLists.Build([new(Unit,
            Skill(new AiHumanActionSlot(3, new("sample:ability/other"), AiHumanAbilityKind.Unsupported, true, true)))], Profile));
    }

    [Fact]
    public void Caller_storage_and_separate_build_branches_cannot_mutate_owned_lists()
    {
        var slots = new[] { Slot(0, self: true), Slot(2) }; var source = Skill(slots);
        var units = new[] { new AiHumanUnitAbilitySource(Unit, source) }; var first = AiHumanAbilityLists.Build(units, Profile);
        slots[0] = Slot(0, known: false); units[0] = new(Unit, Skill());
        Assert.Equal(4, first.Units[Unit].InitializedEntryCount);
        Assert.Equal(2, AiHumanAbilityLists.Build(units, Profile).Units[Unit].InitializedEntryCount);
        Assert.Equal(AiHumanAbilityKind.Accumulate, first.Units[Unit].Entries[1].Kind);
    }

    [Fact]
    public void Missing_context_and_malformed_slot_or_unit_bindings_are_rejected()
    {
        var source = new AiHumanUnitAbilitySource(Unit, Skill());
        Assert.Throws<NotSupportedException>(() => AiHumanAbilityLists.Build([source], new(true, false)));
        Assert.Throws<NotSupportedException>(() => AiHumanAbilityLists.Build([source], new(false, true)));
        Assert.Throws<ArgumentException>(() => Skill(Slot(0), Slot(0)));
        Assert.Throws<ArgumentException>(() => Skill(new AiHumanActionSlot(0, null, AiHumanAbilityKind.Accumulate, true, true)));
        Assert.Throws<ArgumentException>(() => AiHumanAbilityLists.Build([source, source], Profile));
        Assert.Throws<ArgumentException>(() => AiHumanAbilityLists.Build([], Profile));
    }
}
