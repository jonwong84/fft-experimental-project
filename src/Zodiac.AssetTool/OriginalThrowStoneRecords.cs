namespace Zodiac.AssetTool;

/// <summary>Exact selected 14-byte Throw Stone contract; decomp 11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
public static class OriginalThrowStoneRecords
{
    public static void Validate(OriginalImmediateAbilityRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Range != 4 || record.Area != 0 || record.Vertical != 0 || record.Flags1 != 1 || record.Flags2 != 17 ||
            record.Flags3 != 0 || record.Flags4 != 34 || record.Element != 0 || record.Formula != 0x37 ||
            record.X != 0 || record.Y != 2 || record.StatusInflictionId != 0 || record.ChargeTicks != 0 || record.MpCost != 0)
            throw new InvalidDataException("Unsupported Throw Stone effect, flags, targeting, status, charge or MP cost.");
    }
}
