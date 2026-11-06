using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record MagicEvadeEquipmentInput(bool TargetIsMonster, MagicEvadeItem Accessory,
    MagicEvadeItem RightShield, MagicEvadeItem LeftShield,
    byte RightWeaponItemId = byte.MaxValue, byte LeftWeaponItemId = byte.MaxValue);
