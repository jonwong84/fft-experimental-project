using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record MagicInputProjection(FaithMagicAmountInput Amount, MagicDamageElementContext? Elements,
    bool TargetIsUndead, MagicEvadeEquipmentInput? Equipment, MagicEvadeModifiers? EvadeModifiers);
