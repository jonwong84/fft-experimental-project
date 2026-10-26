using Zodiac.Framework;
using Zodiac.Framework.Campaign;

namespace Zodiac.Content.Saves;

public sealed record WorldSaveOperation(bool Succeeded, bool Canceled, string? Diagnostic, string? Revision = null,
    bool WasMigrated = false, bool RecoveredFromBackup = false);

/// <summary>Application-owned live session. Only a confirmed, completely validated load replaces it.</summary>
/// <remarks>One application owner serializes session commands. Slot locks coordinate separate services.</remarks>
public sealed class WorldCheckpointService
{
    public WorldCheckpointState? Current { get; private set; }
    public WorldSaveSlotStore Store { get; }
    public WorldCheckpointService(WorldSaveSlotStore store, WorldCheckpointState? initial = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (initial is not null) _ = WorldCheckpointSave.ToUtf8(store.Profile, initial);
        (Store, Current) = (store, initial);
    }
    public WorldTravelResult Travel(AssetId destination)
    {
        var result = WorldCheckpointSimulator.Travel(Current ?? throw new InvalidOperationException("No active world session."), destination);
        if (result.Accepted) Current = result.State;
        return result;
    }
    public WorldSaveOperation Save(string slot, string? expectedRevision)
    {
        try
        {
            if (Current is null) throw new WorldSaveValidationException("No idle-world session to save.");
            return new(true, false, null, Store.Save(slot, Current, expectedRevision));
        }
        catch (Exception e) when (IsOperationFailure(e)) { return new(false, false, e.Message); }
    }
    public WorldSaveOperation Load(string slot, string expectedRevision, bool discardConfirmed, bool backup = false)
    {
        if (!discardConfirmed) return new(false, true, null);
        try
        {
            var candidate = Store.Load(slot, expectedRevision, backup);
            Current = candidate.State;
            return new(true, false, null, expectedRevision, candidate.WasMigrated, backup);
        }
        catch (Exception e) when (IsOperationFailure(e)) { return new(false, false, e.Message); }
    }
    private static bool IsOperationFailure(Exception e) => e is IOException or UnauthorizedAccessException or WorldSaveValidationException or ArgumentException;
}
