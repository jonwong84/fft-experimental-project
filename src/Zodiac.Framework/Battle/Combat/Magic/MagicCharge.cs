namespace Zodiac.Framework;

public readonly record struct MagicChargeSetup(byte Ticks, bool Persevere)
{
    public bool Immediate => Ticks == 0;
}

public readonly record struct MagicChargeStep(byte Counter, bool Ready, bool Changed);

/// <summary>Ordinary ability timing queries, source 11464943bbdbd2a0af022d90cb7f10455cf911df. No clock or action mutation.</summary>
public static class MagicCharge
{
    /// <summary>One source charge tick with an already-classified wait-menu block predicate; emits no event.</summary>
    public static MagicChargeStep Tick(byte counter, bool turnStatusBlocksWait)
    {
        byte before = counter;
        if (!turnStatusBlocksWait && counter is not (0 or byte.MaxValue)) counter--;
        return new(counter, !turnStatusBlocksWait && counter == 0, counter != before);
    }

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
