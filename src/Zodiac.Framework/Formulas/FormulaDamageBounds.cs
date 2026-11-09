namespace Zodiac.Framework.Formulas;

/// <summary>Checks a calculated damage envelope against signed16 storage without clamping or truncating it.</summary>
internal static class FormulaDamageBounds
{
    public static bool FitsSigned16(long minimumDamage, long maximumDamage) =>
        minimumDamage >= short.MinValue && maximumDamage <= short.MaxValue && minimumDamage <= maximumDamage;
}
