namespace Zodiac.Framework.Formulas;

/// <summary>Formula0x32 (Pummel/Repeating Fist): admitted neutral critical step followed by XA+YA and random1..X.</summary>
internal static class PummelDamageFormula
{
    public static void ValidateParameters(AbilityFormulaParameters parameters)
    {
        // Keep every admitted PA1..99 execution (including a critical) within signed16.
        // Wider operands need a separate signed-result application profile, not a silent clamp.
        const int maximumPhysicalAttack = 99;
        int maximumCriticalXa = 2 * maximumPhysicalAttack - 1;
        long maximumYa = (maximumPhysicalAttack + parameters.Y) / 2;
        long maximumDamage = (maximumCriticalXa + maximumYa) * Math.Max(1, parameters.X);
        if (!FormulaDamageBounds.FitsSigned16Range(0, maximumDamage))
        {
            throw new NotSupportedException("The selected operands exceed the neutral signed16 damage envelope.");
        }
    }

    public static AbilityFormulaResult Evaluate(AbilityFormulaParameters parameters, AbilityFormulaContext context, AbilityFormulaRandom random)
    {
        bool critical = random.FormulaRoll() * 100 / 32768 < 4;
        uint? bonus = null;

        if (critical)
        {
            bonus = random.FormulaRoll();
        }
        int damage = CalculateDamage(parameters, context.PhysicalAttack, random.FormulaRoll(), bonus);
        return new(damage, critical, critical, random.State);
    }

    public static int CalculateDamage(AbilityFormulaParameters parameters, int physicalAttack, uint damageRoll, uint? criticalBonusRoll)
    {
        int xa = physicalAttack;
        if (criticalBonusRoll is { } bonus)
        {
            xa += (int)(bonus * physicalAttack / 32768);
        }
        // YA uses original PA before the critical increase. Normal zodiac signs0..11 floor it to1;
        // Serpentarius and broader modifiers remain outside the admitted profile.
        int ya = Math.Max(1, (physicalAttack + parameters.Y) / 2);

        return (xa + ya) * ((int)(damageRoll * parameters.X / 32768) + 1);
    }
}
