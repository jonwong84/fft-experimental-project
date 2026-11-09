namespace Zodiac.Framework.Formulas;

/// <summary>Immutable evaluation inputs for the currently admitted neutral physical families.</summary>
/// <remarks>Evasion and final result application belong to combat rules. Broader status/equipment/Faith inputs are not admitted yet.</remarks>
public sealed record AbilityFormulaContext
{
    public int PhysicalAttack { get; }
    public int AttackerBrave { get; }
    public int TargetBrave { get; }
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
}
