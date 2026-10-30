using System.Collections.Immutable;
using Zodiac.Content.Samples;
using Zodiac.Content.Saves;
using Zodiac.Framework.Campaign;
using System.Text.Json.Serialization;

namespace Fft.Client.Navigation;

public enum WorldMenuScreen { Home, World, Party, ConfirmNew, ConfirmExit, Closed,
    SlotList, SlotDetails, ConfirmOverwrite, ConfirmLoad, ConfirmRecovery, SlotResult, SlotError }
public enum WorldMenuAction { New, Resume, Exit, Travel, Party, Home, Back, Cancel, Accept,
    OpenSave, OpenLoad, SaveSelected, LoadSelected, RecoverBackup, Refresh, PreviousPage, NextPage, ContinueWorld,
    Slot01, Slot02, Slot03, Slot04, Slot05, Slot06, Slot07, Slot08, Slot09, Slot10 }
public enum WorldMenuInput { Up, Down, Confirm, Back }
public sealed record WorldMenuEntry(WorldMenuAction Action, string Label, bool Enabled = true);
public sealed record WorldMenuView(WorldMenuScreen Screen, ImmutableArray<WorldMenuEntry> Entries,
    WorldMenuAction? Focus, WorldCheckpointState? Checkpoint, string Notice,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WorldMenuSlotView? Slots = null);
public sealed record WorldMenuResult(bool Handled, WorldTravelResult? Travel = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WorldSaveOperation? Operation = null);

/// <summary>Authored application navigation; explicit slot commands delegate storage to the checkpoint service.</summary>
/// <remarks>One application owner submits logical actions. This is not the original title/menu flow.</remarks>
public sealed partial class WorldMenuSession
{
    private WorldCheckpointService service;
    private readonly WorldCheckpointState newCheckpoint;
    private WorldMenuScreen screen = WorldMenuScreen.Home;
    private WorldMenuAction focus = WorldMenuAction.New;
    private WorldMenuScreen exitOrigin;
    private WorldMenuAction exitFocus;
    private string notice = "Authored world checkpoint. Resume uses this live session.";

