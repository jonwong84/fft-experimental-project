using Zodiac.Framework.Formulas;

namespace Zodiac.Framework;

/// <summary>Legacy arithmetic adapter. Executable calculations select PhysicalDamageFormula from ability data.</summary>
public static class DashFormula
{
    public static int Damage(int physicalAttack, uint raw) => new PhysicalDamageFormula(PhysicalDamageFormulaId.RandomPa, 0, 4).CalculateDamage(physicalAttack, raw);
}
