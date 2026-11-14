using Fft.Client.Navigation;
using Fft.Client.Presentation;
using Zodiac.Content.Samples;
using Zodiac.Content.Saves;
using Xunit;

namespace Fft.Client.Tests;

public sealed class WorldSlotMenuTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "fft-slot-ui-" + Guid.NewGuid().ToString("N"));
    private WorldSaveSlotStore Store()
    { var profile = SyntheticWorldCheckpoint.CreateProfile(); return new(profile, root); }
    private WorldMenuSession Menu(WorldSaveSlotStore? store = null) => new(store ?? Store(), enableSaves: true);
    private static void New(WorldMenuSession menu) => menu.Activate(WorldMenuAction.New);
    private static void Details(WorldMenuSession menu, bool save = true, WorldMenuAction slot = WorldMenuAction.Slot01)
    { menu.Activate(save ? WorldMenuAction.OpenSave : WorldMenuAction.OpenLoad); menu.Activate(slot); }
    private static void Accept(WorldMenuSession menu) => menu.Activate(WorldMenuAction.Accept);
    private static void Continue(WorldMenuSession menu) => menu.Activate(WorldMenuAction.ContinueWorld);
    private void Seed(WorldSaveSlotStore store, bool backup = false)
    {
        var state = SyntheticWorldCheckpoint.Start(store.Profile); var revision = store.Save("slot-01", state, null);
        if (backup) store.Save("slot-01", Zodiac.Framework.Campaign.WorldCheckpointSimulator.Travel(state, SyntheticWorldCheckpoint.Hill).State, revision);
    }

    [Fact]
    public void Slot_pages_skip_disabled_controls_and_restore_their_origin_focus()
    {
        var menu = Menu(); Assert.DoesNotContain(menu.View.Entries, e => e.Action == WorldMenuAction.OpenSave);
        menu.Activate(WorldMenuAction.OpenLoad);
        Assert.Equal(9, menu.View.Entries.Length); Assert.Equal(10, menu.View.Slots!.Catalog.Length);
        Assert.False(menu.Activate(WorldMenuAction.Slot06).Handled);
        menu.Activate(WorldMenuAction.NextPage); Assert.Equal(WorldMenuAction.Slot06, menu.View.Focus);
        menu.Activate(WorldMenuAction.Slot10); Assert.Equal("slot-10", menu.View.Slots!.Selected!.SlotId);
        Assert.False(menu.View.Entries.Single(e => e.Action == WorldMenuAction.SaveSelected).Enabled);
        Assert.False(menu.View.Entries.Single(e => e.Action == WorldMenuAction.LoadSelected).Enabled);
        menu.Handle(WorldMenuInput.Back); Assert.Equal(WorldMenuAction.Slot10, menu.View.Focus);
        menu.Handle(WorldMenuInput.Back); Assert.Equal(WorldMenuScreen.Home, menu.View.Screen); Assert.Equal(WorldMenuAction.OpenLoad, menu.View.Focus);
        Assert.False(Directory.Exists(root));
    }
    [Fact]
    public void Empty_save_overwrite_cancel_and_confirm_retain_expected_primary_and_backup()
    {
        var store = Store(); var menu = Menu(store); New(menu); Details(menu);
        var saved = menu.Activate(WorldMenuAction.SaveSelected).Operation!; Assert.True(saved.Succeeded);
        var original = File.ReadAllBytes(Path.Combine(root, "slot-01.json"));
        Continue(menu); menu.Activate(WorldMenuAction.Travel); var live = menu.View.Checkpoint;
        Details(menu); menu.Activate(WorldMenuAction.SaveSelected);
        Assert.Equal(WorldMenuScreen.ConfirmOverwrite, menu.View.Screen); Assert.Equal(WorldMenuAction.Cancel, menu.View.Focus);
        Assert.False(menu.Activate(WorldMenuAction.Travel).Handled); Assert.False(menu.RequestExit().Handled);
        menu.Handle(WorldMenuInput.Confirm); Assert.Equal(original, File.ReadAllBytes(Path.Combine(root, "slot-01.json"))); Assert.Same(live, menu.View.Checkpoint);
        menu.Activate(WorldMenuAction.SaveSelected); Accept(menu);
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(root, "slot-01.bak")));
        Assert.Equal(93, store.Inspect("slot-01").Primary.Summary!.Funds);
    }
    [Fact]
    public void Load_cancel_preserves_live_then_accepted_load_matches_next_command_state_and_events()
    {
        var store = Store(); Seed(store); var menu = Menu(store); New(menu); menu.Activate(WorldMenuAction.Travel); var live = menu.View.Checkpoint;
        Details(menu, false); menu.Activate(WorldMenuAction.LoadSelected); Assert.Equal(WorldMenuAction.Cancel, menu.View.Focus);
        menu.Handle(WorldMenuInput.Back); Assert.Same(live, menu.View.Checkpoint);
        menu.Activate(WorldMenuAction.LoadSelected); Assert.True(menu.Activate(WorldMenuAction.Accept).Operation!.Succeeded);
        Assert.Equal(100, menu.View.Checkpoint!.Funds); Continue(menu);
        var expected = Zodiac.Framework.Campaign.WorldCheckpointSimulator.Travel(SyntheticWorldCheckpoint.Start(store.Profile), SyntheticWorldCheckpoint.Hill);
        var actual = menu.Activate(WorldMenuAction.Travel).Travel!;
        Assert.Equal(expected.Events.ToArray(), actual.Events.ToArray());
        Assert.Equal(WorldCheckpointSave.ToUtf8(store.Profile, expected.State), WorldCheckpointSave.ToUtf8(store.Profile, actual.State));
    }
    [Fact]
    public void Fresh_home_load_requires_no_discard_and_enables_resume()
    {
        var store = Store(); Seed(store); var menu = Menu(store); Details(menu, false);
        Assert.True(menu.Activate(WorldMenuAction.LoadSelected).Operation!.Succeeded); Assert.NotNull(menu.View.Checkpoint);
        Continue(menu); Assert.Equal(WorldMenuScreen.World, menu.View.Screen);
        menu.Activate(WorldMenuAction.Home); Assert.True(menu.View.Entries.Single(e => e.Action == WorldMenuAction.Resume).Enabled);
    }
    [Fact]
    public void Stale_overwrite_errors_preserve_session_and_external_file_until_explicit_refresh()
    {
        var store = Store(); Seed(store); var menu = Menu(store); New(menu); menu.Activate(WorldMenuAction.Travel); Details(menu); menu.Activate(WorldMenuAction.SaveSelected);
        var live = menu.View.Checkpoint;
        store.Save("slot-01", live!, store.Inspect("slot-01").Primary.Revision); var external = File.ReadAllBytes(Path.Combine(root, "slot-01.json"));
        var failed = menu.Activate(WorldMenuAction.Accept).Operation!; Assert.False(failed.Succeeded);
        Assert.Contains("Slot changed", failed.Diagnostic); Assert.Same(live, menu.View.Checkpoint);
        Assert.Equal(external, File.ReadAllBytes(Path.Combine(root, "slot-01.json")));
        menu.Activate(WorldMenuAction.Back); Assert.False(menu.Activate(WorldMenuAction.SaveSelected).Handled);
        Assert.True(menu.View.Slots!.NeedsRefresh); menu.Activate(WorldMenuAction.Refresh);
        Assert.False(menu.View.Slots!.NeedsRefresh); menu.Activate(WorldMenuAction.SaveSelected);
        Assert.Equal(WorldMenuScreen.ConfirmOverwrite, menu.View.Screen);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Missing_or_changed_load_is_rejected_without_replacing_live_session(bool missing)
    {
        var store = Store(); Seed(store); var menu = Menu(store); New(menu); menu.Activate(WorldMenuAction.Travel); var live = menu.View.Checkpoint;
        Details(menu, false); menu.Activate(WorldMenuAction.LoadSelected);
        if (missing) File.Delete(Path.Combine(root, "slot-01.json")); else File.WriteAllText(Path.Combine(root, "slot-01.json"), "{}");
        Assert.False(menu.Activate(WorldMenuAction.Accept).Operation!.Succeeded); Assert.Same(live, menu.View.Checkpoint);
        Assert.Equal(WorldMenuScreen.SlotError, menu.View.Screen);
    }
    [Theory]
    [InlineData("{}")] [InlineData("incompatible")]
    public void Invalid_or_incompatible_primary_exposes_no_summary_and_cannot_load_or_save(string content)
    {
        Directory.CreateDirectory(root);
        if (content == "incompatible")
        {
            var profile = SyntheticWorldCheckpoint.CreateProfile();
            var other = new WorldCheckpointSaveProfile("sample:profile/other", 1, 1, profile.Definition);
            File.WriteAllBytes(Path.Combine(root, "slot-01.json"), WorldCheckpointSave.ToUtf8(other, SyntheticWorldCheckpoint.Start(profile)));
        }
        else File.WriteAllText(Path.Combine(root, "slot-01.json"), content);
        byte[] before = File.ReadAllBytes(Path.Combine(root, "slot-01.json"));
        var menu = Menu(); New(menu); Details(menu);
        Assert.Equal(WorldSaveFileStatus.Invalid, menu.View.Slots!.Selected!.Primary); Assert.Null(menu.View.Slots.Selected.Summary);
        Assert.False(menu.Activate(WorldMenuAction.SaveSelected).Handled); Assert.False(menu.Activate(WorldMenuAction.LoadSelected).Handled);
        Assert.False(menu.Activate(WorldMenuAction.RecoverBackup).Handled); Assert.Equal(before, File.ReadAllBytes(Path.Combine(root, "slot-01.json")));
    }
    [Fact]
    public void Explicit_backup_recovery_can_cancel_then_preserves_corrupt_primary_and_saves_to_another_slot()
    {
        var store = Store(); Seed(store, true); string path = Path.Combine(root, "slot-01.json"); File.WriteAllText(path, "{}");
        var menu = Menu(store); Details(menu, false); menu.Activate(WorldMenuAction.RecoverBackup);
        Assert.Equal(WorldMenuScreen.ConfirmRecovery, menu.View.Screen); Assert.Equal(WorldMenuAction.Cancel, menu.View.Focus);
        menu.Handle(WorldMenuInput.Confirm); Assert.Null(menu.View.Checkpoint);
        menu.Activate(WorldMenuAction.RecoverBackup); Assert.True(menu.Activate(WorldMenuAction.Accept).Operation!.RecoveredFromBackup);
        Assert.Equal("{}", File.ReadAllText(path)); Assert.Equal(100, menu.View.Checkpoint!.Funds);
        Continue(menu); Details(menu, slot: WorldMenuAction.Slot02); Assert.True(menu.Activate(WorldMenuAction.SaveSelected).Operation!.Succeeded);
        Assert.Equal(100, store.Inspect("slot-02").Primary.Summary!.Funds);
    }
    [Fact]
    public void Busy_storage_failure_is_path_free_and_preserves_live_and_files()
    {
        var store = Store(); Seed(store); var menu = Menu(store); New(menu); Details(menu, slot: WorldMenuAction.Slot02); var state = menu.View.Checkpoint;
        using (var held = new FileStream(Path.Combine(root, "slot-02.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var result = menu.Activate(WorldMenuAction.SaveSelected).Operation!;
            Assert.False(result.Succeeded); Assert.DoesNotContain(root, result.Diagnostic); Assert.Same(state, menu.View.Checkpoint);
        }
        Assert.False(File.Exists(Path.Combine(root, "slot-02.json"))); Assert.Equal(100, store.Inspect("slot-01").Primary.Summary!.Funds);
        menu.Activate(WorldMenuAction.Refresh); Assert.True(menu.Activate(WorldMenuAction.SaveSelected).Operation!.Succeeded);
    }
    [Fact]
    public void Unavailable_root_catalog_does_not_offer_save_or_load_and_remains_intact()
    {
        File.WriteAllText(root, "reserved"); var menu = Menu(); New(menu); Details(menu);
        Assert.Equal(WorldSaveFileStatus.Unavailable, menu.View.Slots!.Selected!.Primary);
        Assert.False(menu.Activate(WorldMenuAction.SaveSelected).Handled); Assert.False(menu.Activate(WorldMenuAction.LoadSelected).Handled);
        Assert.Equal("reserved", File.ReadAllText(root));
    }
    [Fact]
    public void Repeated_view_render_and_canceled_dialogs_do_not_refresh_storage_or_change_snapshot()
    {
        var store = Store(); Seed(store); var menu = Menu(store); New(menu); Details(menu);
        var before = menu.View; string primary = Path.Combine(root, "slot-01.json"); File.WriteAllText(primary, "{}");
        for (int i = 0; i < 3; i++) { Assert.Equal(WorldSaveFileStatus.Valid, menu.View.Slots!.Selected!.Primary); WorldMenuPresentation.Render(menu.View); }
        Assert.Equal(WorldSaveFileStatus.Valid, before.Slots!.Selected!.Primary);
        menu.Activate(WorldMenuAction.Refresh); Assert.Equal(WorldSaveFileStatus.Invalid, menu.View.Slots!.Selected!.Primary);
        Assert.Equal("{}", File.ReadAllText(primary));
    }
    [Fact]
    public void Close_from_slot_details_restores_exact_context_on_cancel()
    {
        var menu = Menu(); New(menu); Details(menu); var before = menu.View;
        Assert.True(menu.RequestExit().Handled); menu.Handle(WorldMenuInput.Confirm);
        Assert.Equal(WorldMenuScreen.SlotDetails, menu.View.Screen); Assert.Equal(before.Focus, menu.View.Focus);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Slots), System.Text.Json.JsonSerializer.Serialize(menu.View.Slots)); Assert.Same(before.Checkpoint, menu.View.Checkpoint);
    }
    [Fact]
    public void Compact_slot_rows_are_pickable_and_fit_canvas_without_overlaps()
    {
        var menu = Menu(); menu.Activate(WorldMenuAction.OpenLoad); var view = menu.View;
        for (int i = 0; i < 9; i++)
        {
            var bounds = WorldMenuPresentation.Button(i, view); Assert.InRange(bounds.Y + bounds.Height, 0, 512);
            if (i > 0) Assert.True(WorldMenuPresentation.Button(i - 1, view).Y + bounds.Height < bounds.Y);
            var router = new WorldMenuInputRouter(); router.Baseline(new());
            var command = router.Poll(new(Left: true, Pointer: new(bounds.X + 10, bounds.Y + 10)), view, 960, 640, 960, 640);
            Assert.Equal(view.Entries[i].Enabled, command is not null);
        }
    }
    [Fact]
    public void Legacy_slot_exposes_source_schema_and_loads_through_the_existing_migration()
    {
        Directory.CreateDirectory(root);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Saves", "synthetic-world-idle-v1.json"), Path.Combine(root, "slot-01.json"));
        byte[] before = File.ReadAllBytes(Path.Combine(root, "slot-01.json"));
        var menu = Menu(); Details(menu, false); Assert.Equal(1, menu.View.Slots!.Selected!.SourceSchemaVersion);
        Assert.True(menu.Activate(WorldMenuAction.LoadSelected).Operation!.WasMigrated);
        Assert.Contains("migrated", menu.View.Slots!.Message); Assert.Equal(100, menu.View.Checkpoint!.Funds);
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(root, "slot-01.json")));
    }
    [Fact]
    public void Full_native_plan_repeats_semantics_pixels_storage_and_expected_failures_headlessly()
    {
        string Run(string directory)
        {
            var leftProfile = SyntheticWorldCheckpoint.CreateProfile(); var rightProfile = SyntheticWorldCheckpoint.CreateProfile();
            var leftStore = new WorldSaveSlotStore(leftProfile, Path.Combine(directory, "saves"));
            var rightStore = new WorldSaveSlotStore(rightProfile, Path.Combine(directory, "replay-saves"));
            var menu = Menu(leftStore); var replay = Menu(rightStore);
            using var mutations = new WorldSlotCheckFiles(leftStore, rightStore);
            var router = new WorldMenuInputRouter(); router.Baseline(new());
            var history = new List<object>(); int width = 960, height = 640, successes = 0, failures = 0, events = 0, captures = 0;
            foreach (var step in WorldSlotsCheckPlan.Steps)
            {
                mutations.Apply(step.Mutation);
                if (step.Width > 0) (width, height) = (step.Width, step.Height);
                var sample = step.Sample;
                if (sample.Left)
                {
                    var fit = SceneLayout.Fit(width, height);
                    sample = sample with { Pointer = new(fit.X + sample.Pointer.X * fit.Width / 960, fit.Y + sample.Pointer.Y * fit.Height / 640) };
                }
                var command = step.Close ? new WorldMenuDispatch(Close: true) : router.Poll(sample, menu.View, width, height, width, height);
                WorldMenuResult? result = null;
                if (command is not null)
                {
                    result = command.Apply(menu);
                    Assert.Equal(System.Text.Json.JsonSerializer.Serialize(result), System.Text.Json.JsonSerializer.Serialize(command.Apply(replay)));
                    if (result.Operation is { } operation) { if (operation.Succeeded) successes++; else failures++; }
                    events += result.Travel?.Events.Length ?? 0;
                }
                Assert.Equal(System.Text.Json.JsonSerializer.Serialize(menu.View), System.Text.Json.JsonSerializer.Serialize(replay.View));
                if (step.Capture)
                {
                    uint[] pixels = WorldMenuPresentation.Render(menu.View); captures++;
                    history.Add(new { step.Name, menu.View, result, hash = System.Security.Cryptography.SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(pixels.AsSpan())) });
                }
            }
            Assert.Equal(6, successes); Assert.Equal(3, failures); Assert.Equal(2, events); Assert.Equal(22, captures);
            Assert.Equal(WorldMenuScreen.Closed, menu.View.Screen); Assert.Equal(93, menu.View.Checkpoint!.Funds); Assert.Equal(53, menu.View.Checkpoint.ElapsedSeconds);
            Assert.Equal("{\"corrupt\":true}", File.ReadAllText(Path.Combine(leftStore.RootDirectory, "slot-01.json")));
            Assert.Equal(93, leftStore.Inspect("slot-02").Primary.Summary!.Funds); Assert.Equal(13, Directory.GetFiles(leftStore.RootDirectory).Length);
            foreach (string path in Directory.GetFiles(leftStore.RootDirectory)) Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(Path.Combine(rightStore.RootDirectory, Path.GetFileName(path))));
            return System.Text.Json.JsonSerializer.Serialize(history);
        }
        Assert.Equal(Run(Path.Combine(root, "a")), Run(Path.Combine(root, "b")));
    }
    public void Dispose()
    {
        string resolved = Path.GetFullPath(root), parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("fft-slot-ui-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected test storage.");
        if (Directory.Exists(resolved)) Directory.Delete(resolved, true); else if (File.Exists(resolved)) File.Delete(resolved);
    }
}
