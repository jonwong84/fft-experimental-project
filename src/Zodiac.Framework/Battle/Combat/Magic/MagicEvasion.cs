namespace Zodiac.Framework;

/// <summary>A resolved slot's primary-type qualification and byte fields, not a live equipment lookup.</summary>
public readonly record struct MagicEvadeItem(byte ItemId, byte MagicEvade, bool HasRequiredType);

public sealed record MagicEvadeEquipmentInput(bool TargetIsMonster, MagicEvadeItem Accessory,
    MagicEvadeItem RightShield, MagicEvadeItem LeftShield,
    byte RightWeaponItemId = byte.MaxValue, byte LeftWeaponItemId = byte.MaxValue);

public readonly record struct MagicEvadeValues(byte Accessory, byte RightHand, byte LeftHand)
{
    public byte Class => 0;
}

public readonly record struct MagicEvadeItems(byte Accessory, byte RightHand, byte LeftHand);

/// <summary>Immutable magic contributions and independent source blocking-item labels.</summary>
public sealed record MagicEvadeContributions
{
    public MagicEvadeValues Evades { get; }
    public MagicEvadeItems Items { get; }
    internal MagicEvadeContributions(MagicEvadeValues evades, MagicEvadeItems items)
    { Evades = evades; Items = items; }
}

/// <summary>Magic-only evasion rules; source 11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
public static class MagicEvasion
{
    /// <summary>Load source contributions; class is always zero, weapons affect labels rather than evade.</summary>
    public static MagicEvadeContributions Load(MagicEvadeEquipmentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        byte Contribution(MagicEvadeItem item) =>
            input.TargetIsMonster || !item.HasRequiredType ? (byte)0 : item.MagicEvade;
        return new(new(Contribution(input.Accessory), Contribution(input.RightShield), Contribution(input.LeftShield)),
            new(input.Accessory.ItemId,
                input.RightWeaponItemId == byte.MaxValue ? input.RightShield.ItemId : input.RightWeaponItemId,
                input.LeftWeaponItemId == byte.MaxValue ? input.LeftShield.ItemId : input.LeftWeaponItemId));
    }
}
