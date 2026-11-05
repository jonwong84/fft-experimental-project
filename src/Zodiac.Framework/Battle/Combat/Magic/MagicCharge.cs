namespace Zodiac.Framework;

public readonly record struct MagicChargeSetup(byte Ticks, bool Persevere)
{
    public bool Immediate => Ticks == 0;
}

/// <summary>Ordinary ability timing queries, source 11464943bbdbd2a0af022d90cb7f10455cf911df. No clock or action mutation.</summary>
public static class MagicCharge
{
    public static MagicChargeSetup CalculateSetup(byte rawCt, bool shortCharge, bool nonCharge, bool persevere)
    {
        int ct = rawCt & 0x7f;
        if (!persevere)
        {
            if (shortCharge) ct = (ct + 1) >> 1;
            if (nonCharge) ct = 0;
        }
        return new((byte)ct, persevere);
    }

    /// <summary>The original ordinary AI entry-loading quirk, distinct from actual action setup.</summary>
    public static byte CalculateOrdinaryAiDelay(byte rawCt, bool monsterSkill, bool defend, bool persevere)
    {
        int ct = rawCt;
        if (!persevere)
        {
            if (monsterSkill) ct >>= 1;
            else if (defend) ct = 0;
        }
        return (byte)ct;
    }
}
