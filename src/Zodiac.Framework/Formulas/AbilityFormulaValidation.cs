namespace Zodiac.Framework.Formulas;

/// <summary>Routes definition-time validation to each supported formula's own checks.</summary>
internal static class AbilityFormulaValidation
{
    public static void Validate(AbilityFormulaId id, AbilityFormulaParameters parameters)
    {
        if (!Enum.IsDefined(id))
        {
            throw new NotSupportedException("Unsupported ability formula.");
        }

        ArgumentNullException.ThrowIfNull(parameters);

        switch (id)
        {
            case AbilityFormulaId.RandomPowerTimesPa:
            case AbilityFormulaId.FaithMagicDamage:
            case AbilityFormulaId.FaithMagicHealing:
                // The immutable operand object already validates the current family's input range.
                break;
            case AbilityFormulaId.PummelFormula:
                PummelDamageFormula.ValidateParameters(parameters);
                break;
            default:
                throw new NotSupportedException("Unsupported ability formula.");
        }
    }

    public static void ValidateContext(AbilityFormulaId id, AbilityFormulaContext context)
    {
        bool magicFormula = id is AbilityFormulaId.FaithMagicDamage or AbilityFormulaId.FaithMagicHealing;
        bool hasInputs = magicFormula ? context.Magic is not null : context.Physical is not null;
        if (!hasInputs)
        {
            throw new ArgumentException("Select combat inputs for the assigned formula family.", nameof(context));
        }
    }
}
