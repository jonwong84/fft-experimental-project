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

/// <summary>Explicit magic status facts; TargetActionUnrestricted is the already-classified source restriction.</summary>
public sealed record MagicEvadeModifiers(bool TargetActionUnrestricted, bool AttackerTransparent = false,
    bool TargetAbandon = false, bool TargetDontAct = false, bool TargetSleep = false, bool TargetStop = false,
    bool TargetConfusion = false, bool TargetCharging = false, bool TargetPerforming = false, bool TargetDefending = false);

/// <summary>Admitted magic preparation. Base hit can only be 100, 50 or 25; class evade remains zero.</summary>
public sealed record MagicEvadePrepared
{
    public byte BaseHit { get; }
    public MagicEvadeValues Evades { get; }
    public MagicEvadeItems Items { get; }
    public bool AbandonTriggered { get; }
    internal MagicEvadePrepared(byte baseHit, MagicEvadeValues evades, MagicEvadeItems items, bool abandonTriggered)
    { BaseHit = baseHit; Evades = evades; Items = items; AbandonTriggered = abandonTriggered; }
}

/// <summary>Magic-only evasion rules; source 11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
public static class MagicEvasion
{
    /// <summary>Apply the magic chain only: Transparent, Abandon, target clearing, then Defending.</summary>
    public static MagicEvadePrepared Prepare(MagicEvadeContributions contributions, MagicEvadeModifiers modifiers)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        ArgumentNullException.ThrowIfNull(modifiers);
        byte baseHit = 100;
        var evades = modifiers.AttackerTransparent ? default : contributions.Evades;
        bool abandon = modifiers.TargetActionUnrestricted && modifiers.TargetAbandon;
        if (abandon) baseHit >>= 1;
        if (modifiers.TargetDontAct || modifiers.TargetSleep || modifiers.TargetStop || modifiers.TargetConfusion ||
            modifiers.TargetCharging || modifiers.TargetPerforming) evades = default;
        if (modifiers.TargetDefending) baseHit >>= 1;
        return new(baseHit, evades, contributions.Items, abandon);
    }

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
