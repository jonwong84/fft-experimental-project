namespace Zodiac.Framework;

/// <summary>Closed neutral formula 0x37/Y4; source 11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
public static class DashFormula
{
    public static int Damage(int physicalAttack, uint raw) => RandomPaDamageFormula.Damage(physicalAttack, 4, raw);
}

internal static class RandomPaDamageFormula
{
    internal static int Damage(int physicalAttack, int y, uint raw)
    {
        if (physicalAttack is < 1 or > 99) throw new ArgumentOutOfRangeException(nameof(physicalAttack));
        if (raw > 32767) throw new ArgumentOutOfRangeException(nameof(raw));
        return ((int)(raw * y / 32768) + 1) * physicalAttack;
    }
}
