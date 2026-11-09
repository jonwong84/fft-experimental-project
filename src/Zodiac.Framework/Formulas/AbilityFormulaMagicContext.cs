namespace Zodiac.Framework.Formulas;

/// <summary>Immutable magic combat inputs, separate from formula X/Y/Z and from physical-only stats.</summary>
public sealed record AbilityFormulaMagicContext(byte MagicalAttack, FaithValue AttackerFaith, FaithValue TargetFaith,
    bool TargetIsUndead, FaithMagicAmountModifiers Modifiers = default, MagicDamageElementContext? Elements = null);
