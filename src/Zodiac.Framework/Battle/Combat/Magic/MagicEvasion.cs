using Zodiac.Framework.Random;

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

public enum MagicEvasionRejection { None, RandomDrawCountExhausted }

/// <summary>Fresh result facts plus an Abandon diagnostic; ItemLost is a source blocking label, not an equipment mutation.</summary>
public sealed record MagicEvasionOutcome(bool Hit, ushort Accuracy, MagicResultMissKind Miss,
    byte ItemLost, bool AbandonTriggered);

public sealed record MagicEvasionResult(bool Accepted, MagicEvasionRejection Rejection,
    MagicEvasionOutcome? Outcome, RandomState RngState);

/// <summary>Magic-only evasion rules; source 11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
public static class MagicEvasion
{
    /// <summary>Resolve ordered magic evade checks from fresh hit=true/miss=0/item=0 work and supplied accuracy.</summary>
    public static MagicEvasionResult Resolve(MagicEvadePrepared prepared, bool secondaryEvadeable,
        MagicFormulaEvaluationMode mode, ushort initialAccuracy, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        bool hit = true;
        ushort accuracy = initialAccuracy;
        var miss = MagicResultMissKind.Hit;
        byte itemLost = 0;
        var next = rng;
        if (secondaryEvadeable)
        {
            int baseHit = prepared.BaseHit;
            var values = prepared.Evades;
            if (values.Accessory >= baseHit || values.RightHand >= baseHit || values.LeftHand >= baseHit)
            { hit = false; accuracy = 0; }
            else
            {
                int Factor(byte evade) => (baseHit - evade) * 100 / baseHit;
                int product = Factor(values.Accessory) * Factor(values.RightHand) * Factor(values.LeftHand) * 100;
                accuracy = (ushort)((product / 1_000_000) * initialAccuracy / 100);
            }
            ReadOnlySpan<byte> evades = [values.Accessory, values.RightHand, values.LeftHand, values.Class];
            ReadOnlySpan<byte> items = [prepared.Items.Accessory, prepared.Items.RightHand, prepared.Items.LeftHand, byte.MaxValue];
            var generator = new RandomStateGenerator();
            try
            {
                for (int index = 0; index < evades.Length; index++)
                {
                    var draw = generator.Next(next); next = draw.State;
                    // The real draw precedes the execution gate, including zero/certain evades.
                    if (draw.Value * baseHit / 32768 < evades[index] && mode == MagicFormulaEvaluationMode.Execute)
                    {
                        hit = false; miss = (MagicResultMissKind)(index + 1); itemLost = items[index];
                        break;
                    }
                }
            }
            catch (OverflowException) { return new(false, MagicEvasionRejection.RandomDrawCountExhausted, null, rng); }
        }
        return new(true, MagicEvasionRejection.None, new(hit, accuracy, miss, itemLost, prepared.AbandonTriggered), next);
    }

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
