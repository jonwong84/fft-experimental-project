namespace Zodiac.Framework.Formulas;

/// <summary>Immutable physical or magic evaluation inputs; content operands remain in AbilityFormulaParameters.</summary>
/// <remarks>Evasion, proc/status composition and final result application belong to their combat-rule owners.</remarks>
public sealed record AbilityFormulaContext
{
    public int? PhysicalAttack { get; }
    public int? AttackerBrave { get; }
    public int? TargetBrave { get; }
    public AbilityFormulaMagicContext? Magic { get; }
    public byte? MagicalAttack => Magic?.MagicalAttack;
    public FaithValue? AttackerFaith => Magic?.AttackerFaith;
    public FaithValue? TargetFaith => Magic?.TargetFaith;
    public AbilityFormulaMode Mode { get; }

    public AbilityFormulaContext(int physicalAttack, int attackerBrave, int targetBrave, AbilityFormulaMode mode)
    {
        if (physicalAttack is < 1 or > 99) throw new ArgumentOutOfRangeException(nameof(physicalAttack));
        if (attackerBrave is < 10 or > 100) throw new ArgumentOutOfRangeException(nameof(attackerBrave));
        if (targetBrave is < 10 or > 100) throw new ArgumentOutOfRangeException(nameof(targetBrave));
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));

        PhysicalAttack = physicalAttack;
        AttackerBrave = attackerBrave;
        TargetBrave = targetBrave;
        Mode = mode;
    }

    public AbilityFormulaContext(AbilityFormulaMagicContext magic, AbilityFormulaMode mode)
    {
        ArgumentNullException.ThrowIfNull(magic);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        Magic = magic;
        Mode = mode;
    }
}
