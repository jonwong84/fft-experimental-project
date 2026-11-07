using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Source-derived PA arithmetic for Y=1 and zero equipment bonus; decomp 11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
public static class AccumulateFormula
{
    public static int IncreasePhysicalAttack(int currentPhysicalAttack)
    {
        if (currentPhysicalAttack is < 1 or > 99) throw new ArgumentOutOfRangeException(nameof(currentPhysicalAttack));
        return Math.Min(99, currentPhysicalAttack + 1);
    }
}
