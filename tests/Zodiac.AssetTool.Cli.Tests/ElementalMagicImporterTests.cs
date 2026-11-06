using Zodiac.Framework;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class ElementalMagicImporterTests
{
    public static IEnumerable<object[]> Cases =>
    [
        [16, 54, 6, 4], [17, 70, 12, 5], [18, 94, 24, 7], [19, 125, 48, 10],
        [20, 54, 6, 4], [21, 70, 10, 5], [22, 94, 24, 7], [23, 125, 48, 10],
        [24, 54, 6, 4], [25, 70, 12, 5], [26, 94, 24, 7], [27, 125, 48, 10]
    ];

    [Theory] [MemberData(nameof(Cases))]
    public void Every_selected_spell_runs_the_shared_command_path_with_its_own_power_cost_and_charge(int originalId, int damage, int cost, int charge)
    {
        var import = OriginalOrdinaryMagicImporter.DecodeSelection(Image(), OriginalOrdinarySpellSelection.ElementalTiers);
        Assert.Equal(56, import.Records.Length); Assert.Equal(12, import.Package.Definition.Spells.Count);
        var definition = import.Package.Definition;
        var spell = definition.Spells.Single(p => p.Value.OriginalAbilityId == originalId).Value;
        var state = definition.Start();
        void Apply(OrdinaryMagicCommand command)
        { var r = OrdinaryMagicBattle.Apply(state, command); Assert.True(r.Accepted, r.Rejection); state = r.State; }
        Apply(new OrdinaryMagicCommand.Advance());
        Apply(new OrdinaryMagicCommand.Cast(new("mage"), spell.Id, new(4, 2, 0)));
        Assert.Equal((byte)charge, state.Pending[new("mage")].Counter); Assert.Equal((ushort)75, state.Units[0].Current.Mp);
        Apply(new OrdinaryMagicCommand.Wait(new("mage")));
        for (int i = 0; i < 20 && !state.Pending.IsEmpty; i++)
            Apply(state.Turns.ActiveUnitId is { } active ? new OrdinaryMagicCommand.Wait(active) : new OrdinaryMagicCommand.Advance());
        Assert.Empty(state.Pending); Assert.Equal((ushort)(200 - damage), state.Units[1].Vitals.Hp);
        Assert.Equal((ushort)(75 - cost), state.Units[0].Current.Mp);
        Assert.Equal(10UL + (ulong)charge - 1, state.Turns.Ticks);
        Assert.Equal((byte)11, state.Units[0].Level); Assert.Equal((byte)0, state.Units[0].Experience);
        // The CT10 tier also reaches Mage's regular tick18 turn while charging.
        Assert.Equal(originalId % 4 == 3 ? 13UL : 12UL, state.Turns.RngState.DrawCount);
    }

    [Theory] [InlineData(19)] [InlineData(23)] [InlineData(27)]
    public void Fourth_tier_uses_radius_two_but_excludes_the_caster(int originalId)
    {
        var definition = OriginalOrdinaryMagicImporter.DecodeSelection(Image(), OriginalOrdinarySpellSelection.ElementalTiers).Package.Definition;
        var state = OrdinaryMagicBattle.Apply(definition.Start(), new OrdinaryMagicCommand.Advance()).State;
        var spell = definition.Spells.Values.Single(s => s.OriginalAbilityId == originalId);
        state = OrdinaryMagicBattle.Apply(state, new OrdinaryMagicCommand.Cast(new("mage"), spell.Id, new(2, 2, 0), false)).State;
        state = OrdinaryMagicBattle.Apply(state, new OrdinaryMagicCommand.Wait(new("mage"))).State;
        for (int i = 0; i < 20 && !state.Pending.IsEmpty; i++)
            state = OrdinaryMagicBattle.Apply(state, state.Turns.ActiveUnitId is { } active
                ? new OrdinaryMagicCommand.Wait(active) : new OrdinaryMagicCommand.Advance()).State;
        Assert.Empty(state.Pending); Assert.Equal((ushort)100, state.Units[0].Vitals.Hp);
        Assert.Equal((ushort)75, state.Units[1].Vitals.Hp); Assert.True(spell.Targeting!.CannotHitCaster);
        Assert.Equal((ushort)27, state.Units[0].Current.Mp);
    }

    [Fact]
    public void Elemental_selection_preserves_slot_order_and_charges_all_explicit_grants_to_the_authored_career()
    {
        var import = OriginalOrdinaryMagicImporter.DecodeSelection(Image(), OriginalOrdinarySpellSelection.ElementalTiers);
        var mage = import.Package.Definition.Units[0];
        Assert.Equal(12, mage.LearnedSpells.Count);
        Assert.Equal((ushort)260, mage.JobPoints[new("fft.ps1:job/wizard")].Current);
        Assert.Equal((ushort)5000, mage.JobPoints[new("fft.ps1:job/wizard")].Total);
        Assert.Empty(import.Package.Definition.Units[1].LearnedSpells);
        Assert.Null(import.VerifiedInputSha256);
        var ice = import.Package.Definition.Spells[new("fft.ps1:ability/source-0024")];
        Assert.Equal(MagicElement.Ice, ice.Spell.Elements);
        Assert.Equal(MagicElement.Lightning, import.Package.Definition.Spells[new("fft.ps1:ability/source-0020")].Spell.Elements);
    }

    [Fact]
    public void Changed_new_spell_slot_or_flag_is_rejected_before_runtime_content_exists()
    {
        byte[] image = Image(); int offset = Offset(0x8005fbf0 + 23 * 14);
        image[offset + 4] = 0;
        Assert.Throws<InvalidDataException>(() => OriginalOrdinaryMagicImporter.DecodeSelection(image, OriginalOrdinarySpellSelection.ElementalTiers));
        image = Image(); image[Offset(0x80064a94 + 11 * 25) + 3 + 8] = 25;
        Assert.Throws<InvalidDataException>(() => OriginalOrdinaryMagicImporter.DecodeSelection(image, OriginalOrdinarySpellSelection.ElementalTiers));
        Assert.Throws<ArgumentOutOfRangeException>(() => OriginalOrdinaryMagicImporter.DecodeSelection(Image(), (OriginalOrdinarySpellSelection)999));
    }

    private static int Offset(uint address) => checked((int)(address - 0x80010000 + 0x800));
    private static byte[] Image()
    {
        byte[] image = OriginalOrdinaryMagicImporterTests.Image();
        byte[] power = [14, 18, 24, 32], area = [1, 1, 1, 2], vertical = [1, 2, 3, 3], ct = [4, 5, 7, 10];
        ushort[] jp = [50, 200, 480, 850];
        for (int id = 16; id <= 27; id++)
        {
            int tier = (id - 16) % 4; byte element = id < 20 ? (byte)128 : id < 24 ? (byte)64 : (byte)32;
            byte mp = tier switch { 0 => 6, 1 => id == 21 ? (byte)10 : (byte)12, 2 => 24, _ => 48 };
            new byte[] { 4, area[tier], vertical[tier], 0, (byte)(tier == 3 ? 1 : 0), (byte)(tier == 3 ? 34 : 226),
                (byte)(tier == 3 ? 2 : 66), element, 8, 0, power[tier], 0, ct[tier], mp }.CopyTo(image, Offset((uint)(0x8005fbf0 + id * 14)));
            image[Offset((uint)(0x8005ebf0 + id * 8))] = (byte)jp[tier];
            image[Offset((uint)(0x8005ebf0 + id * 8)) + 1] = (byte)(jp[tier] >> 8);
            image[Offset(0x80064a94 + 11 * 25) + 3 + id - 16] = (byte)id;
        }
        return image;
    }
}
