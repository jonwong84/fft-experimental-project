using System.Collections.Immutable;
using Zodiac.Content.Saves;

namespace Fft.Client.Navigation;

public sealed record WorldMenuSlotSummary(string SlotId, WorldSaveFileStatus Primary, WorldSaveFileStatus Backup,
    WorldSaveSummary? Summary, WorldSaveSummary? BackupSummary, int? SourceSchemaVersion, int? BackupSourceSchemaVersion);
public sealed record WorldMenuSlotView(bool Saving, int Page, ImmutableArray<WorldMenuSlotSummary> Catalog,
    WorldMenuSlotSummary? Selected, bool NeedsRefresh, string Message);

public sealed partial class WorldMenuSession
{
    private readonly bool enableSaves;
    private ImmutableArray<WorldSaveSlotInfo> catalog = [];
    private WorldSaveSlotInfo? selected;
    private bool saving, needsRefresh;
    private int page;
    private WorldMenuScreen slotOrigin;
    private string slotMessage = "";
    private bool IsSlotScreen => screen is WorldMenuScreen.SlotList or WorldMenuScreen.SlotDetails or
        WorldMenuScreen.ConfirmOverwrite or WorldMenuScreen.ConfirmLoad or WorldMenuScreen.ConfirmRecovery or WorldMenuScreen.SlotResult or WorldMenuScreen.SlotError;
    private bool IsSlotConfirmation => screen is WorldMenuScreen.ConfirmOverwrite or WorldMenuScreen.ConfirmLoad or WorldMenuScreen.ConfirmRecovery;
    private static WorldMenuAction SlotAction(int index) => (WorldMenuAction)((int)WorldMenuAction.Slot01 + index);
    private static int SlotIndex(WorldMenuAction action) => (int)action - (int)WorldMenuAction.Slot01;
    private static WorldMenuSlotSummary Summary(WorldSaveSlotInfo slot) => new(slot.SlotId, slot.Primary.Status, slot.Backup.Status,
        slot.Primary.Summary, slot.Backup.Summary, slot.Primary.SourceSchemaVersion, slot.Backup.SourceSchemaVersion);
    private WorldMenuSlotView? SlotView()
    {
        if (!IsSlotScreen) return null;
        var detail = selected;
        if (screen == WorldMenuScreen.SlotList && SlotIndex(focus) is >= 0 and < 10) detail = catalog[SlotIndex(focus)];
        return new(saving, page, [.. catalog.Select(Summary)], detail is null ? null : Summary(detail), needsRefresh, slotMessage);
    }
    private bool CanSave => !needsRefresh && service.Current is not null && selected?.Primary.Status is WorldSaveFileStatus.Empty or WorldSaveFileStatus.Valid;
    private bool CanLoad => !needsRefresh && selected?.Primary.Status == WorldSaveFileStatus.Valid;
    private bool CanRecover => !needsRefresh && selected?.Backup.Status == WorldSaveFileStatus.Valid;
    private ImmutableArray<WorldMenuEntry> SlotEntries()
    {
        if (IsSlotConfirmation) return [new(WorldMenuAction.Cancel, "Cancel"), new(WorldMenuAction.Accept, screen switch
        {
            WorldMenuScreen.ConfirmOverwrite => "Overwrite saved checkpoint",
            WorldMenuScreen.ConfirmLoad => "Replace live checkpoint",
            _ => "Load validated backup",
        })];
        if (screen == WorldMenuScreen.SlotList)
        {
            var rows = Enumerable.Range(page * 5, 5).Select(i => new WorldMenuEntry(SlotAction(i),
                $"SLOT {i + 1:D2} / {catalog[i].Primary.Status.ToString().ToUpperInvariant()}"));
            return [.. rows, new(WorldMenuAction.PreviousPage, "Previous page", page > 0),
                new(WorldMenuAction.NextPage, "Next page", page == 0), new(WorldMenuAction.Refresh, "Refresh"), new(WorldMenuAction.Back, "Back")];
        }
        if (screen == WorldMenuScreen.SlotDetails) return [new(WorldMenuAction.SaveSelected, "Save here", CanSave),
            new(WorldMenuAction.LoadSelected, "Load checkpoint", CanLoad), new(WorldMenuAction.RecoverBackup, "Recover backup", CanRecover),
            new(WorldMenuAction.Refresh, "Refresh"), new(WorldMenuAction.Back, "Back to slots")];
        if (screen == WorldMenuScreen.SlotResult) return [new(WorldMenuAction.ContinueWorld, "Continue to world"), new(WorldMenuAction.Back, "Slot details")];
        return [new(WorldMenuAction.Back, "Slot details"), new(WorldMenuAction.Refresh, "Refresh")];
    }
    private WorldMenuResult ActivateSlot(WorldMenuAction action)
    {
        if (action is WorldMenuAction.OpenSave or WorldMenuAction.OpenLoad)
        {
            slotOrigin = screen; saving = action == WorldMenuAction.OpenSave; page = 0; selected = null;
            RefreshCatalog(); Show(WorldMenuScreen.SlotList, SlotAction(0)); return new(true);
        }
        if (screen == WorldMenuScreen.SlotList && SlotIndex(action) is >= 0 and < 10)
        { selected = catalog[SlotIndex(action)]; ShowDetails(); return new(true); }
        switch (action)
        {
            case WorldMenuAction.PreviousPage: page = 0; Show(WorldMenuScreen.SlotList, SlotAction(0)); break;
            case WorldMenuAction.NextPage: page = 1; Show(WorldMenuScreen.SlotList, SlotAction(5)); break;
            case WorldMenuAction.Refresh:
                bool list = screen == WorldMenuScreen.SlotList; RefreshCatalog();
                if (list) Show(WorldMenuScreen.SlotList, SlotAction(page * 5)); else ShowDetails();
                break;
            case WorldMenuAction.SaveSelected:
                if (selected!.Primary.Status == WorldSaveFileStatus.Valid) Show(WorldMenuScreen.ConfirmOverwrite, WorldMenuAction.Cancel);
                else return Perform(WorldMenuAction.SaveSelected);
                break;
            case WorldMenuAction.LoadSelected:
                if (service.Current is not null) Show(WorldMenuScreen.ConfirmLoad, WorldMenuAction.Cancel);
                else return Perform(WorldMenuAction.LoadSelected);
                break;
            case WorldMenuAction.RecoverBackup: Show(WorldMenuScreen.ConfirmRecovery, WorldMenuAction.Cancel); break;
            case WorldMenuAction.Cancel: ShowDetails(); break;
            case WorldMenuAction.Accept:
                return Perform(screen == WorldMenuScreen.ConfirmOverwrite ? WorldMenuAction.SaveSelected :
                    screen == WorldMenuScreen.ConfirmLoad ? WorldMenuAction.LoadSelected : WorldMenuAction.RecoverBackup);
            case WorldMenuAction.ContinueWorld:
                Show(WorldMenuScreen.World, saving ? WorldMenuAction.OpenSave : WorldMenuAction.OpenLoad); break;
            case WorldMenuAction.Back:
                if (screen == WorldMenuScreen.SlotList) Show(slotOrigin, saving ? WorldMenuAction.OpenSave : WorldMenuAction.OpenLoad);
                else if (screen == WorldMenuScreen.SlotDetails) Show(WorldMenuScreen.SlotList, SlotAction(WorldSaveSlotStore.SlotIds.IndexOf(selected!.SlotId)));
                else ShowDetails();
                break;
        }
        return new(true);
    }
    private void RefreshCatalog()
    {
        catalog = service.Store.Catalog(); needsRefresh = false; slotMessage = "";
        if (selected is not null) selected = catalog.Single(s => s.SlotId == selected.SlotId);
    }
    private void ShowDetails()
    {
        var preferred = saving ? WorldMenuAction.SaveSelected : CanLoad ? WorldMenuAction.LoadSelected : CanRecover ? WorldMenuAction.RecoverBackup : WorldMenuAction.Back;
        Show(WorldMenuScreen.SlotDetails, saving && !CanSave ? WorldMenuAction.Back : preferred);
    }
    private WorldMenuResult Perform(WorldMenuAction action)
    {
        var operation = action == WorldMenuAction.SaveSelected ? service.Save(selected!.SlotId, selected.Primary.Revision) :
            service.Load(selected!.SlotId, action == WorldMenuAction.LoadSelected ? selected.Primary.Revision! : selected.Backup.Revision!,
                discardConfirmed: true, backup: action == WorldMenuAction.RecoverBackup);
        if (operation.Succeeded)
        {
            slotMessage = action == WorldMenuAction.SaveSelected ? "Checkpoint saved." : action == WorldMenuAction.RecoverBackup ?
                "Backup loaded. Primary file preserved." : operation.WasMigrated ? "Checkpoint loaded and migrated." : "Checkpoint loaded.";
            notice = slotMessage;
            // Refresh only after successful publication/load; views never perform I/O.
            string message = slotMessage; RefreshCatalog(); slotMessage = message;
            Show(WorldMenuScreen.SlotResult, WorldMenuAction.ContinueWorld);
        }
        else
        {
            slotMessage = operation.Diagnostic?.StartsWith("Slot changed", StringComparison.Ordinal) == true ?
                "Slot changed. Refresh before trying again." : "Operation failed. Refresh slots and check storage access.";
            operation = operation with { Diagnostic = slotMessage }; // Do not publish filesystem paths or temporary filenames.
            needsRefresh = true; Show(WorldMenuScreen.SlotError, WorldMenuAction.Back);
        }
        return new(true, Operation: operation);
    }
}
