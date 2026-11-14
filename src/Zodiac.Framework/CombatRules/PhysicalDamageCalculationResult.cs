using Zodiac.Framework.Random;

namespace Zodiac.Framework.CombatRules;

public sealed record PhysicalDamageCalculationResult(bool Accepted, PhysicalDamageOutcome? Outcome, RandomState RngState);
