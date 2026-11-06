using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record MagicCastTargetResult(BattleUnitId TargetId, MagicResultFrame Result, MagicEvasionOutcome? Evasion,
    MagicConditionalProcOutcome? Proc);
