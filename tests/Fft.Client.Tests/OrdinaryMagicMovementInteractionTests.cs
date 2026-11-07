using Fft.Client.Presentation;
using TestSupport;
using Zodiac.Content.BattleSetup;
using Zodiac.Framework;
using Xunit;

namespace Fft.Client.Tests;

public sealed class OrdinaryMagicMovementInteractionTests
{
    private static OrdinaryMagicInteraction Active()
    {
        var ui = new OrdinaryMagicInteraction(PreparedOrdinaryMagicBattle.Create(AuthoredOrdinaryMagic.Document()));
        ui.Advance(); return ui;
    }
    private static void ResolveAfterTurn(OrdinaryMagicInteraction ui) { ui.Advance(); ui.Wait(); ui.Advance(); }

    [Fact]
    public void Full_range_and_source_path_preview_can_be_canceled_without_state_or_rng_changes()
    {
        var ui = Active(); string before = OrdinaryMagicSnapshot.Capture(ui.State);
        ui.BeginMove(); Assert.Equal(OrdinaryMagicInteractionMode.Move, ui.Mode);
        Assert.Contains(new TilePosition(2, 1, 0), ui.Range);
        Assert.DoesNotContain(new TilePosition(0, 2, 0), ui.Range); Assert.DoesNotContain(new TilePosition(4, 2, 0), ui.Range);
        ui.Select(new(2, 1, 0)); Assert.Equal(new byte[] { 0, 0, 128 }, ui.PreviewPath!.PackedSteps.ToArray());
        Assert.Equal(before, OrdinaryMagicSnapshot.Capture(ui.State)); Assert.Single(ui.Journal);
        ui.Cancel(); Assert.Null(ui.PreviewPath); Assert.Empty(ui.Range);
        Assert.Equal(before, OrdinaryMagicSnapshot.Capture(ui.State));
    }

    [Fact]
    public void Move_then_cast_uses_the_new_position_and_publishes_both_choices_with_exact_replay()
    {
        var ui = Active(); ui.BeginMove(); ui.Select(new(2, 1, 0)); ui.Confirm();
        Assert.Equal(BattleFacing.South, ui.State.Units[0].Facing); Assert.True(ui.State.Turns.Units[0].MovementTaken);
        Assert.Equal((ushort)75, ui.State.Units[0].Current.Mp); Assert.Equal(1UL, ui.State.Turns.RngState.DrawCount);
        ui.BeginMove(); Assert.True(ui.IsError);
        ui.BeginCast(); ui.Select(new(4, 2, 0)); ui.Confirm();
        Assert.Null(ui.State.Turns.ActiveUnitId); Assert.Contains("N advances", ui.Message); Assert.Equal((byte)4, ui.State.Pending[new("mage")].Counter);
        ResolveAfterTurn(ui);
        Assert.Equal((ushort)146, ui.State.Units[1].Vitals.Hp); Assert.Equal((ushort)69, ui.State.Units[0].Current.Mp);
        Assert.Contains(ui.Journal, c => c is OrdinaryMagicCommand.Move);
        Assert.Equal(OrdinaryMagicSnapshot.Capture(ui.State),
            OrdinaryMagicSnapshot.Capture(OrdinaryMagicSnapshot.Replay(ui.State.Definition, ui.Journal)));
    }

    [Fact]
    public void Cast_then_move_preserves_charge_and_finishes_without_a_second_wait()
    {
        var ui = Active(); ui.BeginCast(); ui.Select(new(4, 2, 0)); ui.Confirm();
        var pending = ui.State.Pending[new("mage")];
        ui.BeginMove(); ui.Select(new(2, 1, 0)); ui.Confirm();
        Assert.Null(ui.State.Turns.ActiveUnitId); Assert.Same(pending, ui.State.Pending[new("mage")]);
        Assert.True(ui.State.Units[0].Current.Statuses.Charging); Assert.Equal(10UL, ui.State.Turns.Ticks);
        Assert.Equal(1UL, ui.State.Turns.RngState.DrawCount); Assert.False(ui.IsError);
        ResolveAfterTurn(ui); Assert.Equal((ushort)146, ui.State.Units[1].Vitals.Hp); Assert.Empty(ui.State.Pending);
    }

    [Fact]
    public void Invalid_move_confirmation_preserves_state_and_a_new_legal_choice_can_succeed()
    {
        var ui = Active(); string before = OrdinaryMagicSnapshot.Capture(ui.State);
        ui.BeginMove(); ui.Select(new(4, 2, 0)); ui.Confirm(); Assert.True(ui.IsError); Assert.Null(ui.PreviewPath);
        Assert.Equal(before, OrdinaryMagicSnapshot.Capture(ui.State));
        ui.Select(new(6, 4, 0)); ui.Confirm(); Assert.True(ui.IsError); Assert.Equal(before, OrdinaryMagicSnapshot.Capture(ui.State));
        ui.Select(new(2, 1, 0)); ui.Confirm(); Assert.False(ui.IsError); Assert.Equal(new TilePosition(2, 1, 0), ui.State.Units[0].Position);
        ui.Wait(); Assert.Equal(20, ui.State.Turns.Units[0].Ct);
        Assert.Equal(OrdinaryMagicSnapshot.Capture(ui.State),
            OrdinaryMagicSnapshot.Capture(OrdinaryMagicSnapshot.Replay(ui.State.Definition, ui.Journal)));
    }
}
