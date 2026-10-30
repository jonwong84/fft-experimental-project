using Fft.Client.Navigation;
using Zodiac.Content.Samples;

namespace Fft.Client.Presentation;

/// <summary>Opaque authored diagnostic menu pixels. No original UI assets or state mutation.</summary>
public static class WorldMenuPresentation
{
    public const int Width = 960, Height = 640;
    public const uint Background = 0xff22150e, Panel = 0xff35281b, Ink = 0xfff4eee4,
        Muted = 0xffbaa291, Accent = 0xffe8ca52, Selected = 0xff70522d, Disabled = 0xff766858;
    public static PixelRect Button(int index) => new(32, 184 + index * 72, 300, 56);
    public static PixelRect Button(int index, WorldMenuView view) => view.Screen == WorldMenuScreen.SlotList ? new(32, 184 + index * 36, 300, 32) : Button(index);
    public static uint[] Render(WorldMenuView view)
    {
        var pixels = new uint[Width * Height]; Array.Fill(pixels, Background);
        Fill(new(32, 32, 6, 96), Accent);
        Text("FFT / WORLD CHECKPOINT", 56, 36, 3, Ink);
        Text("AUTHORED NAVIGATION / LIVE SESSION", 56, 78, 2, Accent);
        Text(Title(view.Screen), 32, 142, 2, Ink);
        Fill(new(364, 184, 564, 328), Panel);
        for (int i = 0; i < view.Entries.Length; i++)
        {
            var entry = view.Entries[i]; var bounds = Button(i, view);
            Fill(bounds, entry.Action == view.Focus ? Selected : Panel);
            if (entry.Action == view.Focus) Fill(new(bounds.X, bounds.Y, 4, bounds.Height), Accent);
            Text(entry.Label, bounds.X + 16, bounds.Y + (bounds.Height < 40 ? 9 : 20), entry.Label.Length > 23 ? 1 : 2, entry.Enabled ? Ink : Disabled);
        }
        int line = 210;
        void Detail(string value, uint color = Ink) { Text(value, 388, line, 2, color); line += 32; }
        var state = view.Checkpoint;
        if (view.Slots is { } slots)
        {
            Detail(slots.Selected?.SlotId.Replace("slot-", "SLOT ", StringComparison.Ordinal) ?? "SELECT A SLOT", Accent);
            if (view.Screen is WorldMenuScreen.ConfirmOverwrite or WorldMenuScreen.ConfirmLoad or WorldMenuScreen.ConfirmRecovery)
            {
                Detail(view.Screen switch { WorldMenuScreen.ConfirmOverwrite => "REPLACE THE SAVED CHECKPOINT",
                    WorldMenuScreen.ConfirmLoad => "REPLACE THE LIVE SESSION", _ => "LOAD THE VALIDATED BACKUP" });
                Detail("CANCEL KEEPS EVERYTHING", Accent);
                Detail(view.Screen == WorldMenuScreen.ConfirmRecovery ? "PRIMARY FILE WILL BE PRESERVED" : "ACCEPT CONFIRMS THIS OPERATION", Muted);
            }
            else if (view.Screen is WorldMenuScreen.SlotError or WorldMenuScreen.SlotResult)
            {
                foreach (string text in Wrap(slots.Message, 42)) Detail(text, view.Screen == WorldMenuScreen.SlotError ? Accent : Ink);
                Detail(view.Screen == WorldMenuScreen.SlotError ? "LIVE SESSION IS UNCHANGED" : "READY TO CONTINUE", Muted);
                if (slots.NeedsRefresh) Detail("REFRESH BEFORE TRYING AGAIN", Accent);
            }
            else if (slots.Selected is { } slot)
            {
                Detail($"PRIMARY / {slot.Primary}"); Detail($"BACKUP / {slot.Backup}", Muted);
                var summary = slot.Summary ?? slot.BackupSummary;
                if (summary is not null)
                {
                    Detail($"{(summary.LocationId.EndsWith("/harbor", StringComparison.Ordinal) ? "HARBOR" : "HILL")} / PARTY {summary.PartyCount}", Accent);
                    Detail($"FUNDS {summary.Funds} / TIME {summary.ElapsedSeconds} SEC");
                    Detail($"DATE {summary.Month}/{summary.Day} / BRIDGE {(summary.Progress ? "OPEN" : "CLOSED")}", Muted);
                    Detail($"{(slot.Summary is null ? "BACKUP" : "PRIMARY")} SCHEMA {slot.SourceSchemaVersion ?? slot.BackupSourceSchemaVersion}", Muted);
                }
                else Detail("NO VALIDATED SUMMARY", Muted);
                if (slots.NeedsRefresh) Detail("REFRESH REQUIRED", Accent);
                else if (view.Screen == WorldMenuScreen.SlotList) Detail($"PAGE {slots.Page + 1}/2", Accent);
            }
        }
        else if (view.Screen is WorldMenuScreen.ConfirmNew or WorldMenuScreen.ConfirmExit)
        {
            Detail(view.Screen == WorldMenuScreen.ConfirmNew ? "REPLACE THE LIVE CHECKPOINT" : "EXIT WITHOUT SAVING", Accent);
            line += 16;
            Detail(view.Screen == WorldMenuScreen.ConfirmNew ? "NEW STARTS AGAIN AT HARBOR" : "THIS WINDOW WILL CLOSE");
            Detail("CANCEL KEEPS YOUR LIVE SESSION");
            Detail("NO SAVE FILE WILL BE WRITTEN", Muted);
        }
        else if (state is null)
        {
            Detail("START A SMALL WORLD SESSION", Accent); line += 16;
            Detail("TWO LOCATIONS / TWO PARTY MEMBERS");
            Detail("RESUME KEEPS THE LIVE CHECKPOINT");
            Detail(view.Entries.Any(e => e.Action == WorldMenuAction.OpenLoad) ? "LOAD RESTORES A SAVED CHECKPOINT" : "SAVE AND LOAD SCREENS COME NEXT", Muted);
        }
        else
        {
            Detail(state.LocationId == SyntheticWorldCheckpoint.Harbor ? "HARBOR" : "HILL", Accent);
            Detail($"FUNDS {state.Funds} / ELAPSED {state.ElapsedSeconds} SEC");
            Detail($"DATE {state.Date.Month}/{state.Date.Day} / BRIDGE {(state.Progress ? "OPEN" : "CLOSED")}", Muted);
            if (view.Screen == WorldMenuScreen.Party)
            {
                foreach (var member in state.Party)
                {
                    string name = member.Id.Value.EndsWith("/ada", StringComparison.Ordinal) ? "ADA" : "BORIN";
                    Detail(name + " / COURIER / " + (member.EquippedItemId.HasValue ? "BLADE" : "NO WEAPON"));
                }
                int blades = state.Inventory.Where(i => i.ItemId.Value == "sample:item/training-blade").Sum(i => i.Quantity);
                int rations = state.Inventory.Where(i => i.ItemId.Value == "sample:item/rations").Sum(i => i.Quantity);
                Detail($"CARRIED / {blades} BLADES / {rations} RATIONS", Muted);
                Detail("READ ONLY PARTY VIEW", Accent);
            }
            else
            {
                line += 16;
                Detail("TRAVEL / 7 FUNDS AND 11 SECONDS");
                Detail("PARTY / ADA AND BORIN", Muted);
                Detail(view.Screen == WorldMenuScreen.Closed ? "SESSION CLOSED" : "RESUME DOES NOT LOAD A FILE", Muted);
            }
        }
        Text(view.Notice, 32, 532, 1, Accent);
        Text("ARROWS MOVE / ENTER CONFIRM / ESC BACK", 32, 564, 2, Ink);
        Text("CLICK AN ENTRY / GAMEPAD DPAD AND A B", 32, 596, 2, Muted);
        return pixels;

        void Fill(PixelRect rectangle, uint color)
        {
            for (int y = Math.Max(0, rectangle.Y); y < Math.Min(Height, rectangle.Y + rectangle.Height); y++)
                Array.Fill(pixels, color, y * Width + Math.Max(0, rectangle.X), Math.Max(0, Math.Min(Width, rectangle.X + rectangle.Width) - Math.Max(0, rectangle.X)));
        }
        void Text(string text, int x, int y, int scale, uint color)
        {
            foreach (char character in text.ToUpperInvariant())
            {
                var rows = TinyFont.Rows(character);
                for (int row = 0; row < rows.Length; row++)
                for (int col = 0; col < 5; col++)
                    if ((rows[row] & (1 << (4 - col))) != 0) Fill(new(x + col * scale, y + row * scale, scale, scale), color);
                x += 6 * scale;
            }
        }
    }
    public static string Title(WorldMenuScreen screen) => screen switch
    {
        WorldMenuScreen.Home => "HOME", WorldMenuScreen.World => "WORLD", WorldMenuScreen.Party => "PARTY",
        WorldMenuScreen.ConfirmNew => "NEW CHECKPOINT", WorldMenuScreen.ConfirmExit => "EXIT SESSION",
        WorldMenuScreen.SlotList => "SAVE AND LOAD / SLOTS", WorldMenuScreen.SlotDetails => "SLOT DETAILS",
        WorldMenuScreen.ConfirmOverwrite => "CONFIRM OVERWRITE", WorldMenuScreen.ConfirmLoad => "CONFIRM LOAD",
        WorldMenuScreen.ConfirmRecovery => "CONFIRM BACKUP RECOVERY", WorldMenuScreen.SlotResult => "OPERATION COMPLETE",
        WorldMenuScreen.SlotError => "OPERATION FAILED", _ => "CLOSED",
    };
    private static IEnumerable<string> Wrap(string message, int columns)
    {
        string line = "";
        foreach (string word in message.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + word.Length + 1 > columns) { yield return line; line = ""; }
            line = line.Length == 0 ? word : line + " " + word;
        }
        if (line.Length > 0) yield return line;
    }
}
