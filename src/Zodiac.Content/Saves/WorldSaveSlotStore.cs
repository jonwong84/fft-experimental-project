using System.Collections.Immutable;
using System.Security.Cryptography;
using Zodiac.Framework.Campaign;

namespace Zodiac.Content.Saves;

public enum WorldSaveFileStatus { Empty, Valid, Invalid, Unavailable }
public sealed record WorldSaveFileInfo(WorldSaveFileStatus Status, string? Revision, WorldSaveSummary? Summary,
    int? SourceSchemaVersion, string? Diagnostic);
public sealed record WorldSaveSlotInfo(string SlotId, WorldSaveFileInfo Primary, WorldSaveFileInfo Backup);

/// <summary>Ten synthetic slots. Files cannot choose paths or profiles. Locks coordinate cooperating service instances.</summary>
public sealed class WorldSaveSlotStore
{
    public static ImmutableArray<string> SlotIds { get; } = [.. Enumerable.Range(1, 10).Select(i => $"slot-{i:D2}")];
    public WorldCheckpointSaveProfile Profile { get; }
    public string RootDirectory { get; }
    private readonly WorldSavePublisher publisher;
    public WorldSaveSlotStore(WorldCheckpointSaveProfile profile, string? directory = null)
        : this(profile, directory ?? DefaultDirectory(), new WorldSavePublisher()) { }
    internal WorldSaveSlotStore(WorldCheckpointSaveProfile profile, string directory, WorldSavePublisher publisher)
    {
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(publisher);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        (Profile, RootDirectory, this.publisher) = (profile, Path.GetFullPath(directory), publisher);
        NoReparsePath(RootDirectory);
    }
    public static string DefaultDirectory()
    {
        string user = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(user)) throw new PlatformNotSupportedException("A per-user local application-data location is required.");
        return Path.Combine(user, "FFTReimplementation", "Saves", "SyntheticWorld");
    }
    public ImmutableArray<WorldSaveSlotInfo> Catalog() => [.. SlotIds.Select(Inspect)];
    public WorldSaveSlotInfo Inspect(string slot)
    {
        ValidateSlot(slot);
        try
        {
            NoReparsePath(RootDirectory);
            if (!Directory.Exists(RootDirectory))
            {
                if (File.Exists(RootDirectory)) throw new IOException("Save storage root is a file, not a directory.");
                return new(slot, Empty(), Empty());
            }
            using var guard = Lock(slot);
            return new(slot, InspectFile(FilePath(slot)), InspectFile(FilePath(slot, true)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            var unavailable = new WorldSaveFileInfo(WorldSaveFileStatus.Unavailable, null, null, null, e.Message);
            return new(slot, unavailable, unavailable);
        }
    }
    public string Save(string slot, WorldCheckpointState state, string? expectedRevision)
    {
        ValidateSlot(slot); ValidateRevision(expectedRevision);
        byte[] bytes = WorldCheckpointSave.ToUtf8(Profile, state); // No files touched on serialization failure.
        NoReparsePath(RootDirectory); Directory.CreateDirectory(RootDirectory);
        using var guard = Lock(slot);
        string destination = FilePath(slot), backup = FilePath(slot, true);
        NoReparsePath(destination); NoReparsePath(backup);
        byte[]? previous = ReadIfPresent(destination);
        if (Revision(previous) != expectedRevision) throw new WorldSaveValidationException("Slot changed; refresh before overwriting.");
        if (previous is not null) _ = WorldCheckpointSave.Read(Profile, previous); // Preserve corrupt primary for explicit recovery.
        string temporary = Path.Combine(RootDirectory, $".{slot}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var output = publisher.Create(temporary))
            {
                output.Write(bytes); publisher.Flush(output);
            }
            publisher.Promote(temporary, destination, backup, previous is not null);
            return Revision(bytes)!;
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
    public WorldCheckpointLoad Load(string slot, string expectedRevision, bool backup = false)
    {
        ValidateSlot(slot); ValidateRevision(expectedRevision);
        if (expectedRevision is null) throw new WorldSaveValidationException("Select an existing file revision before loading.");
        NoReparsePath(RootDirectory);
        if (!Directory.Exists(RootDirectory)) throw new FileNotFoundException("Save directory does not exist.");
        using var guard = Lock(slot);
        byte[] bytes = ReadIfPresent(FilePath(slot, backup)) ?? throw new FileNotFoundException("Selected save file does not exist.");
        if (Revision(bytes) != expectedRevision) throw new WorldSaveValidationException("Slot changed; refresh before loading.");
        return WorldCheckpointSave.Read(Profile, bytes); // No repair or silent backup fallback.
    }
    private FileStream Lock(string slot)
    {
        string path = Path.Combine(RootDirectory, slot + ".lock"); NoReparsePath(path);
        return new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    private string FilePath(string slot, bool backup = false) => Path.Combine(RootDirectory, slot + (backup ? ".bak" : ".json"));
    private WorldSaveFileInfo InspectFile(string path)
    {
        byte[]? bytes = null;
        try
        {
            bytes = ReadIfPresent(path);
            if (bytes is null) return Empty();
            var loaded = WorldCheckpointSave.Read(Profile, bytes);
            return new(WorldSaveFileStatus.Valid, Revision(bytes), loaded.Summary, loaded.SourceSchemaVersion, null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or WorldSaveValidationException)
        { return new(e is WorldSaveValidationException ? WorldSaveFileStatus.Invalid : WorldSaveFileStatus.Unavailable, Revision(bytes), null, null, e.Message); }
    }
    private static byte[]? ReadIfPresent(string path)
    {
        NoReparsePath(path);
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > WorldCheckpointSave.MaximumBytes) throw new WorldSaveValidationException("World save exceeds 64 KiB.");
            using var bytes = new MemoryStream(); byte[] chunk = new byte[4096];
            while (true)
            {
                int count = file.Read(chunk, 0, (int)Math.Min(chunk.Length, WorldCheckpointSave.MaximumBytes - bytes.Length + 1));
                if (count == 0) return bytes.ToArray();
                if (bytes.Length + count > WorldCheckpointSave.MaximumBytes) throw new WorldSaveValidationException("World save exceeds 64 KiB.");
                bytes.Write(chunk, 0, count);
            }
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
    private static string? Revision(byte[]? bytes) => bytes is null ? null : Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static WorldSaveFileInfo Empty() => new(WorldSaveFileStatus.Empty, null, null, null, null);
    private static void ValidateSlot(string slot)
    {
        if (!SlotIds.Contains(slot, StringComparer.Ordinal)) throw new ArgumentException("Select one of slot-01..slot-10.", nameof(slot));
    }
    private static void ValidateRevision(string? revision)
    {
        if (revision is not null && (revision.Length != 64 || revision.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f'))))
            throw new ArgumentException("Expected a lowercase SHA-256 revision or null for an absent slot.", nameof(revision));
    }
    private static void NoReparsePath(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse paths are unsupported for save storage.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
}

// A narrow internal fault boundary for actual create/flush/promotion operations; not a public filesystem contract.
internal class WorldSavePublisher
{
    internal virtual FileStream Create(string path) => new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    internal virtual void Flush(FileStream file) => file.Flush(flushToDisk: true);
    internal virtual void Promote(string temporary, string destination, string backup, bool exists)
    {
        if (exists) File.Replace(temporary, destination, backup);
        else File.Move(temporary, destination);
    }
}
