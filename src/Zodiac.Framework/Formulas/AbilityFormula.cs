using Zodiac.Framework.Random;

namespace Zodiac.Framework.Formulas;

/// <summary>Immutable formula selection and operands, dispatching to the supported formula families.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df. Currently admits neutral physical 0x32/0x37 only.</remarks>
public sealed record AbilityFormula
{
    public AbilityFormulaId Id { get; }
    public AbilityFormulaParameters Parameters { get; }

    public AbilityFormula(AbilityFormulaId id, AbilityFormulaParameters parameters)
    {
        if (!Enum.IsDefined(id))
        {
            throw new NotSupportedException("Unsupported ability formula.");
        }

        ArgumentNullException.ThrowIfNull(parameters);

        if (id == AbilityFormulaId.PummelFormula)
        {
            PummelDamageFormula.ValidateParameters(parameters);
        }

        Id = id;
        Parameters = parameters;
    }

    /// <summary>Dispatch after preparation/evasion; preserves formula-specific draw order and returns the next RNG state.</summary>
    /// <remarks>RNG exhaustion throws without publishing partial work; the combat-rule caller owns rejection/rollback.</remarks>
    public AbilityFormulaResult Evaluate(AbilityFormulaContext context, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(context);

        var random = new AbilityFormulaRandom(context.Mode, rng);

        return Id switch
        {
            AbilityFormulaId.RandomPa => RandomPaDamageFormula.Evaluate(Parameters, context, random),
            AbilityFormulaId.PummelFormula => PummelDamageFormula.Evaluate(Parameters, context, random),
            _ => throw new NotSupportedException("Unsupported ability formula.")
        };
    }

    /// <summary>Raw-roll arithmetic adapter for the admitted neutral physical formulas; consumes no RNG and applies no cap.</summary>
    /// <remarks>A bonus roll means the 0x32 critical check succeeded. Normal execution uses Evaluate.</remarks>
    public int CalculateDamage(int physicalAttack, uint damageRoll, uint? criticalBonusRoll = null)
    {
        if (physicalAttack is < 1 or > 99) throw new ArgumentOutOfRangeException(nameof(physicalAttack));
        if (damageRoll > 32767 || criticalBonusRoll is > 32767) throw new ArgumentOutOfRangeException(nameof(damageRoll));
        return Id switch
        {
            AbilityFormulaId.RandomPa => criticalBonusRoll is null
                ? RandomPaDamageFormula.CalculateDamage(Parameters, physicalAttack, damageRoll)
                : throw new ArgumentException("Formula0x37 has no critical step.", nameof(criticalBonusRoll)),
            AbilityFormulaId.PummelFormula => PummelDamageFormula.CalculateDamage(Parameters, physicalAttack, damageRoll, criticalBonusRoll),
            _ => throw new NotSupportedException("Unsupported ability formula.")
        };
    }
}
