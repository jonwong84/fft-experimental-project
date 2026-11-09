using Zodiac.Framework.Formulas;
using Zodiac.Framework.CombatRules;

namespace Zodiac.Framework;

/// <summary>Legacy arithmetic/rule adapters. Execution and AI use shared formula data and CombatRules owners.</summary>
public static class ThrowStoneFormula
{
    /// <summary>Physical front-evasion geometry; either facing toward the attacker counts on an exact diagonal.</summary>
    public static bool IsFront(long dx, long dy, BattleFacing facing)
    {
        return PhysicalDamageRules.IsFront(dx, dy, facing);
    }

    public static int Damage(int physicalAttack, uint raw)
    {
        return new AbilityFormula(AbilityFormulaId.RandomPowerTimesPa, new(0, 2)).CalculateDamage(physicalAttack, raw);
    }

    /// <summary>Two independently scaled Brave rolls; equality does not attempt a push.</summary>
    public static bool AttemptsKnockback(int attackerBrave, int targetBrave, uint attackerRaw, uint targetRaw)
    {
        return PhysicalDamageRules.AttemptsKnockback(attackerBrave, targetBrave, attackerRaw, targetRaw);
    }
}
