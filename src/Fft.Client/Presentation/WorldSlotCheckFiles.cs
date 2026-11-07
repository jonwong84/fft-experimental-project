using Zodiac.Content.Samples;
using Zodiac.Content.Saves;

namespace Fft.Client.Presentation;

/// <summary>Only the opt-in finite check constructs this adapter with its fresh output roots.</summary>
public sealed class WorldSlotCheckFiles(WorldSaveSlotStore live, WorldSaveSlotStore replay) : IDisposable
{
    private readonly List<FileStream> held = [];
    public void Apply(WorldSlotCheckMutation mutation)
    {
        if (mutation == WorldSlotCheckMutation.Release03) { Dispose(); return; }
        foreach (var store in new[] { live, replay })
        {
            switch (mutation)
            {
                case WorldSlotCheckMutation.ExternalOverwrite01:
                    store.Save("slot-01", SyntheticWorldCheckpoint.Start(store.Profile), store.Inspect("slot-01").Primary.Revision); break;
                case WorldSlotCheckMutation.Corrupt01: File.WriteAllText(PathFor(store, "slot-01.json"), "{\"corrupt\":true}"); break;
                case WorldSlotCheckMutation.Delete04: File.Delete(PathFor(store, "slot-04.json")); break;
                case WorldSlotCheckMutation.Hold03:
                    held.Add(new(PathFor(store, "slot-03.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)); break;
            }
        }
    }
    private static string PathFor(WorldSaveSlotStore store, string name)
    {
        string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(store.RootDirectory)) + Path.DirectorySeparatorChar;
        string target = Path.GetFullPath(Path.Combine(parent, name));
        if (!target.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) throw new IOException("Check mutation escaped its new output directory.");
        return target;
    }
    public void Dispose() { foreach (var stream in held) stream.Dispose(); held.Clear(); }
}
