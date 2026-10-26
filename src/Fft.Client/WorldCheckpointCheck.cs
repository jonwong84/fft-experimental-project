using System.Text.Json;
using Zodiac.Content.Samples;
using Zodiac.Content.Saves;
using Zodiac.Framework.Campaign;

namespace Fft.Client;

/// <summary>Finite headless proof in a fresh caller-selected directory; never touches real user saves.</summary>
public static class WorldCheckpointCheck
{
    public static int Run(string directory)
    {
        try
        {
            string output = Path.GetFullPath(directory);
            if (Directory.Exists(output) || File.Exists(output)) throw new IOException("World-save check requires a fresh directory.");
            Directory.CreateDirectory(output);
            var profile = SyntheticWorldCheckpoint.CreateProfile(); var original = SyntheticWorldCheckpoint.Start(profile);
            var service = new WorldCheckpointService(new(profile, Path.Combine(output, "slots")), original);
            var first = service.Save("slot-01", null); Require(first.Succeeded, first.Diagnostic);
            var selected = SyntheticWorldCheckpoint.CreateProfile();
            var resumed = new WorldCheckpointService(new(selected, service.Store.RootDirectory));
            Require(resumed.Load("slot-01", first.Revision!, true).Succeeded, "Fresh profile load failed.");
            Require(WorldCheckpointSave.ToUtf8(profile, original).SequenceEqual(WorldCheckpointSave.ToUtf8(selected, resumed.Current!)), "Restored state differs.");
            var left = WorldCheckpointSimulator.Travel(original, SyntheticWorldCheckpoint.Hill); var right = resumed.Travel(SyntheticWorldCheckpoint.Hill);
            Require(left.Accepted && right.Accepted && left.Events.SequenceEqual(right.Events) &&
                WorldCheckpointSave.ToUtf8(profile, left.State).SequenceEqual(WorldCheckpointSave.ToUtf8(selected, right.State)), "Continuation state/events differ.");
            var second = resumed.Save("slot-01", first.Revision); Require(second.Succeeded, second.Diagnostic);
            var unchanged = service.Current; Require(!service.Save("slot-01", first.Revision).Succeeded && ReferenceEquals(unchanged, service.Current), "Stale save changed the session.");
            var live = resumed.Current; Require(resumed.Load("slot-01", second.Revision!, false).Canceled && ReferenceEquals(live, resumed.Current), "Canceled load changed state.");
            string primary = Path.Combine(service.Store.RootDirectory, "slot-01.json");
            File.WriteAllText(primary, "{\"corrupt\":true}"); byte[] damaged = File.ReadAllBytes(primary);
            var catalog = resumed.Store.Catalog(); var slot = catalog.Single(s => s.SlotId == "slot-01");
            Require(slot.Primary.Status == WorldSaveFileStatus.Invalid && slot.Backup.Status == WorldSaveFileStatus.Valid, "Catalog did not distinguish damaged primary/valid backup.");
            Require(!resumed.Load("slot-01", slot.Primary.Revision!, true).Succeeded && ReferenceEquals(live, resumed.Current), "Failed load changed state.");
            var recovery = resumed.Load("slot-01", slot.Backup.Revision!, true, backup: true);
            Require(recovery.Succeeded && recovery.RecoveredFromBackup && File.ReadAllBytes(primary).SequenceEqual(damaged), "Recovery silently repaired or failed.");
            Require(WorldCheckpointSave.ToUtf8(profile, original).SequenceEqual(WorldCheckpointSave.ToUtf8(selected, resumed.Current!)), "Backup restored wrong state.");
            Require(resumed.Save("slot-02", null).Succeeded, "Recovered checkpoint could not be saved to a fresh slot.");
            File.WriteAllText(Path.Combine(output, "check.json"), JsonSerializer.Serialize(new
            {
                format = "fft.synthetic-world-save-check", schemaVersion = 1, evidence = "invented headless world checkpoint; no original campaign/card/RNG",
                saveSchema = WorldCheckpointSave.SchemaVersion, profileId = profile.Id, catalogSlots = catalog.Length,
                freshDefinitionsMatched = true, continuationStateAndEventsMatched = true, staleSaveRejected = true,
                canceledAndFailedLoadPreservedState = true, explicitValidatedBackupRecovery = true,
                damagedPrimaryPreserved = true, recoveredSavedToNewSlot = true, rngCapability = "unsupported",
                nextLocation = right.State.LocationId.Value, nextFunds = right.State.Funds, nextElapsedSeconds = right.State.ElapsedSeconds,
            }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            Console.WriteLine("World-save check passed: fresh restore/continuation, ten slots, stale protection and explicit backup recovery.");
            return 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or WorldSaveValidationException)
        { Console.Error.WriteLine(e.Message); return 1; }
    }
    private static void Require(bool condition, string? message)
    { if (!condition) throw new WorldSaveValidationException(message ?? "World checkpoint check failed."); }
}
