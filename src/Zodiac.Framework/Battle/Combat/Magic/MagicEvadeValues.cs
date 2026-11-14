using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public readonly record struct MagicEvadeValues(byte Accessory, byte RightHand, byte LeftHand)
{
    public byte Class => 0;
}
