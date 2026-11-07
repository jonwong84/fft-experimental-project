namespace Zodiac.Framework;

/// <summary>Pure certified player confirmation after blue selection; not a runtime cursor search or command.</summary>
public static class SpellAimCapture
{
    public static SpellAimCaptureResult Capture(SpellTargetChoice choice, TilePosition cursor, byte answer,
        SpellTargetOccupant? selectedOccupant, BattleUnitId? previousTargetId, SpellAimCaptureProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(choice);
        SpellAimCaptureResult Reject(SpellTargetingRejection reason) => new(false, reason, null);
        if (profile is not { SelectionContextUnchangedKnown: true, AnswerOfferedByPromptKnown: true } ||
            choice.Mode is not (SpellTargetChoiceMode.UnitOrTile or SpellTargetChoiceMode.TileOnly) || answer is not (5 or 6 or 7))
            return Reject(SpellTargetingRejection.UnsupportedContext);
        if (choice.Range is null || !choice.Range.Panels.Contains(cursor)) return Reject(SpellTargetingRejection.InvalidAim);
        byte type = answer; var id = previousTargetId;
        if (answer != 5)
        {
            if (!profile.CursorOccupantSelectionKnown) return Reject(SpellTargetingRejection.UnsupportedContext);
            if (selectedOccupant is null) type = 5;
            else
            {
                if (string.IsNullOrWhiteSpace(selectedOccupant.UnitId.Value) || !selectedOccupant.Active || selectedOccupant.Crystal ||
                    selectedOccupant.Treasure || selectedOccupant.Jump || selectedOccupant.Position != cursor)
                    return Reject(SpellTargetingRejection.UnsupportedContext);
                id = selectedOccupant.UnitId; // Includes raw YES7. Mount is not rejected by the source cursor selector.
            }
        }
        var aim = new StoredSpellAim(type, checked((short)cursor.X), checked((short)cursor.Y), checked((short)cursor.Layer), id);
        return new(true, SpellTargetingRejection.None, new(choice, aim));
    }
}
