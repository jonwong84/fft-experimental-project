namespace Zodiac.Framework;

/// <summary>Closed formula 0x37, Y=2, neutral/no modifiers; decomp 11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
public static class ThrowStoneFormula
{
    /// <summary>Physical front-evasion geometry; either facing toward the attacker counts on an exact diagonal.</summary>
    public static bool IsFront(long dx, long dy, BattleFacing facing)
    {
        bool horizontal = dx > 0 && facing == BattleFacing.West || dx < 0 && facing == BattleFacing.East;
        bool vertical = dy > 0 && facing == BattleFacing.South || dy < 0 && facing == BattleFacing.North;
        return Math.Abs(dx) > Math.Abs(dy) ? horizontal : Math.Abs(dy) > Math.Abs(dx) ? vertical : horizontal || vertical;
    }

    public static int Damage(int physicalAttack, uint raw)
    {
        if (physicalAttack is < 1 or > 99) throw new ArgumentOutOfRangeException(nameof(physicalAttack));
        ValidateRaw(raw);
        return ((int)(raw * 2 / 32768) + 1) * physicalAttack;
    }

    /// <summary>Two independently scaled Brave rolls; equality does not attempt a push.</summary>
    public static bool AttemptsKnockback(int attackerBrave, int targetBrave, uint attackerRaw, uint targetRaw)
    {
        if (attackerBrave is < 10 or > 100) throw new ArgumentOutOfRangeException(nameof(attackerBrave));
        if (targetBrave is < 10 or > 100) throw new ArgumentOutOfRangeException(nameof(targetBrave));
        ValidateRaw(attackerRaw); ValidateRaw(targetRaw);
        return targetRaw * targetBrave / 32768 < attackerRaw * attackerBrave / 32768;
    }

    private static void ValidateRaw(uint raw)
    {
        if (raw > 32767) throw new ArgumentOutOfRangeException(nameof(raw));
    }
}
