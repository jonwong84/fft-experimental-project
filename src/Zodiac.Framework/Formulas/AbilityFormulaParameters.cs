namespace Zodiac.Framework.Formulas;

/// <summary>Immutable content operands; Z comes from item data for the original item formulas.</summary>
public sealed record AbilityFormulaParameters
{
    public int X { get; }
    public int Y { get; }
    public int Z { get; }

    public AbilityFormulaParameters(int x, int y, int z = 0)
    {
        if (x is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(x));
        if (y is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(y));
        if (z is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(z));
        X = x;
        Y = y;
        Z = z;
    }
}
