using Zodiac.Framework.Random;

namespace Zodiac.Framework.Formulas;

/// <summary>Uncapped formula HP amounts and requested effects; does not mutate or displace battle units.</summary>
public sealed record AbilityFormulaResult(int Damage, bool Critical, bool KnockbackAttempted, RandomState RngState)
{
    public int Healing { get; init; }
    public FaithMagicElementAmount? MagicProjection { get; init; }
    internal FaithMagicHpResult? MagicHp { get; init; }
}
