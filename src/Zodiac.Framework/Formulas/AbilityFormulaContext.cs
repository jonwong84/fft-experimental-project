namespace Zodiac.Framework.Formulas;

/// <summary>Immutable physical or magic evaluation inputs; content operands remain in AbilityFormulaParameters.</summary>
/// <remarks>Evasion, proc/status composition and final result application belong to their combat-rule owners.</remarks>
public sealed record AbilityFormulaContext
{
    public AbilityFormulaPhysicalContext? Physical { get; }
    public int? PhysicalAttack => Physical?.PhysicalAttack;
    public int? AttackerBrave => Physical?.AttackerBrave;
    public int? TargetBrave => Physical?.TargetBrave;
    public AbilityFormulaMagicContext? Magic { get; }
    public byte? MagicalAttack => Magic?.MagicalAttack;
    public FaithValue? AttackerFaith => Magic?.AttackerFaith;
    public FaithValue? TargetFaith => Magic?.TargetFaith;
    public AbilityFormulaMode Mode { get; }

    public AbilityFormulaContext(AbilityFormulaPhysicalContext physical, AbilityFormulaMode mode)
    {
        ArgumentNullException.ThrowIfNull(physical);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        Physical = physical;
        Mode = mode;
    }

    public AbilityFormulaContext(AbilityFormulaMagicContext magic, AbilityFormulaMode mode)
    {
        ArgumentNullException.ThrowIfNull(magic);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        Magic = magic;
        Mode = mode;
    }
}
