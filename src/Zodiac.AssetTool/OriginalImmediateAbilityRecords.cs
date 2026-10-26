namespace Zodiac.AssetTool;

public sealed record OriginalImmediateAbilityRecord(byte Range, byte Area, byte Vertical,
    byte Flags1, byte Flags2, byte Flags3, byte Flags4, byte Element, byte Formula,
    byte X, byte Y, byte StatusInflictionId, byte ChargeTicks, byte MpCost);

/// <summary>ability_secondary_data_t at decomp 11464943bbdbd2a0af022d90cb7f10455cf911df. No execution or file access.</summary>
public static class OriginalImmediateAbilityRecords
{
    public static OriginalImmediateAbilityRecord Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 14) throw new InvalidDataException("An ordinary ability secondary record requires exactly 14 bytes.");
        return new(bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5], bytes[6],
            bytes[7], bytes[8], bytes[9], bytes[10], bytes[11], bytes[12], bytes[13]);
    }

    public static void ValidateAccumulate(OriginalImmediateAbilityRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Range != 0 || record.Area != 0 || record.Vertical != 0 || record.Flags1 != 2 || record.Flags2 != 16 ||
            record.Flags3 != 0 || record.Flags4 != 0 || record.Element != 0 || record.Formula != 0x36 ||
            record.X != 0 || record.Y != 1 || record.StatusInflictionId != 0 || record.ChargeTicks != 0 || record.MpCost != 0)
            throw new InvalidDataException("Unsupported Accumulate effect, parameter, targeting, flags, status, charge or MP cost.");
    }
}
