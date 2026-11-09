using Fft.Client.Presentation;
using TestSupport;
using Zodiac.Content.BattleSetup;
using Zodiac.Framework;
using Xunit;

namespace Fft.Client.Tests;

public sealed class DefaultAbilitySpellSelectionTests
{
    private const string Bolt2 = "test:spell/z-bolt2", Ice4 = "test:spell/a-ice4";
    private static DefaultAbilityInteraction Interaction(bool learnIce = true)
    {
        var document = AuthoredDefaultAbility.Document(); var fire = document.Spells[0];
        document = document with { Spells = [fire,
            fire with { Id = Bolt2, Name = "Bolt 2", OriginalId = 21, MpCost = 10, ChargeTime = 5,
                Formula = new(FaithMagicAmountKind.Damage, 18, MagicElement.Lightning, true), Targeting = new(4, 1, 2) },
            fire with { Id = Ice4, Name = "Ice 4", OriginalId = 27, MpCost = 48, ChargeTime = 10,
                Formula = new(FaithMagicAmountKind.Damage, 32, MagicElement.Ice, true), Targeting = new(4, 2, 3, CannotHitCaster: true) }] };
        document.Units[0] = document.Units[0] with { LearnedSpells = learnIce ? [AuthoredDefaultAbility.Fire, Bolt2, Ice4] : [AuthoredDefaultAbility.Fire, Bolt2] };
        return new(PreparedDefaultAbilityBattle.Create(document));
    }

    [Fact]
    public void Menu_keeps_prepared_slot_order_filters_grants_and_wraps_without_gameplay_changes()
    {
        var ui = Interaction(); ui.Advance(); string before = DefaultAbilitySnapshot.Capture(ui.State);
        ui.BeginCast(); Assert.Equal(new AssetId(AuthoredDefaultAbility.Fire), ui.SelectedSpell);
        ui.NextSpell(); Assert.Equal(new AssetId(Bolt2), ui.SelectedSpell);
        ui.NextSpell(); Assert.Equal(new AssetId(Ice4), ui.SelectedSpell);
        ui.NextSpell(); Assert.Equal(new AssetId(AuthoredDefaultAbility.Fire), ui.SelectedSpell);
        ui.NextSpell(-1); Assert.Equal(new AssetId(Ice4), ui.SelectedSpell);
        ui.Select(new(2, 2, 0)); Assert.DoesNotContain(new TilePosition(0, 2, 0), ui.Area);
        Assert.Contains(new TilePosition(4, 2, 0), ui.Area);
        Assert.Equal(before, DefaultAbilitySnapshot.Capture(ui.State)); Assert.Single(ui.Journal);
        var partial = Interaction(false); partial.Advance(); partial.BeginCast();
        Assert.Equal(new[] { new AssetId(AuthoredDefaultAbility.Fire), new AssetId(Bolt2) }, partial.AvailableSpells.ToArray());
        partial.SelectSpell(new(Ice4)); Assert.True(partial.IsError); Assert.Equal(new AssetId(AuthoredDefaultAbility.Fire), partial.SelectedSpell);
    }

    [Fact]
    public void Confirmed_selection_uses_its_own_formula_mp_charge_and_replay_command()
    {
        var ui = Interaction(); ui.Advance(); ui.BeginCast(); ui.NextSpell(); ui.Select(new(4, 2, 0)); ui.Confirm();
        Assert.Equal((byte)5, ui.State.Pending[new("mage")].Counter); Assert.Equal((ushort)75, ui.State.Units[0].Current.Mp);
        ui.Wait(); ui.Advance(); ui.Wait(); ui.Advance();
        Assert.Equal((ushort)130, ui.State.Units[1].Vitals.Hp); Assert.Equal((ushort)65, ui.State.Units[0].Current.Mp);
        Assert.Equal(14UL, ui.State.Turns.Ticks);
        Assert.Contains(ui.Journal, c => c is DefaultAbilityCommand.Cast cast && cast.SpellId == new AssetId(Bolt2));
        Assert.Equal(DefaultAbilitySnapshot.Capture(ui.State),
            DefaultAbilitySnapshot.Capture(DefaultAbilitySnapshot.Replay(ui.State.Definition, ui.Journal)));
    }

    [Fact]
    public void Changing_spells_clears_old_aim_and_cannot_confirm_a_stale_target()
    {
        var ui = Interaction(); ui.Advance(); ui.BeginCast(); ui.Select(new(4, 2, 0));
        ui.SelectSpell(new(Bolt2)); Assert.Null(ui.Aim); Assert.Empty(ui.Area);
        string before = DefaultAbilitySnapshot.Capture(ui.State); ui.Confirm(); Assert.True(ui.IsError);
        Assert.Equal(before, DefaultAbilitySnapshot.Capture(ui.State));
        ui.Select(new(4, 2, 0)); ui.Confirm(); Assert.False(ui.IsError);
        ui.Wait(); ui.Advance(); Assert.Empty(ui.AvailableSpells); ui.BeginCast(); Assert.True(ui.IsError);
    }

    [Fact]
    public void Unaffordable_selected_spell_rejects_without_spending_or_consuming_the_turn()
    {
        var document = AuthoredDefaultAbility.Document(); document.Spells[0] = document.Spells[0] with { MpCost = 300 };
        var ui = new DefaultAbilityInteraction(PreparedDefaultAbilityBattle.Create(document)); ui.Advance();
        string before = DefaultAbilitySnapshot.Capture(ui.State);
        ui.BeginCast(); ui.Select(new(4, 2, 0)); ui.Confirm();
        Assert.True(ui.IsError); Assert.Contains("Not enough MP", ui.Message);
        Assert.Equal(before, DefaultAbilitySnapshot.Capture(ui.State)); Assert.Empty(ui.State.Pending);
    }
}
