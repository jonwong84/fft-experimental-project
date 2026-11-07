using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public enum OrdinarySpellAiEffect { None, Useful, Adverse }
public sealed record OrdinarySpellAiUsefulTarget(BattleUnitId UnitId, OrdinarySpellAiEffect Effect);

/// <summary>Plain human HP usefulness, not a hit guarantee or a formula-kind classification.</summary>
public static class OrdinarySpellAiUsefulness
{
    public static OrdinarySpellAiEffect Classify(OrdinarySpellAiConsidered ability, MagicCurrentUnit target,
        MagicUnitVitals vitals, bool enemy, bool self)
    {
        ArgumentNullException.ThrowIfNull(ability); ArgumentNullException.ThrowIfNull(target); ArgumentNullException.ThrowIfNull(vitals);
        var b = ability.Behavior; var s = target.Statuses;
        if (!b.AffectsHp || b.AffectsMp || b.AffectsStats || b.AddsStatus || b.CancelsStatus || b.Unequips ||
            b.UndeadReverse || b.RandomUse || b.TargetsMapTiles || target.Identity.IsMonster || target.IsMime ||
            target.HasRider || target.Affinities != default || s is null || s != new MagicCurrentStatuses(Dead: s.Dead, Charging: s.Charging))
            throw new NotSupportedException("This classifier admits plain human empty-status HP preferences only.");
        if (vitals.MaxHp == 0 || vitals.Hp > vitals.MaxHp || s.Dead != (vitals.Hp == 0))
            throw new ArgumentException("Coherent current HP and Dead state is required.", nameof(vitals));
        if (ability.Usage.EnemiesOnly && !enemy || ability.Usage.AlliesOnly && enemy ||
            self && ability.Geometry.CannotHitCaster || s.Dead)
            return OrdinarySpellAiEffect.None;
        if (b.TargetsEnemies)
            return enemy ? OrdinarySpellAiEffect.Useful : OrdinarySpellAiEffect.Adverse;
        if (b.TargetsAllies)
            return enemy ? OrdinarySpellAiEffect.Adverse : vitals.Hp < vitals.MaxHp / 2
                ? OrdinarySpellAiEffect.Useful : OrdinarySpellAiEffect.None;
        // With neither side preference, the source accepts an HP effect after the absolute exclusions.
        return OrdinarySpellAiEffect.Useful;
    }

    public static ImmutableArray<OrdinarySpellAiUsefulTarget> Filter(OrdinaryMagicBattleState state,
        BattleUnitId actorId, OrdinarySpellAiConsidered ability)
    {
        ArgumentNullException.ThrowIfNull(state);
        var actor = state.Units.Single(u => u.Id == actorId);
        return state.Units.OrderBy(u => u.Occupant.SourceOrder).Select(u => new OrdinarySpellAiUsefulTarget(u.Id,
            Classify(ability, u.Current, u.Vitals, u.Id != actorId && u.Occupant.InitialTeam != actor.Occupant.InitialTeam, u.Id == actorId))).ToImmutableArray();
    }
}