    public WorldMenuSession(WorldSaveSlotStore store, WorldCheckpointState? initial = null, bool enableSaves = false)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (store.Profile.Id != "sample:profile/world-checkpoint" || store.Profile.Version != 1 || store.Profile.ContentVersion != 1)
            throw new ArgumentException("The world menu supports only the authored checkpoint profile.", nameof(store));
        newCheckpoint = SyntheticWorldCheckpoint.Start(store.Profile);
        service = new(store, initial);
        this.enableSaves = enableSaves;
    }

    public WorldMenuView View => new(screen, Entries(), screen == WorldMenuScreen.Closed ? null : focus, service.Current, notice, SlotView());

    public WorldMenuResult Handle(WorldMenuInput input)
    {
        if (!Enum.IsDefined(input)) return new(false);
        if (screen == WorldMenuScreen.Closed) return new(false);
        if (input == WorldMenuInput.Confirm) return Activate(focus);
        if (input == WorldMenuInput.Back)
        {
            if (IsSlotScreen) return Activate(IsSlotConfirmation ? WorldMenuAction.Cancel : WorldMenuAction.Back);
            return screen switch
            {
                WorldMenuScreen.Home => RequestExit(),
                WorldMenuScreen.World => Activate(WorldMenuAction.Home),
                WorldMenuScreen.Party => Activate(WorldMenuAction.Back),
                _ => Activate(WorldMenuAction.Cancel),
            };
        }
        var enabled = Entries().Where(e => e.Enabled).Select(e => e.Action).ToImmutableArray();
        int index = enabled.IndexOf(focus), direction = input == WorldMenuInput.Up ? -1 : 1;
        focus = enabled[(index + direction + enabled.Length) % enabled.Length];
        return new(true);
    }

    /// <summary>Stable-action activation for future pointer bindings; validates the currently visible screen.</summary>
    public WorldMenuResult Activate(WorldMenuAction action)
    {
        if (!Entries().Any(e => e.Action == action && e.Enabled)) return new(false);
        focus = action;
        if (IsSlotScreen || action is WorldMenuAction.OpenSave or WorldMenuAction.OpenLoad) return ActivateSlot(action);
        switch (action)
        {
            case WorldMenuAction.New:
                if (service.Current is null) StartNew();
                else Show(WorldMenuScreen.ConfirmNew, WorldMenuAction.Cancel);
                break;
            case WorldMenuAction.Resume: Show(WorldMenuScreen.World, WorldMenuAction.Travel); break;
            case WorldMenuAction.Exit: return RequestExit();
            case WorldMenuAction.Travel:
                var destination = service.Current!.LocationId == SyntheticWorldCheckpoint.Harbor
                    ? SyntheticWorldCheckpoint.Hill : SyntheticWorldCheckpoint.Harbor;
                var result = service.Travel(destination);
                notice = result.Accepted ? "Travel completed: 7 funds, 11 seconds." : $"Travel rejected: {result.Rejection}.";
                return new(true, result);
            case WorldMenuAction.Party: Show(WorldMenuScreen.Party, WorldMenuAction.Back); break;
            case WorldMenuAction.Home: Show(WorldMenuScreen.Home, WorldMenuAction.Resume); break;
            case WorldMenuAction.Back: Show(WorldMenuScreen.World, WorldMenuAction.Party); break;
            case WorldMenuAction.Cancel:
                if (screen == WorldMenuScreen.ConfirmNew) Show(WorldMenuScreen.Home, WorldMenuAction.New);
                else Show(exitOrigin, exitFocus);
                break;
            case WorldMenuAction.Accept:
                if (screen == WorldMenuScreen.ConfirmNew) StartNew();
                else screen = WorldMenuScreen.Closed;
                break;
        }
        return new(true);
    }

    /// <summary>A host close request is a modal decision; it never auto-saves or discards the live checkpoint.</summary>
    public WorldMenuResult RequestExit()
    {
        if (screen is not (WorldMenuScreen.Home or WorldMenuScreen.World or WorldMenuScreen.Party or
            WorldMenuScreen.SlotList or WorldMenuScreen.SlotDetails or WorldMenuScreen.SlotResult or WorldMenuScreen.SlotError)) return new(false);
        (exitOrigin, exitFocus) = (screen, focus);
        Show(WorldMenuScreen.ConfirmExit, WorldMenuAction.Cancel);
        return new(true);
    }

    private void StartNew()
    {
        // Build a replacement service before publishing it; the same selected profile/store remains bound.
        var candidate = new WorldCheckpointService(service.Store, newCheckpoint);
        service = candidate;
        notice = "New authored checkpoint at Harbor.";
        Show(WorldMenuScreen.World, WorldMenuAction.Travel);
    }
    private void Show(WorldMenuScreen target, WorldMenuAction selected) => (screen, focus) = (target, selected);
    private ImmutableArray<WorldMenuEntry> Entries()
    {
        if (IsSlotScreen) return SlotEntries();
        var entries = NavigationEntries();
        if (enableSaves && screen == WorldMenuScreen.Home) return entries.Add(new(WorldMenuAction.OpenLoad, "Load checkpoint"));
        if (enableSaves && screen == WorldMenuScreen.World) return entries.Add(new(WorldMenuAction.OpenSave, "Save checkpoint")).Add(new(WorldMenuAction.OpenLoad, "Load checkpoint"));
        return entries;
    }
    private ImmutableArray<WorldMenuEntry> NavigationEntries() => screen switch
    {
        WorldMenuScreen.Home => [new(WorldMenuAction.New, "New checkpoint"),
            new(WorldMenuAction.Resume, "Resume session", service.Current is not null), new(WorldMenuAction.Exit, "Exit")],
        WorldMenuScreen.World => [new(WorldMenuAction.Travel, service.Current!.LocationId == SyntheticWorldCheckpoint.Harbor ? "Travel to Hill" : "Travel to Harbor"),
            new(WorldMenuAction.Party, "View party"), new(WorldMenuAction.Home, "Home")],
        WorldMenuScreen.Party => [new(WorldMenuAction.Back, "Back to world")],
        WorldMenuScreen.ConfirmNew or WorldMenuScreen.ConfirmExit => [new(WorldMenuAction.Cancel, "Cancel"), new(WorldMenuAction.Accept,
            screen == WorldMenuScreen.ConfirmNew ? "Replace live checkpoint" : "Exit without saving")],
        _ => [],
    };
}
