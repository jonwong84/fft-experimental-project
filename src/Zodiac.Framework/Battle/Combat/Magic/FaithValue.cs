namespace Zodiac.Framework;

/// <summary>Stored Faith byte plus explicit current Faith/Innocent status facts; Innocent wins.</summary>
public readonly record struct FaithValue(byte Stored, bool FaithStatus = false, bool InnocentStatus = false)
{
    public byte Effective => InnocentStatus ? (byte)0 : FaithStatus ? (byte)100 : Stored;
}
