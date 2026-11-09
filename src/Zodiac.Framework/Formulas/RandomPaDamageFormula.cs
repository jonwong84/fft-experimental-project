using Zodiac.Framework.CombatRules;

namespace Zodiac.Framework.Formulas;

/// <summary>Formula0x37's admitted neutral damage and execution-only two-Brave-roll knockback request.</summary>
internal static class RandomPaDamageFormula
{
    public static AbilityFormulaResult Evaluate(AbilityFormulaParameters parameters, AbilityFormulaContext context, AbilityFormulaRandom random)
    {
        int damage = CalculateDamage(parameters, context.PhysicalAttack, random.FormulaRoll());
        bool knockback = false;
        if (context.Mode == AbilityFormulaMode.Execution)
        {
            knockback = PhysicalDamageRules.AttemptsKnockback(context.AttackerBrave, context.TargetBrave, random.Next(), random.Next());
        }
        return new(damage, false, knockback, random.State);
    }

    public static int CalculateDamage(AbilityFormulaParameters parameters, int physicalAttack, uint damageRoll) =>
        ((int)(damageRoll * parameters.Y / 32768) + 1) * physicalAttack;
}
