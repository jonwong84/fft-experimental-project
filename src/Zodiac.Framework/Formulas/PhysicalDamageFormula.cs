namespace Zodiac.Framework.Formulas;

/// <summary>Immutable selection and X/Y operands for the admitted neutral physical formula families.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df. No supports, status, equipment or zodiac modifiers.</remarks>
public sealed record PhysicalDamageFormula
{
    public PhysicalDamageFormulaId Id { get; }
    public int X { get; }
    public int Y { get; }

    public PhysicalDamageFormula(PhysicalDamageFormulaId id, int x, int y)
    {
        if (!Enum.IsDefined(id)) throw new NotSupportedException("Unsupported physical damage formula.");
        if (x is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(x));
        if (y is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(y));
        // Keep every admitted PA1..99 execution (including a critical) within signed16.
        // Wider operands need a separate signed-result application profile, not a silent clamp.
        if (id == PhysicalDamageFormulaId.RandomPaPlusHalfPa &&
            (197 + (99 + y) / 2) * Math.Max(1, x) > short.MaxValue)
            throw new NotSupportedException("The selected operands exceed the neutral signed16 damage envelope.");
        Id = id; X = x; Y = y;
    }

    /// <summary>Raw signed16-safe arithmetic; final caps belong to combat rules. A bonus roll means the 0x32 critical check succeeded.</summary>
    public int CalculateDamage(int physicalAttack, uint damageRoll, uint? criticalBonusRoll = null)
    {
        if (physicalAttack is < 1 or > 99) throw new ArgumentOutOfRangeException(nameof(physicalAttack));
        if (damageRoll > 32767 || criticalBonusRoll is > 32767) throw new ArgumentOutOfRangeException(nameof(damageRoll));
        if (Id == PhysicalDamageFormulaId.RandomPa)
        {
            if (criticalBonusRoll is not null) throw new ArgumentException("Formula0x37 has no critical step.", nameof(criticalBonusRoll));
            return ((int)(damageRoll * Y / 32768) + 1) * physicalAttack;
        }
        int xa = physicalAttack;
        if (criticalBonusRoll is { } bonus) xa += (int)(bonus * physicalAttack / 32768);
        int ya = (physicalAttack + Y) / 2; // YA uses original PA, before the critical's XA increase.
        return (xa + ya) * ((int)(damageRoll * X / 32768) + 1);
    }
}
