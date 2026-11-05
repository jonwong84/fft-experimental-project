namespace Zodiac.Framework;

/// <summary>Stored Faith byte plus explicit current Faith/Innocent status facts; Innocent wins.</summary>
public readonly record struct FaithValue(byte Stored, bool FaithStatus = false, bool InnocentStatus = false)
{
    public byte Effective => InnocentStatus ? (byte)0 : FaithStatus ? (byte)100 : Stored;
}

/// <summary>Stored signed-halfword steps shared by Faith-based formulas, before result finalization.</summary>
/// <remarks>ZOD-335; source 11464943bbdbd2a0af022d90cb7f10455cf911df. No state, RNG or command authority.</remarks>
public static class FaithMagicArithmetic
{
    public static short MultiplyStored(short xa, short ya) => unchecked((short)(xa * ya));

    public static short ScaleStored(short amount, FaithValue caster, FaithValue target)
    {
        // Even full-byte Faith keeps this product within signed 32 bits. Divide once,
        // toward zero, then retain the original signed-halfword store (including wrap).
        return unchecked((short)(amount * target.Effective * caster.Effective / 10000));
    }
}
