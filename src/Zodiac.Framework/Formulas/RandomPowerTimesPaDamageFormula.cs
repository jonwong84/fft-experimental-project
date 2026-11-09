using Zodiac.Framework.CombatRules;

namespace Zodiac.Framework.Formulas;

/// <summary>
/// Corresponds to Formula0x37, used by Dash, Throw Stone, Cat Kick, and Tail Swing.
/// Damage is calculated as Random(1,Y) * PA, where Random(1,Y) is a uniform integer distribution over the inclusive range [1,Y].
/// Also has a chance to trigger knockback.
/// </summary>
internal static class RandomPowerTimesPaDamageFormula
{
    public static AbilityFormulaResult Evaluate(AbilityFormulaParameters parameters, AbilityFormulaContext context, AbilityFormulaRandom random)
    {
        int damage = CalculateDamage(parameters, context.PhysicalAttack!.Value, random.FormulaRoll());
        bool knockbackTriggered = false;

        if (context.Mode == AbilityFormulaMode.Execution)
        {
            knockbackTriggered = PhysicalDamageRules.AttemptsKnockback(context.AttackerBrave!.Value, context.TargetBrave!.Value, random.Next(), random.Next());
        }

        return new(damage, false, knockbackTriggered, random.State);
    }

    public static int CalculateDamage(AbilityFormulaParameters parameters, int physicalAttack, uint damageRoll) =>
        ((int)(damageRoll * parameters.Y / 32768) + 1) * physicalAttack;
}
