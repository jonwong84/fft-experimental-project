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
            case AbilityFormulaId.RandomPa:
                // The immutable operand object already validates the current family's input range.
                break;
            case AbilityFormulaId.PummelFormula:
                PummelDamageFormula.ValidateParameters(parameters);
                break;
            default:
                throw new NotSupportedException("Unsupported ability formula.");
        }
    }
}
