using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Current-input projection for the shared magic owner, source 11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
public static class FaithMagicInputs
{
    /// <summary>Resolve a current immutable pair with the existing shared formula; no live state or RNG adoption.</summary>
    public static FaithMagicResolutionResult Resolve(FaithMagicCurrentRequest request, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Mode)) throw new ArgumentOutOfRangeException(nameof(request));
        var projected = Project(request.Caster, request.Target, request.Spell, request.Weather);
        if (!projected.Accepted) return new(false, FaithMagicResolutionRejection.UnsupportedContext, null, null, null, rng);
        var p = projected.Outcome!;
        return FaithMagicFormula.Resolve(new(new(p.Amount, p.TargetIsUndead, 100, request.Mode, request.Profile, p.Elements),
            request.Entry, request.Spell.SecondaryEvadeable, p.Equipment, p.EvadeModifiers), rng);
    }

    public static MagicInputProjectionResult Project(MagicCurrentUnit caster, MagicCurrentUnit target,
        FaithMagicSpell spell, MagicDamageWeather weather)
    {
        ValidateCurrentUnit(caster); ValidateCurrentUnit(target); ArgumentNullException.ThrowIfNull(spell);
        if (!Enum.IsDefined(spell.Kind)) throw new ArgumentOutOfRangeException(nameof(spell));
        if (spell.Kind == FaithMagicAmountKind.Damage && !Enum.IsDefined(weather)) throw new ArgumentOutOfRangeException(nameof(weather));
        if (Unsupported(caster.Statuses) || Unsupported(target.Statuses) ||
            caster.UnitId == target.UnitId && caster != target) return new(false, null);
        var t = target.Statuses; var c = caster.Statuses;
        if (spell.Kind == FaithMagicAmountKind.Damage &&
            ((spell.Elements & MagicElement.Fire) != 0 && t.Oil ||
             (spell.Elements & MagicElement.Earth) != 0 && (t.Float || target.HasRider))) return new(false, null);
        var amount = new FaithMagicAmountInput(spell.Kind, caster.MagicAttack, spell.Power,
            new(caster.Faith, c.Faith, c.Innocent), new(target.Faith, t.Faith, t.Innocent),
            new((caster.Strengthens & spell.Elements) != 0, caster.Supports.MagicAttackUp,
                target.Supports.MagicDefenseUp, t.Shell, t.Chicken || t.Frog,
                ClassifyCompatibility(caster.Identity, target.Identity)));
        if (spell.Kind == FaithMagicAmountKind.Healing) return new(true, new(amount, null, t.Undead, null, null));
        ArgumentNullException.ThrowIfNull(target.Equipment);
        if (target.Equipment.TargetIsMonster != target.Identity.IsMonster)
            throw new ArgumentException("Equipment and identity monster facts must agree.", nameof(target));
        var elements = new MagicDamageElementContext(spell.Elements, weather, target.Affinities,
            new(true, !t.Oil, !t.Float && !target.HasRider));
        var evades = new MagicEvadeModifiers(target.ActionUnrestricted && !t.DontAct, c.Transparent,
            target.Supports.Abandon, t.DontAct, t.Sleep, t.Stop, t.Confusion, t.Charging, t.Performing, t.Defending);
        return new(true, new(amount, elements, t.Undead, target.Equipment, evades));
    }

    private static bool Unsupported(MagicCurrentStatuses s) => s.Dead || s.Petrify || s.Wall;
    internal static void ValidateCurrentUnit(MagicCurrentUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit); ArgumentNullException.ThrowIfNull(unit.Statuses);
        ArgumentNullException.ThrowIfNull(unit.Supports);
        if (string.IsNullOrWhiteSpace(unit.UnitId.Value)) throw new ArgumentException("A current unit requires an ID.", nameof(unit));
        if (!Enum.IsDefined(unit.Identity.Sign)) throw new ArgumentOutOfRangeException(nameof(unit));
    }

    public static MagicCompatibility ClassifyCompatibility(MagicZodiacIdentity caster, MagicZodiacIdentity target)
    {
        if (!Enum.IsDefined(caster.Sign)) throw new ArgumentOutOfRangeException(nameof(caster));
        if (!Enum.IsDefined(target.Sign)) throw new ArgumentOutOfRangeException(nameof(target));
        if (caster.Sign == MagicZodiacSign.Serpentarius || target.Sign == MagicZodiacSign.Serpentarius)
            return MagicCompatibility.Serpentarius;
        int difference = Math.Abs((int)caster.Sign - (int)target.Sign);
        if (difference == 6)
        {
            if (caster.IsMonster || target.IsMonster) return MagicCompatibility.Bad;
            return caster.IsMale == target.IsMale && caster.IsFemale == target.IsFemale
                ? MagicCompatibility.Worst : MagicCompatibility.Best;
        }
        return difference switch
        {
            3 or 9 => MagicCompatibility.Bad,
            4 or 8 => MagicCompatibility.Good,
            _ => MagicCompatibility.Neutral
        };
    }
}
