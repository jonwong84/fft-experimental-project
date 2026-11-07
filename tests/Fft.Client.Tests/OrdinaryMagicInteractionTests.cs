using Fft.Client.Presentation;
using TestSupport;
using Zodiac.Content.BattleSetup;
using Zodiac.Framework;
using Xunit;

namespace Fft.Client.Tests;

public sealed class OrdinaryMagicInteractionTests
{
    private static OrdinaryMagicInteraction Interaction() => new(PreparedOrdinaryMagicBattle.Create(AuthoredOrdinaryMagic.Document()));
    private static void Queue(OrdinaryMagicInteraction ui, TilePosition? aim = null)
    {
        ui.Advance(); ui.BeginCast(); ui.Select(aim ?? new(4, 2, 0)); ui.Confirm();
    }
    private static void Resolve(OrdinaryMagicInteraction ui)
    { ui.Wait(); ui.Advance(); ui.Wait(); ui.Advance(); }

    [Fact]
    public void Aim_area_follow_toggle_and_cancel_never_publish_gameplay_or_spend_mp()
    {
        var ui = Interaction(); ui.Advance();
        string before = OrdinaryMagicSnapshot.Capture(ui.State); int commands = ui.Journal.Length;
        ui.BeginCast(); ui.Select(new(4, 2, 0));
        Assert.Contains(new TilePosition(4, 2, 0), ui.Range);
        Assert.Equal(5, ui.Area.Length); Assert.True(ui.FollowUnit);
        ui.ToggleFollow(); Assert.False(ui.FollowUnit);
        ui.Cancel(); Assert.Empty(ui.Area); Assert.Null(ui.Aim);
        Assert.Equal(before, OrdinaryMagicSnapshot.Capture(ui.State)); Assert.Equal(commands, ui.Journal.Length);
    }

    [Fact]
    public void Player_route_queues_then_publishes_actual_loaded_spell_results_and_replays_exactly()
    {
        var ui = Interaction(); Queue(ui);
        Assert.True(ui.State.Units[0].Current.Statuses.Charging); Assert.Equal((ushort)75, ui.State.Units[0].Current.Mp);
        Resolve(ui);
        Assert.Equal((ushort)146, ui.State.Units[1].Vitals.Hp); Assert.Equal((ushort)69, ui.State.Units[0].Current.Mp);
        Assert.Equal((byte)11, ui.State.Units[0].Level); Assert.Equal((byte)0, ui.State.Units[0].Experience);
        Assert.Equal((ushort)62, ui.State.Units[0].JobPoints[new(AuthoredOrdinaryMagic.Wizard)].Current);
        Assert.Empty(ui.State.Pending); Assert.False(ui.IsError);
        Assert.Contains(ui.LastEvents, e => e is OrdinaryMagicEvent.Rewarded { Exp: 10, Jp: 12, Level: 11 });
        Assert.Equal(OrdinaryMagicSnapshot.Capture(ui.State),
            OrdinaryMagicSnapshot.Capture(OrdinaryMagicSnapshot.Replay(ui.State.Definition, ui.Journal)));
    }

    [Fact]
    public void Invalid_aim_and_repeated_action_preserve_state_and_allow_a_valid_choice()
    {
        var ui = Interaction(); ui.Advance(); string before = OrdinaryMagicSnapshot.Capture(ui.State);
        ui.Advance(); Assert.True(ui.IsError); Assert.Equal(before, OrdinaryMagicSnapshot.Capture(ui.State));
        ui.BeginCast(); ui.Select(new(6, 4, 0)); ui.Confirm(); Assert.True(ui.IsError);
        Assert.Equal(before, OrdinaryMagicSnapshot.Capture(ui.State));
        ui.Select(new(4, 2, 0)); ui.Confirm(); Assert.False(ui.IsError);
        string queued = OrdinaryMagicSnapshot.Capture(ui.State); ui.BeginCast(); Assert.True(ui.IsError);
        Assert.Equal(queued, OrdinaryMagicSnapshot.Capture(ui.State));
        ui.Wait(); ui.Advance(); ui.BeginCast(); Assert.Contains("no learned spells", ui.Message);
        Assert.Equal(OrdinaryMagicSnapshot.Capture(ui.State),
            OrdinaryMagicSnapshot.Capture(OrdinaryMagicSnapshot.Replay(ui.State.Definition, ui.Journal)));
    }

    [Fact]
    public void Tile_choice_can_miss_a_moving_target_and_still_pay_the_cast_cost()
    {
        var ui = Interaction(); Queue(ui, new(3, 2, 0)); ui.Wait(); ui.Advance();
        ui.BeginStep(); ui.Select(new(5, 2, 0)); ui.Confirm(); ui.Wait(); ui.Advance();
        Assert.Equal((ushort)200, ui.State.Units[1].Vitals.Hp);
        Assert.Equal((ushort)69, ui.State.Units[0].Current.Mp); Assert.Equal((byte)95, ui.State.Units[0].Experience);
        Assert.Contains(ui.LastEvents, e => e is OrdinaryMagicEvent.Fizzled { MpCost: 6 });
        Assert.Equal(new TilePosition(5, 2, 0), ui.State.Units[1].Position);
    }

    [Fact]
    public void Restart_recreates_the_same_first_cast_and_clears_only_the_local_journal()
    {
        var ui = Interaction(); string initial = OrdinaryMagicSnapshot.Capture(ui.State);
        Queue(ui); Resolve(ui); string first = OrdinaryMagicSnapshot.Capture(ui.State);
        ui.Restart(); Assert.Equal(initial, OrdinaryMagicSnapshot.Capture(ui.State)); Assert.Empty(ui.Journal); Assert.Empty(ui.Log);
        Queue(ui); Resolve(ui); Assert.Equal(first, OrdinaryMagicSnapshot.Capture(ui.State));
    }

    [Fact]
    public void First_ko_finishes_the_demo_and_rejected_input_cannot_advance_it()
    {
        var ui = Interaction();
        for (int i = 0; i < 80 && !ui.State.Finished; i++)
        {
            if (ui.State.Turns.ActiveUnitId is { } actor)
            {
                if (actor.Value == "mage" && !ui.State.Pending.ContainsKey(actor))
                { ui.BeginCast(); ui.Select(new(4, 2, 0)); ui.Confirm(); }
                ui.Wait();
            }
            else ui.Advance();
            Assert.False(ui.IsError, ui.Message);
        }
        Assert.True(ui.State.Finished); Assert.Equal((ushort)0, ui.State.Units[1].Vitals.Hp);
        Assert.Equal((byte)1, ui.State.Units[1].KoCount); Assert.Equal((byte)3, ui.State.Units[1].DeathCounter);
        string final = OrdinaryMagicSnapshot.Capture(ui.State); ui.Advance(); Assert.True(ui.IsError);
        Assert.Equal(final, OrdinaryMagicSnapshot.Capture(ui.State));
        Assert.Equal(final, OrdinaryMagicSnapshot.Capture(OrdinaryMagicSnapshot.Replay(ui.State.Definition, ui.Journal)));
    }
}
