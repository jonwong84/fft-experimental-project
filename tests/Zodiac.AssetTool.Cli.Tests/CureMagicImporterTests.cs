using System.Collections.Immutable;
using Zodiac.Framework;
using Zodiac.Framework.Random;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class CureMagicImporterTests
{
    private static readonly BattleUnitId Mage = new("mage"), Guard = new("guard");
    private static readonly AssetId Cure = new("fft.ps1:ability/source-0001");

    [Theory]
    [InlineData(1,55,10)] [InlineData(30,84,10)] [InlineData(146,200,10)] [InlineData(190,200,10)] [InlineData(200,200,0)]
    public void Cure_publishes_clamped_healing_clears_critical_and_rewards_only_an_actual_change(int hp, int expected, int exp)
    {
        var state = Resolve(Definition((ushort)hp));
        Assert.Equal((ushort)expected, state.Units[1].Vitals.Hp); Assert.False(state.Units[1].Vitals.Critical);
        Assert.Equal((ushort)69, state.Units[0].Current.Mp); Assert.Equal((byte)exp, state.Units[0].Experience);
        Assert.Equal(2UL, state.Turns.RngState.DrawCount); Assert.Empty(state.Pending);
        Assert.Equal(exp == 0 ? AbilityOutcomeMarker.Targeted : AbilityOutcomeMarker.Affected, state.Units[1].OutcomeMarker);
        Assert.Equal((ushort)(exp == 0 ? 50 : 62), state.Units[0].JobPoints[new("fft.ps1:job/wizard")].Current);
        Assert.Equal((byte)0, state.Units[1].KoCount); Assert.Equal((byte)3, state.Units[1].DeathCounter);
    }

    [Fact]
    public void Cure_content_grants_two_skillsets_and_applies_growth_with_no_formula_random_draws()
    {
        var import = Import(); Assert.Equal(39, import.Records.Length); Assert.Equal(2, import.Package.Definition.Spells.Count);
        Assert.Equal(new[] { Cure, new AssetId("fft.ps1:ability/source-0016") }, import.Package.SpellOrder.ToArray());
        var cure = import.Package.Definition.Spells[Cure];
        Assert.Equal(FaithMagicAmountKind.Healing, cure.Spell.Kind); Assert.False(cure.Spell.SecondaryEvadeable);
        Assert.Equal(MagicElement.None, cure.Spell.Elements); Assert.Null(import.VerifiedInputSha256);
        var state = Resolve(Definition(30, 95));
        Assert.Equal((byte)11, state.Units[0].Level); Assert.Equal((byte)0, state.Units[0].Experience);
        Assert.Equal(7UL, state.Turns.RngState.DrawCount); Assert.Equal((ushort)84, state.Units[1].Vitals.Hp);
        Assert.Equal((ushort)50, state.Units[0].JobPoints[new("fft.ps1:job/priest")].Total);
        Assert.Equal((ushort)0, state.Units[0].JobPoints[new("fft.ps1:job/priest")].Current);
    }

    [Fact]
    public void Zero_faith_spends_mp_but_preserves_hp_critical_and_rewards()
    {
        var definition = Definition(30);
        var units = definition.Units.SetItem(0, definition.Units[0] with { Current = definition.Units[0].Current with { Faith = 0 } });
        var state = Resolve(Copy(definition, units));
        Assert.Equal((ushort)30, state.Units[1].Vitals.Hp); Assert.True(state.Units[1].Vitals.Critical);
        Assert.Equal((ushort)69, state.Units[0].Current.Mp); Assert.Equal((byte)0, state.Units[0].Experience);
        Assert.Equal(2UL, state.Turns.RngState.DrawCount);
    }

    [Fact]
    public void A_living_charging_target_keeps_its_pending_cast_after_healing()
    {
        var definition = Definition(30);
        definition = Copy(definition, definition.Units.SetItem(1, definition.Units[1] with { LearnedSpells = [Cure] }));
        var state = Apply(definition.Start(), new OrdinaryMagicCommand.Advance());
        state = Apply(state, new OrdinaryMagicCommand.Cast(Mage, Cure, new(4, 2, 0)));
        state = Apply(state, new OrdinaryMagicCommand.Wait(Mage)); state = Apply(state, new OrdinaryMagicCommand.Advance());
        state = Apply(state, new OrdinaryMagicCommand.Cast(Guard, Cure, new(4, 2, 0)));
        state = Apply(state, new OrdinaryMagicCommand.Wait(Guard)); state = Apply(state, new OrdinaryMagicCommand.Advance());
        Assert.Equal((ushort)84, state.Units[1].Vitals.Hp); Assert.False(state.Units[1].Vitals.Critical);
        Assert.True(state.Units[1].Current.Statuses.Charging); Assert.Equal((byte)3, state.Pending[Guard].Counter);
    }

    [Fact]
    public void Healing_can_resolve_at_the_draw_counter_limit_when_no_growth_is_due()
    {
        var definition = Definition(30); definition = Copy(definition, definition.Units, new(57, ulong.MaxValue - 2));
        var state = Resolve(definition);
        Assert.Equal(ulong.MaxValue, state.Turns.RngState.DrawCount); Assert.Equal((ushort)84, state.Units[1].Vitals.Hp);
    }

    [Theory] [InlineData("formula")] [InlineData("flags")] [InlineData("slot")] [InlineData("menu")] [InlineData("learning")]
    public void Unsupported_Cure_dependencies_are_rejected(string change)
    {
        byte[] image = Image();
        switch (change)
        {
            case "formula": image[Offset(0x8005fbf0 + 14) + 8] = 13; break;
            case "flags": image[Offset(0x8005fbf0 + 14) + 6] = 64; break;
            case "slot": image[Offset(0x80064a94 + 10 * 25) + 3] = 2; break;
            case "menu": image[Offset(0x80065cb4 + 10)] = 8; break;
            case "learning": image[Offset(0x8005ebf0 + 8) + 1] = 255; break;
        }
        Assert.Throws<InvalidDataException>(() => OriginalOrdinaryMagicImporter.DecodeSelection(image, OriginalDefaultAbilitySelection.CureAndFire));
    }

    private static OrdinaryMagicBattleState Resolve(OrdinaryMagicBattleDefinition definition)
    {
        var state = Apply(definition.Start(), new OrdinaryMagicCommand.Advance());
        state = Apply(state, new OrdinaryMagicCommand.Cast(Mage, Cure, new(4, 2, 0)));
        state = Apply(state, new OrdinaryMagicCommand.Wait(Mage)); state = Apply(state, new OrdinaryMagicCommand.Advance());
        state = Apply(state, new OrdinaryMagicCommand.Wait(Guard)); return Apply(state, new OrdinaryMagicCommand.Advance());
    }
    private static OrdinaryMagicBattleState Apply(OrdinaryMagicBattleState state, OrdinaryMagicCommand command)
    { var result = OrdinaryMagicBattle.Apply(state, command); Assert.True(result.Accepted, result.Rejection); return result.State; }
    private static OrdinaryMagicBattleDefinition Definition(ushort hp, byte exp = 0)
    {
        var definition = Import().Package.Definition;
        return Copy(definition, definition.Units.SetItem(0, definition.Units[0] with { Experience = exp })
            .SetItem(1, definition.Units[1] with { Vitals = definition.Units[1].Vitals with { Hp = hp, Critical = hp <= 40 } }));
    }
    private static OrdinaryMagicBattleDefinition Copy(OrdinaryMagicBattleDefinition definition,
        ImmutableArray<OrdinaryMagicUnitDefinition> units, RandomState? rng = null) =>
        new(definition.Map, units, definition.Spells.Values, definition.Jobs, definition.JobThresholds, rng ?? definition.InitialRng,
            definition.MovementGround, definition.Learning);
    private static OriginalOrdinaryMagicImport Import() => OriginalOrdinaryMagicImporter.DecodeSelection(Image(), OriginalDefaultAbilitySelection.CureAndFire);
    private static int Offset(uint address) => checked((int)(address - 0x80010000 + 0x800));
    internal static byte[] Image()
    {
        byte[] image = OriginalOrdinaryMagicImporterTests.Image();
        new byte[] { 4, 1, 1, 0, 0, 226, 0, 0, 12, 0, 14, 0, 4, 6 }.CopyTo(image, Offset(0x8005fbf0 + 14));
        image[Offset(0x8005ebf0 + 8)] = 50; image[Offset(0x80064a94 + 10 * 25) + 3] = 1;
        return image;
    }
}
