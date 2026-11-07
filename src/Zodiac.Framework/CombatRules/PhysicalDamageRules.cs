using Zodiac.Framework.Formulas;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.CombatRules;

/// <summary>Fresh bare-human, neutral, single-target physical work; formula RNG and evasion have distinct mode contracts.</summary>
public static class PhysicalDamageRules
{
    public static PhysicalDamageCalculationResult Calculate(PhysicalAbilityDefinition ability, int physicalAttack,
        int attackerBrave, int targetBrave, int classEvade, long dx, long dy, BattleFacing targetFacing,
        PhysicalDamageMode mode, RandomState entryRng)
    {
        ArgumentNullException.ThrowIfNull(ability);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (!Enum.IsDefined(targetFacing)) throw new ArgumentOutOfRangeException(nameof(targetFacing));
        if (physicalAttack is < 1 or > 99) throw new ArgumentOutOfRangeException(nameof(physicalAttack));
        if (attackerBrave is < 10 or > 100 || targetBrave is < 10 or > 100) throw new ArgumentOutOfRangeException(nameof(attackerBrave));
        if (classEvade is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(classEvade));
        var rng = entryRng;
        uint Draw() { var d = new RandomStateGenerator().Next(rng); rng = d.State; return d.Value; }
        uint FormulaRoll() => mode == PhysicalDamageMode.Execution ? Draw() : 16384;
        try
        {
            int evade = ability.Evadeable && IsFront(dx, dy, targetFacing) ? classEvade : 0;
            int accuracy = 100 - evade;
            if (ability.Evadeable)
            {
                foreach (int chance in new[] { 0, 0, 0, evade })
                    if (Draw() * 100 / 32768 < chance && mode == PhysicalDamageMode.Execution)
                        return new(true, new(false, 0, 0, false, false), rng);
            }
            if (accuracy == 0) return new(true, new(false, 0, 0, false, false), rng);
            bool critical = false, knockback = false;
            uint? bonus = null;
            if (ability.Formula.Id == PhysicalDamageFormulaId.RandomPaPlusHalfPa)
            {
                critical = FormulaRoll() * 100 / 32768 < 4;
                if (critical) { bonus = FormulaRoll(); knockback = true; }
            }
            int damage = ability.Formula.CalculateDamage(physicalAttack, FormulaRoll(), bonus);
            if (ability.Formula.Id == PhysicalDamageFormulaId.RandomPa && mode == PhysicalDamageMode.Execution)
                knockback = AttemptsKnockback(attackerBrave, targetBrave, Draw(), Draw());
            return new(true, new(true, accuracy, Math.Min(999, damage), critical, knockback), rng);
        }
        catch (OverflowException) { return new(false, null, entryRng); }
    }

    public static bool IsFront(long dx, long dy, BattleFacing facing)
    {
        bool horizontal = dx > 0 && facing == BattleFacing.West || dx < 0 && facing == BattleFacing.East;
        bool vertical = dy > 0 && facing == BattleFacing.South || dy < 0 && facing == BattleFacing.North;
        return Math.Abs(dx) > Math.Abs(dy) ? horizontal : Math.Abs(dy) > Math.Abs(dx) ? vertical : horizontal || vertical;
    }

    public static bool AttemptsKnockback(int attackerBrave, int targetBrave, uint attackerRaw, uint targetRaw)
    {
        if (attackerBrave is < 10 or > 100 || targetBrave is < 10 or > 100) throw new ArgumentOutOfRangeException(nameof(attackerBrave));
        if (attackerRaw > 32767 || targetRaw > 32767) throw new ArgumentOutOfRangeException(nameof(attackerRaw));
        return targetRaw * targetBrave / 32768 < attackerRaw * attackerBrave / 32768;
    }
}
