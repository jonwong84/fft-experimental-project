namespace Zodiac.Framework;

/// <summary>Raw signed result fields and a deferred status-mask operation; no caps, procs or HP changes.</summary>
public sealed record FaithMagicHpResult(FaithMagicHpRoute Route, short HpDamage, short HpHealing,
    bool ClearFirstCanceledStatusByte);
