namespace Zodiac.Framework;

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
        xa = ElementalStrengthen(xa, m.StrengthensElement);
        if (input.Kind == FaithMagicAmountKind.Healing)
        {
            return MagicalXaTimesYa(xa, input.Power, m);
        }

        xa = MagicalSupportStatusCompat(xa, m);
        short product = FaithMagicArithmetic.MultiplyStored(xa,input.Power);
        return (xa,product);
    }

    /// <summary>Elemental Strengthen, formula_element_apply_strengthen: store XA *5/4 before either magic family.</summary>
    private static short ElementalStrengthen(short xa, bool strengthens) => strengthens ? ScaleXa(xa, 5, 4) : xa;

    /// <summary>Magical Support/Status/Compat, formula_apply_magical_modifiers: each helper stores its own signed16 XA.</summary>
    private static short MagicalSupportStatusCompat(short xa, FaithMagicAmountModifiers modifiers)
    {
        xa = MagicAttackUp(xa, modifiers.MagicAttackUp);
        xa = MagicDefenseUp(xa, modifiers.MagicDefenseUp);
        xa = TargetStatusAffectingXaMagical(xa, modifiers);
        return Compatibility(xa, modifiers.Compatibility);
    }

    /// <summary>Magical XA * YA, formula_calculate_magical_xa_times_ya: healing omits the target defense/status chain.</summary>
    private static (short Xa, short Product) MagicalXaTimesYa(short xa, byte ya, FaithMagicAmountModifiers modifiers)
    {
        xa = MagicAttackUp(xa, modifiers.MagicAttackUp);
        xa = Compatibility(xa, modifiers.Compatibility);
        return (xa, FaithMagicArithmetic.MultiplyStored(xa, ya));
    }

    private static short MagicAttackUp(short xa, bool enabled) => enabled ? ScaleXa(xa, 4, 3) : xa;
    private static short MagicDefenseUp(short xa, bool enabled) => enabled ? ScaleXa(xa, 2, 3) : xa;

    /// <summary>Target's Status Affecting XA (Magical): Shell first, then one Chicken-or-Frog increase.</summary>
    private static short TargetStatusAffectingXaMagical(short xa, FaithMagicAmountModifiers modifiers)
    {
        if (modifiers.TargetShell) xa = ScaleXa(xa, 2, 3);
        if (modifiers.TargetChickenOrFrog) xa = ScaleXa(xa, 3, 2);
        return xa;
    }

    private static short ScaleXa(short xa, int numerator, int denominator) => unchecked((short)(xa * numerator / denominator));

    /// <summary>Original Compatibility, formula_apply_zodiac_compatibility; the later rewrite is deliberately excluded.</summary>
    private static short Compatibility(short xa, MagicCompatibility compatibility)
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
