namespace Zodiac.Framework;

public sealed record FaithMagicAmountInput(FaithMagicAmountKind Kind, byte MagicAttack, byte Power,
    FaithValue CasterFaith, FaithValue TargetFaith, FaithMagicAmountModifiers Modifiers = default);
