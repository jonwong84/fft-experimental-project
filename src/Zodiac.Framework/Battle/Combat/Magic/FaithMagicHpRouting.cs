namespace Zodiac.Framework;

/// <summary>Elemental absorption after damage Faith, or undead reversal after healing Faith.</summary>
/// <remarks>ZOD-339; source 11464943bbdbd2a0af022d90cb7f10455cf911df. Not complete action-result finalization.</remarks>
public static class FaithMagicHpRouting
{
    public static FaithMagicHpResult Route(FaithMagicElementAmount projection, bool targetIsUndead)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (projection.Nullified) return new(FaithMagicHpRoute.Nullified,0,0,false);
        short amount = projection.Amount!.Amount;
        if (projection.Kind == FaithMagicAmountKind.Healing)
            return targetIsUndead ? new(FaithMagicHpRoute.Damage,amount,0,true) : new(FaithMagicHpRoute.Healing,0,amount,false);
        return projection.Elements!.Absorbed ? new(FaithMagicHpRoute.Healing,0,amount,false) : new(FaithMagicHpRoute.Damage,amount,0,false);
    }
}
