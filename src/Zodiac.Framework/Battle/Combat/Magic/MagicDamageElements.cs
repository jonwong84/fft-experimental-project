namespace Zodiac.Framework;

/// <summary>Stored damage weather/affinity steps before Faith. No Oil/Float/rider or state publication.</summary>
/// <remarks>ZOD-337; source 11464943bbdbd2a0af022d90cb7f10455cf911df.</remarks>
public static class MagicDamageElements
{
    public static MagicDamageElementOutcome Transform(short amount, MagicDamageElementContext context)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(context.Profile);
        if (!Enum.IsDefined(context.EffectiveWeather)) throw new ArgumentOutOfRangeException(nameof(context));
        var elements = context.Elements; var p = context.Profile;
        if (!p.FreshElementWork || Has(elements,MagicElement.Fire) && !p.FireTargetHasNoOil ||
            Has(elements,MagicElement.Earth) && !p.EarthTargetHasNoFloatOrRider)
            throw new NotSupportedException("Certify fresh work and the relevant Oil/Float/rider exclusions explicitly.");
        short weather = WeatherElementalEffects(amount, context.EffectiveWeather, elements);
        return ElementalDamageModification(weather, elements, context.TargetAffinities);
    }

    /// <summary>Weather Elemental effects: signed16 stores after each matching weather/element step.</summary>
    private static short WeatherElementalEffects(short amount, MagicDamageWeather weather, MagicElement elements)
    {
        if (weather is MagicDamageWeather.Storm or MagicDamageWeather.StrongStorm)
        {
            if (Has(elements,MagicElement.Fire)) amount = Scale(amount,3,4);
            if (Has(elements,MagicElement.Lightning)) amount = Scale(amount,5,4);
        }
        if ((weather is MagicDamageWeather.Snowstorm or MagicDamageWeather.StrongSnowstorm) && Has(elements,MagicElement.Ice))
            amount = Scale(amount,5,4);
        return amount;
    }

    /// <summary>Elemental Damage Modification: absorption marker, nullification exit, then independent half and weakness stores.</summary>
    private static MagicDamageElementOutcome ElementalDamageModification(short weather, MagicElement elements, MagicElementAffinities affinities)
    {
        short amount = weather;
        bool absorbed = Has(elements,affinities.Absorb);
        // Nullification clears previously marked absorption and skips half/weak.
        if (Has(elements,affinities.Nullify)) return new(weather,0,true,false,false);
        if (Has(elements,affinities.Half)) amount = Scale(amount,1,2);
        bool weak = Has(elements,affinities.Weak);
        if (weak) amount = Scale(amount,2,1);
        return new(weather,amount,false,absorbed,weak);
    }

    private static bool Has(MagicElement a, MagicElement b) => (a & b) != MagicElement.None;
    private static short Scale(short amount, int numerator, int denominator) => unchecked((short)(amount * numerator / denominator));
}
