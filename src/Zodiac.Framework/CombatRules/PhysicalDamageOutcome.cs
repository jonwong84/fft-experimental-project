namespace Zodiac.Framework.CombatRules;

public sealed record PhysicalDamageOutcome(bool Hit, int Accuracy, int Damage, bool Critical, bool KnockbackAttempted);
