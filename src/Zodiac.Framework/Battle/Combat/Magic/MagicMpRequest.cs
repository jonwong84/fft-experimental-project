namespace Zodiac.Framework;

public sealed record MagicMpRequest(MagicMpActor Actor, MagicMpAbility Ability, MagicFormulaEvaluationMode Mode,
    MagicMpProfile? Profile);
