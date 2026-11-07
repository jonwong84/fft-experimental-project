using System.Collections.Immutable;
using Xunit;

namespace Zodiac.Framework.Ai.Tests;

public sealed class OrdinarySpellAiListTests
{
    internal static MagicCastDefinition Spell(short original = 16, int cost = 6, byte charge = 4) => new(
        new($"test:spell/source-{original}"), original, new(FaithMagicAmountKind.Damage, 14, MagicElement.Fire, true),
        cost, charge, true, Targeting: new(4, 1, 1));
    internal static AiAbilityBehavior FireBehavior => new(TargetsEnemies: true, AffectsHp: true, AffectedBySilence: true,
        AffectedByFaith: true, FollowTarget: true, Reflectable: true, MagicalAttack: true);
    internal static OrdinarySpellAiSlot Slot(int index, short id = 16, int cost = 6, bool usable = true) => new(index, Spell(id, cost), FireBehavior, usable);
    internal static ImmutableDictionary<int, ImmutableHashSet<int>> Rows(params int[] known) =>
        ImmutableDictionary<int, ImmutableHashSet<int>>.Empty.Add(6, known.ToImmutableHashSet());

    [Theory]
    [InlineData(5, 5, 0)] [InlineData(11, 11, 6)] [InlineData(23, 23, 18)] [InlineData(74, 74, 0)]
    public void Learning_uses_source_skillset_row(byte skillset, byte primary, int expected) =>
        Assert.Equal(expected, OrdinarySpellAiLists.LearnedRow(skillset, primary, 5));

    [Fact]
    public void Unowned_special_skillsets_and_monsters_are_known_without_reading_a_row()
    {
        Assert.True(OrdinarySpellAiLists.IsKnown(74, 11, 5, 0, ImmutableDictionary<int, ImmutableHashSet<int>>.Empty));
        Assert.True(OrdinarySpellAiLists.IsKnown(11, 11, 5, 0, ImmutableDictionary<int, ImmutableHashSet<int>>.Empty, true));
        Assert.Throws<ArgumentException>(() => OrdinarySpellAiLists.IsKnown(11, 11, 5, 0, ImmutableDictionary<int, ImmutableHashSet<int>>.Empty));
    }

    [Fact]
    public void Source_order_and_duplicate_ability_slots_remain_distinct()
    {
        var primary = new OrdinarySpellAiSkillset(11, new("test:skillset/black"), [Slot(8), Slot(0), Slot(3)]);
        var list = OrdinarySpellAiLists.Build(primary, null, 5, Rows(0, 8), 75);
        Assert.Equal(new[] { 0, 8 }, list.Entries.Select(e => e.Slot.Index));
        Assert.All(list.Entries, e => Assert.Equal(primary.Id, e.SkillsetId));
        Assert.All(list.Entries, e => Assert.Equal((short)16, e.Slot.Spell.OriginalAbilityId));
    }

    [Fact]
    public void Initialization_checks_maximum_mp_and_static_ai_usable_not_current_mp()
    {
        var p = new OrdinarySpellAiSkillset(11, new("test:skillset/black"), [Slot(0, cost: 75), Slot(1, cost: 76), Slot(2, usable: false)]);
        var list = OrdinarySpellAiLists.Build(p, null, 5, Rows(0, 1, 2), 75);
        Assert.Equal(0, Assert.Single(list.Entries).Slot.Index);
        Assert.Equal(2, OrdinarySpellAiLists.Build(p, null, 5, Rows(0, 1, 2), 75, true).Entries.Length);
    }

    [Fact]
    public void Action_slot_and_id_filters_do_not_admit_reaction_or_support_entries()
    {
        var p = new OrdinarySpellAiSkillset(11, new("test:skillset/black"), [Slot(0, 422), Slot(16), Slot(1)]);
        Assert.Equal(1, Assert.Single(OrdinarySpellAiLists.Build(p, null, 5, Rows(0, 1, 16), 75).Entries).Slot.Index);
        p = p with { Slots = [Slot(0), Slot(0)] };
        Assert.Throws<ArgumentException>(() => OrdinarySpellAiLists.Build(p, null, 5, Rows(0), 75));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(6)] [InlineData(8)] [InlineData(17)] [InlineData(18)]
    [InlineData(19)] [InlineData(20)] [InlineData(21)] [InlineData(52)]
    [InlineData(56)] [InlineData(57)] [InlineData(59)] [InlineData(62)] [InlineData(156)]
    public void Special_menus_cannot_enter_the_default_spell_list_or_loader(byte skillset)
    {
        var rows = Enumerable.Range(0, 19).ToImmutableDictionary(i => i, _ => ImmutableHashSet.Create(0));
        var ordinary = new OrdinarySpellAiSkillset(11, new("test:skillset/black"), [Slot(0)]);
        var special = new OrdinarySpellAiSkillset(skillset, new("test:skillset/special"), [Slot(0)]);

        Assert.Throws<NotSupportedException>(() => OrdinarySpellAiLists.Build(special, null, 5, rows, 75));
        Assert.Throws<NotSupportedException>(() => OrdinarySpellAiLists.Build(ordinary, special, 5, rows, 75));
        Assert.Throws<NotSupportedException>(() => OrdinarySpellAiParameters.Load(
            OrdinarySpellAiParameterTests.Entry() with { OriginalSkillsetId = skillset }, new(), new()));
    }

    [Theory]
    [InlineData(5)] [InlineData(74)] [InlineData(175)]
    public void Default_menus_keep_primary_known_row_aliases_and_special_skillset_ids(byte skillset)
    {
        var primary = new OrdinarySpellAiSkillset(skillset, new("test:skillset/default"), [Slot(0)]);
        var rows = ImmutableDictionary<int, ImmutableHashSet<int>>.Empty.Add(0, ImmutableHashSet.Create(0));
        var entry = Assert.Single(OrdinarySpellAiLists.Build(primary, null, 5, rows, 75).Entries);

        Assert.Equal(skillset, entry.OriginalSkillsetId);
        Assert.Equal(6, OrdinarySpellAiParameters.Load(entry, new(), new()).MpCost);
    }
}
