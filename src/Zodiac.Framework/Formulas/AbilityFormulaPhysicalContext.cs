namespace Zodiac.Framework.Formulas;

/// <summary>Immutable combat inputs for the currently admitted neutral physical formula families.</summary>
public sealed record AbilityFormulaPhysicalContext
{
    public int PhysicalAttack { get; }
    public int AttackerBrave { get; }
    public int TargetBrave { get; }

    public AbilityFormulaPhysicalContext(int physicalAttack, int attackerBrave, int targetBrave)
    {
        if (physicalAttack is < 1 or > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalAttack));
        }
        if (attackerBrave is < 10 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(attackerBrave));
        }
        if (targetBrave is < 10 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(targetBrave));
        }

        PhysicalAttack = physicalAttack;
        AttackerBrave = attackerBrave;
        TargetBrave = targetBrave;
    }
}
