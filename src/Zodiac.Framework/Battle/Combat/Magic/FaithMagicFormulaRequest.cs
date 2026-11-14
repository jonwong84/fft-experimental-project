using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>One already-admitted target's immutable formula inputs; geometry and live state belong to the caller.</summary>
public sealed record FaithMagicFormulaRequest(FaithMagicAmountInput Amount, bool TargetIsUndead,
    ushort Accuracy, MagicFormulaEvaluationMode Mode, MagicResultFinalizationProfile? Profile,
    MagicDamageElementContext? Elements = null);
