using System.Collections.Immutable;
using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class EquipmentAttributeCalculationTests
{
    private static BattleRawStats Raw => new(1638400, 1638400, 1638400, 1638400, 1638400);
    private static BattleStatMultipliers Multipliers => new(100, 75, 100, 90, 80);
    [Fact]
    public void Fourteen_literals_preserve_item_type_presence_caps_monsters_and_final_clamp_order()
    {
        using var f = DerivedAttributeTestData.Fixture();
        foreach (var row in f.RootElement.GetProperty("equipment").EnumerateArray())
        {
            var d = row.GetProperty("input"); var e = row.GetProperty("expected"); var list = d.GetProperty("items");
            var items = list.ValueKind == System.Text.Json.JsonValueKind.Null ? default : list.EnumerateArray().Select(i =>
                new EquipmentAttributeContribution(new(i[1].GetByte(), i[2].GetByte(), i[3].GetByte()), i[4].GetByte(), i[5].GetByte(),
                    i[6].GetBoolean(), i[7].GetByte(), i[8].GetByte(), i[0].GetBoolean())).ToImmutableArray();
            var r = EquipmentAttributeCalculation.CalculateMode1(GrowthUnlockTestData.Raw(d.GetProperty("raw")), DerivedAttributeTestData.Multipliers(d),
                d.GetProperty("hp").GetUInt16(), d.GetProperty("mp").GetUInt16(), DerivedAttributeTestData.Triple(d.GetProperty("base")),
                d.GetProperty("move").GetByte(), d.GetProperty("jump").GetByte(), items, d.GetProperty("monster").GetBoolean(), d.GetProperty("egg").GetBoolean());
            Assert.Equal(DerivedAttributeTestData.Expected(e), r.Attributes);
            Assert.Equal(DerivedAttributeTestData.Triple(e.GetProperty("bonuses")), r.Bonuses);
            Assert.Equal(DerivedAttributeTestData.Triple(e.GetProperty("actual")), r.ActualAttributes);
            Assert.Equal(e.GetProperty("move").GetByte(), r.Move); Assert.Equal(e.GetProperty("jump").GetByte(), r.Jump);
        }
    }

    [Fact]
    public void Armor_is_applied_before_current_hp_mp_clamps_instead_of_composing_a_clamped_bare_result()
    {
        var bare = Mode1BattleAttributes.CalculateWithoutEquipment(Raw, Multipliers, 350, 140, new(7, 8, 9));
        var r = EquipmentAttributeCalculation.CalculateMode1(Raw, Multipliers, 350, 140, new(7, 8, 9), 3, 3,
            [new(default, AddsArmorVitals: true, HpBonus: 100, MpBonus: 40), new(default, AddsArmorVitals: true, HpBonus: 200, MpBonus: 30)]);
        Assert.Equal((ushort)100, bare.Hp); Assert.Equal((ushort)75, bare.Mp);
        Assert.Equal(new(350, 400, 140, 145, new(7, 8, 9)), r.Attributes);
    }

    [Fact]
    public void Monster_skips_unknown_contributions_but_keeps_base_and_caps_actual_values()
    {
        var r = EquipmentAttributeCalculation.CalculateMode1(Raw, Multipliers, 200, 100, new(255, 255, 255), 255, 255, default, monster: true);
        Assert.Equal(new BattleAttributeValues(255, 255, 255), r.Attributes.BaseAttributes);
        Assert.Equal(new BattleAttributeValues(99, 99, 50), r.ActualAttributes); Assert.Equal(default, r.Bonuses);
        Assert.Equal((byte)255, r.Move); Assert.Equal((byte)255, r.Jump);
        Assert.Equal((ushort)100, r.Attributes.Hp); Assert.Equal((ushort)75, r.Attributes.Mp);
    }

    [Fact]
    public void Item_presence_and_armor_classification_are_independent_from_numeric_bonus_values()
    {
        var item = new EquipmentAttributeContribution(default, HpBonus: 255, MpBonus: 255);
        EquipmentAttributeProjection Run(EquipmentAttributeContribution contribution) =>
            EquipmentAttributeCalculation.CalculateMode1(Raw, Multipliers, 350, 140, default, 255, 255, [contribution]);
        var absent = Run(item with { ItemPresent = false }); var present = Run(item); var armor = Run(item with { AddsArmorVitals = true });
        Assert.Equal((byte)255, absent.Move); Assert.Equal((byte)253, present.Move); Assert.Equal((byte)7, present.Jump);
        Assert.Equal((ushort)100, present.Attributes.MaxHp); Assert.Equal((ushort)355, armor.Attributes.MaxHp);
        Assert.Equal((ushort)350, armor.Attributes.Hp);
    }

    [Fact]
    public void Synthetic_extended_contribution_list_replays_without_mutating_inputs_or_inheriting_slot_capacity()
    {
        var items = Enumerable.Repeat(new EquipmentAttributeContribution(new(2, 3, 4)), 12).ToImmutableArray(); var copy = items.ToArray();
        var raw = Raw; var a = EquipmentAttributeCalculation.CalculateMode1(raw, Multipliers, 90, 70, new(7, 8, 9), 3, 3, items);
        var b = EquipmentAttributeCalculation.CalculateMode1(raw, Multipliers, 90, 70, new(7, 8, 9), 3, 3, items);
        Assert.Equal(a, b); Assert.Equal(copy, items.ToArray()); Assert.Equal(Raw, raw);
        Assert.Equal(new BattleAttributeValues(24, 36, 48), a.Bonuses); Assert.Equal(new BattleAttributeValues(31, 44, 50), a.ActualAttributes);
    }

    [Fact]
    public void Unknown_nonmonster_equipment_and_invalid_late_raw_fields_return_no_numeric_projection()
    {
        Assert.Throws<ArgumentException>(() => EquipmentAttributeCalculation.CalculateMode1(Raw, Multipliers, 0, 0, default, 3, 3, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => EquipmentAttributeCalculation.CalculateMode1(Raw with { MagicAttack = 0x1000000 }, Multipliers, 0, 0, default, 3, 3, []));
    }
}
