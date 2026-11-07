using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record MagicConditionalProcRequest(FaithMagicAmountKind Formula, MagicProcInvocation Invocation,
    MagicFormulaEvaluationMode Mode);
