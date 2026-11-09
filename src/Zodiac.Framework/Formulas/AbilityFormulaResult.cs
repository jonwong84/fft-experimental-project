using Zodiac.Framework.Random;

namespace Zodiac.Framework.Formulas;

/// <summary>Uncapped formula damage and requested effects; does not mutate or displace battle units.</summary>
public sealed record AbilityFormulaResult(int Damage, bool Critical, bool KnockbackAttempted, RandomState RngState);
