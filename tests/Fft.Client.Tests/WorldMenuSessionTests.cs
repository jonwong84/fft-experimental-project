using System.Text.Json;
using Fft.Client.Navigation;
using Zodiac.Content.Samples;
using Zodiac.Content.Saves;
using Zodiac.Framework.Campaign;
using Xunit;

namespace Fft.Client.Tests;

public sealed class WorldMenuSessionTests
{
    private static WorldMenuSession Create(out string directory, int? funds = null)
    {
        directory = Path.Combine(Path.GetTempPath(), "fft-world-menu-" + Guid.NewGuid().ToString("N"));
        var profile = SyntheticWorldCheckpoint.CreateProfile();
        WorldCheckpointState? state = null;
        if (funds.HasValue)
        {
            var start = SyntheticWorldCheckpoint.Start(profile);
            state = profile.Definition.Restore(profile.Definition.RouteId, start.LocationId, start.Party, start.Inventory,
                funds.Value, start.Date, profile.Definition.ProgressFlagId, start.Progress, start.ElapsedSeconds);
        }
        return new(new WorldSaveSlotStore(profile, directory), state);
    }

    [Fact]
    public void Fresh_home_disables_resume_and_new_does_not_also_travel_or_create_files()
    {
        var menu = Create(out var directory);
        Assert.Null(menu.View.Checkpoint);
        Assert.Equal(WorldMenuAction.New, menu.View.Focus);
        Assert.False(menu.View.Entries.Single(e => e.Action == WorldMenuAction.Resume).Enabled);
        Assert.False(menu.Activate(WorldMenuAction.Resume).Handled);
        Assert.True(menu.Handle(WorldMenuInput.Confirm).Handled);
        Assert.Equal(WorldMenuScreen.World, menu.View.Screen);
        Assert.Equal(SyntheticWorldCheckpoint.Harbor, menu.View.Checkpoint!.LocationId);
        Assert.Equal(100, menu.View.Checkpoint.Funds);
        Assert.Equal(42, menu.View.Checkpoint.ElapsedSeconds);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void World_travel_matches_complete_service_transition_and_retained_view_stays_immutable()
    {
        var menu = Create(out var directory);
        menu.Activate(WorldMenuAction.New);
        var before = menu.View;
        var expected = WorldCheckpointSimulator.Travel(before.Checkpoint!, SyntheticWorldCheckpoint.Hill);
        var actual = menu.Handle(WorldMenuInput.Confirm).Travel!;
        Assert.True(actual.Accepted);
        Assert.Equal(expected.Events.ToArray(), actual.Events.ToArray());
        var profile = new WorldCheckpointSaveProfile("sample:profile/world-checkpoint", 1, 1, before.Checkpoint!.Definition);
        Assert.Equal(WorldCheckpointSave.ToUtf8(profile, expected.State), WorldCheckpointSave.ToUtf8(profile, menu.View.Checkpoint!));
        Assert.Equal(100, before.Checkpoint.Funds);
        Assert.Equal("Travel to Hill", before.Entries[0].Label);
        Assert.Equal("Travel to Harbor", menu.View.Entries[0].Label);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void Party_and_home_return_restore_focus_and_resume_retains_exact_checkpoint()
    {
        var menu = Create(out _);
        menu.Activate(WorldMenuAction.New); menu.Activate(WorldMenuAction.Travel);
        var state = menu.View.Checkpoint;
        menu.Activate(WorldMenuAction.Party);
        Assert.Same(state, menu.View.Checkpoint);
        Assert.Single(menu.View.Entries);
        menu.Handle(WorldMenuInput.Back);
        Assert.Equal(WorldMenuAction.Party, menu.View.Focus);
        menu.Handle(WorldMenuInput.Back);
        Assert.Equal(WorldMenuScreen.Home, menu.View.Screen);
        Assert.Equal(WorldMenuAction.Resume, menu.View.Focus);
        menu.Handle(WorldMenuInput.Confirm);
        Assert.Equal(WorldMenuScreen.World, menu.View.Screen);
        Assert.Same(state, menu.View.Checkpoint);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void New_confirmation_defaults_to_cancel_and_replaces_only_after_accept(bool useBack)
    {
        var menu = Create(out var directory);
        menu.Activate(WorldMenuAction.New); menu.Activate(WorldMenuAction.Travel);
        var state = menu.View.Checkpoint;
        menu.Activate(WorldMenuAction.Home); menu.Activate(WorldMenuAction.New);
        Assert.Equal(WorldMenuScreen.ConfirmNew, menu.View.Screen);
        Assert.Equal(WorldMenuAction.Cancel, menu.View.Focus);
        Assert.False(menu.Activate(WorldMenuAction.Travel).Handled);
        Assert.False(menu.RequestExit().Handled);
        menu.Handle(useBack ? WorldMenuInput.Back : WorldMenuInput.Confirm);
        Assert.Equal(WorldMenuScreen.Home, menu.View.Screen);
        Assert.Equal(WorldMenuAction.New, menu.View.Focus);
        Assert.Same(state, menu.View.Checkpoint);
        menu.Handle(WorldMenuInput.Confirm);
        menu.Handle(WorldMenuInput.Down); menu.Handle(WorldMenuInput.Confirm);
        Assert.Equal(WorldMenuScreen.World, menu.View.Screen);
        Assert.Equal(100, menu.View.Checkpoint!.Funds);
        Assert.Equal(93, state!.Funds);
        Assert.NotSame(state, menu.View.Checkpoint);
        Assert.False(Directory.Exists(directory));
    }

    [Theory]
    [InlineData(WorldMenuScreen.Home)] [InlineData(WorldMenuScreen.World)] [InlineData(WorldMenuScreen.Party)]
    public void Host_exit_cancels_to_origin_with_focus_then_accept_closes_without_mutation(WorldMenuScreen origin)
    {
        var menu = Create(out var directory);
        menu.Activate(WorldMenuAction.New);
        if (origin == WorldMenuScreen.Home) menu.Activate(WorldMenuAction.Home);
        if (origin == WorldMenuScreen.Party) menu.Activate(WorldMenuAction.Party);
        var before = menu.View;
        Assert.True(menu.RequestExit().Handled);
        Assert.Equal(WorldMenuAction.Cancel, menu.View.Focus);
        Assert.False(menu.RequestExit().Handled);
        menu.Handle(WorldMenuInput.Back);
        Assert.Equal(before.Screen, menu.View.Screen);
        Assert.Equal(before.Focus, menu.View.Focus);
        Assert.Same(before.Checkpoint, menu.View.Checkpoint);
        menu.RequestExit(); menu.Activate(WorldMenuAction.Accept);
        Assert.Equal(WorldMenuScreen.Closed, menu.View.Screen);
        Assert.Null(menu.View.Focus); Assert.Empty(menu.View.Entries);
        Assert.Same(before.Checkpoint, menu.View.Checkpoint);
        Assert.False(menu.Activate(WorldMenuAction.New).Handled);
        Assert.False(menu.Handle(WorldMenuInput.Back).Handled);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void Fresh_home_back_and_exit_both_require_confirmation_without_creating_a_session()
    {
        var menu = Create(out _);
        menu.Handle(WorldMenuInput.Back);
        Assert.Equal(WorldMenuScreen.ConfirmExit, menu.View.Screen);
        menu.Handle(WorldMenuInput.Confirm);
        Assert.Equal(WorldMenuScreen.Home, menu.View.Screen);
        Assert.Equal(WorldMenuAction.New, menu.View.Focus);
        menu.Activate(WorldMenuAction.Exit); menu.Activate(WorldMenuAction.Cancel);
        Assert.Equal(WorldMenuAction.Exit, menu.View.Focus);
        Assert.Null(menu.View.Checkpoint);
    }

    [Theory]
    [InlineData(WorldMenuInput.Down)] [InlineData(WorldMenuInput.Up)]
    public void Focus_wraps_and_skips_disabled_resume(WorldMenuInput direction)
    {
        var menu = Create(out _);
        menu.Handle(direction); Assert.Equal(WorldMenuAction.Exit, menu.View.Focus);
        menu.Handle(direction); Assert.Equal(WorldMenuAction.New, menu.View.Focus);
        menu.Activate(WorldMenuAction.New); menu.Activate(WorldMenuAction.Party);
        menu.Handle(direction); Assert.Equal(WorldMenuAction.Back, menu.View.Focus);
    }

    [Theory]
    [InlineData(0)] [InlineData(6)]
    public void Insufficient_funds_preserve_checkpoint_and_report_service_rejection(int funds)
    {
        var menu = Create(out _, funds); menu.Activate(WorldMenuAction.Resume);
        var before = menu.View.Checkpoint;
        var result = menu.Activate(WorldMenuAction.Travel);
        Assert.True(result.Handled);
        Assert.Equal(WorldTravelRejection.InsufficientFunds, result.Travel!.Rejection);
        Assert.Empty(result.Travel.Events); Assert.Same(before, menu.View.Checkpoint);
        Assert.Contains("InsufficientFunds", menu.View.Notice);
    }

    [Theory]
    [InlineData(WorldMenuAction.Travel)] [InlineData(WorldMenuAction.Accept)]
    [InlineData(WorldMenuAction.Back)] [InlineData((WorldMenuAction)999)]
    public void Off_screen_or_unknown_actions_leave_entire_view_unchanged(WorldMenuAction action)
    {
        var menu = Create(out _); string before = JsonSerializer.Serialize(menu.View);
        Assert.False(menu.Activate(action).Handled);
        Assert.Equal(before, JsonSerializer.Serialize(menu.View));
        Assert.False(menu.Handle((WorldMenuInput)999).Handled);
        Assert.Equal(before, JsonSerializer.Serialize(menu.View));
    }

    [Fact]
    public void Logical_script_repeats_views_and_events_without_storage_or_device_input()
    {
        static string Run(out string directory)
        {
            var menu = Create(out directory);
            WorldMenuInput[] inputs = [WorldMenuInput.Confirm, WorldMenuInput.Confirm, WorldMenuInput.Down,
                WorldMenuInput.Confirm, WorldMenuInput.Back, WorldMenuInput.Back, WorldMenuInput.Confirm,
                WorldMenuInput.Confirm, WorldMenuInput.Back, WorldMenuInput.Up, WorldMenuInput.Confirm,
                WorldMenuInput.Confirm, WorldMenuInput.Down, WorldMenuInput.Confirm, WorldMenuInput.Back,
                WorldMenuInput.Down, WorldMenuInput.Confirm, WorldMenuInput.Down, WorldMenuInput.Confirm];
            var history = inputs.Select(input => { var result = menu.Handle(input); return new { view = menu.View, result }; }).ToArray();
            Assert.Equal(WorldMenuScreen.Closed, menu.View.Screen);
            Assert.Equal(86, menu.View.Checkpoint!.Funds);
            Assert.Equal(64, menu.View.Checkpoint.ElapsedSeconds);
            Assert.Equal(2, history.Sum(step => step.result.Travel?.Events.Length ?? 0));
            return JsonSerializer.Serialize(history);
        }
        string first = Run(out var firstDirectory), second = Run(out var secondDirectory);
        Assert.Equal(first, second);
        Assert.False(Directory.Exists(firstDirectory)); Assert.False(Directory.Exists(secondDirectory));
    }
}
