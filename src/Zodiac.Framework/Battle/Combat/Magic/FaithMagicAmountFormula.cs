namespace Zodiac.Framework;

public enum FaithMagicAmountKind { Damage = 0x08, Healing = 0x0c }

/// <summary>Already-classified source compatibility. Sign/sex/monster classification belongs to its caller.</summary>
public enum MagicCompatibility { Neutral, Good, Best, Bad, Worst, Serpentarius }

public readonly record struct FaithMagicAmountModifiers(bool StrengthensElement = false, bool MagicAttackUp = false,
    bool MagicDefenseUp = false, bool TargetShell = false, bool TargetChickenOrFrog = false,
    MagicCompatibility Compatibility = MagicCompatibility.Neutral);

/// <summary>Explicit remaining scope fact for formula0x08's omitted between-product-and-Faith element/weather steps.</summary>
public sealed record FaithMagicAmountProfile(bool DamageHasNoWeatherOrTargetElementChanges = false);

public sealed record FaithMagicAmountInput(FaithMagicAmountKind Kind, byte MagicAttack, byte Power,
    FaithValue CasterFaith, FaithValue TargetFaith, FaithMagicAmountModifiers Modifiers = default);

/// <summary>Raw formula work, before absorption/undead routing, procs, 999 cap or HP publication.</summary>
public sealed record FaithMagicAmount(short ModifiedXa, short StoredProduct, short Amount,
    byte EffectiveCasterFaith, byte EffectiveTargetFaith);

/// <summary>Opaque raw amount work. A null amount records element cancellation before the Faith step.</summary>
public sealed record FaithMagicElementAmount
{
    public FaithMagicAmountKind Kind { get; }
    public short ModifiedXa { get; }
    public short StoredProduct { get; }
    public MagicDamageElementOutcome? Elements { get; }
    public FaithMagicAmount? Amount { get; }
    public bool Nullified => Elements?.Nullified == true;
    internal FaithMagicElementAmount(FaithMagicAmountKind kind, short xa, short product,
        MagicDamageElementOutcome? elements, FaithMagicAmount? amount)
    { Kind=kind; ModifiedXa=xa; StoredProduct=product; Elements=elements; Amount=amount; }
}

/// <summary>Shared data-parameterized 0x08/0x0C amount pipeline for an already-admitted hit.</summary>
/// <remarks>ZOD-336; source 11464943bbdbd2a0af022d90cb7f10455cf911df. Not a spell command, complete effect or AI ability entry.</remarks>
public static class FaithMagicAmountFormula
{
    public static FaithMagicAmount ProjectAfterHit(FaithMagicAmountInput input, FaithMagicAmountProfile profile)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(profile);
        Validate(input);
        if (input.Kind == FaithMagicAmountKind.Damage && !profile.DamageHasNoWeatherOrTargetElementChanges)
            throw new NotSupportedException("Damage amount requires explicit unchanged weather/target-element steps.");
        var (xa,product) = PrepareProduct(input);
        return Complete(input,xa,product,product);
    }

    /// <summary>Compose admitted damage elements before Faith; healing bypasses the entire element context.</summary>
    public static FaithMagicElementAmount ProjectWithElementsAfterHit(FaithMagicAmountInput input, MagicDamageElementContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(input); Validate(input);
        if (input.Kind == FaithMagicAmountKind.Damage)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (input.Modifiers.StrengthensElement && context.Elements == MagicElement.None)
                throw new ArgumentException("Strengthening requires a nonempty ability element set.",nameof(context));
        }
        var (xa,product) = PrepareProduct(input);
        if (input.Kind == FaithMagicAmountKind.Healing) return new(input.Kind,xa,product,null,Complete(input,xa,product,product));
        var elements = MagicDamageElements.Transform(product,context!);
        return new(input.Kind,xa,product,elements,elements.Nullified ? null : Complete(input,xa,product,elements.Amount));
    }

    private static void Validate(FaithMagicAmountInput input)
    {
        if (!Enum.IsDefined(input.Kind)) throw new ArgumentOutOfRangeException(nameof(input));
        if (!Enum.IsDefined(input.Modifiers.Compatibility)) throw new ArgumentOutOfRangeException(nameof(input));
    }

    private static FaithMagicAmount Complete(FaithMagicAmountInput input, short xa, short product, short amount) =>
        new(xa,product,FaithMagicArithmetic.ScaleStored(amount,input.CasterFaith,input.TargetFaith),input.CasterFaith.Effective,input.TargetFaith.Effective);

    private static (short Xa, short Product) PrepareProduct(FaithMagicAmountInput input)
    {
        var m = input.Modifiers; short xa = input.MagicAttack;
        if (m.StrengthensElement) xa = ScaleXa(xa,5,4);
        if (m.MagicAttackUp) xa = ScaleXa(xa,4,3);
        if (input.Kind == FaithMagicAmountKind.Damage)
        {
            if (m.MagicDefenseUp) xa = ScaleXa(xa,2,3);
            if (m.TargetShell) xa = ScaleXa(xa,2,3);
            if (m.TargetChickenOrFrog) xa = ScaleXa(xa,3,2);
        }
        xa = ApplyCompatibility(xa,m.Compatibility);
        short product = FaithMagicArithmetic.MultiplyStored(xa,input.Power);
        return (xa,product);
    }

    private static short ScaleXa(short xa, int numerator, int denominator) => unchecked((short)(xa * numerator / denominator));

    private static short ApplyCompatibility(short xa, MagicCompatibility compatibility)
    {
        if (compatibility == MagicCompatibility.Serpentarius) return xa;
        // Source keeps XA*25 in an unsigned halfword before dividing, then floors XA.
        ushort product = unchecked((ushort)(xa * 25));
        xa = compatibility switch
        {
            MagicCompatibility.Good => unchecked((short)(xa + product / 100)),
            MagicCompatibility.Best => unchecked((short)(xa + product / 50)),
            MagicCompatibility.Bad => unchecked((short)(xa - product / 100)),
            MagicCompatibility.Worst => unchecked((short)(xa - product / 50)),
            _ => xa,
        };
        return xa <= 0 ? (short)1 : xa;
    }
}
