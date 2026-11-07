namespace Zodiac.Framework;

public readonly record struct MagicChargeSetup(byte Ticks, bool Persevere)
{
    public bool Immediate => Ticks == 0;
}
