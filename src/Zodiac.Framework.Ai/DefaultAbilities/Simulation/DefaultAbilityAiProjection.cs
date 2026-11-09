using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record DefaultAbilityAiUnit(SpellTargetOccupant Occupant, MagicCurrentUnit Current, MagicUnitVitals Vitals)
{
    public BattleUnitId Id => Current.UnitId;
}
public enum DefaultAbilityAiProjectionRejection { None, UnsupportedContext, ResourceBlocked, RandomDrawCountExhausted }
public sealed record DefaultAbilityAiProjectionOutcome(DefaultAbilityAiUnit Caster, MagicCastOutcome Cast);
public sealed record DefaultAbilityAiProjectionResult(bool Accepted, DefaultAbilityAiProjectionRejection Rejection,
    DefaultAbilityAiProjectionOutcome? Outcome, RandomState RngState);

/// <summary>Fresh ready formula/resource work in AI mode. Target application/accounting remain separate.</summary>
public static class DefaultAbilityAiProjection
{
    internal static MagicResultFinalizationProfile ResultProfile => new(true, true, true, true, true, true, new(true, true, true));
    private static MagicResultFrame Miss => new(false, 0, MagicResultMissKind.Hit, 0, 0, new(), new());

    internal static bool Supported(DefaultAbilityAiUnit unit)
    {
        if (unit is null || unit.Current is null || unit.Vitals is null || unit.Occupant is null ||
            unit.Id != unit.Occupant.UnitId || unit.Occupant.SourceOrder < 0 || !Enum.IsDefined(unit.Occupant.InitialTeam) ||
            unit.Current.Faith > 100 || !unit.Occupant.Active || unit.Occupant.Jump || unit.Occupant.Mount ||
            unit.Occupant.Crystal || unit.Occupant.Treasure || unit.Occupant.InitialTeam != unit.Occupant.CurrentTeam ||
            unit.Current.Identity.IsMonster || unit.Current.IsMime || unit.Current.HasRider ||
            unit.Current.Equipment != DefaultAbilityBattleDefinition.BareEquipment || unit.Current.Supports != new MagicCurrentSupports() ||
            unit.Current.Strengthens != MagicElement.None || unit.Current.Affinities != default || !unit.Current.ActionUnrestricted)
            return false;
        try { _ = DefaultAbilityHitApplication.Project(unit.Current, unit.Vitals, Miss); return true; }
        catch (Exception e) when (e is ArgumentException or NotSupportedException) { return false; }
    }

    public static DefaultAbilityAiProjectionResult Calculate(DefaultAbilityAiUnit caster, MagicCastDefinition spell,
        ImmutableArray<DefaultAbilityAiUnit> targets, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(caster); ArgumentNullException.ThrowIfNull(spell);
        DefaultAbilityAiProjectionResult Reject(DefaultAbilityAiProjectionRejection reason) => new(false, reason, null, rng);
        if (!Supported(caster) || targets.IsDefaultOrEmpty || targets.Any(u => !Supported(u)) ||
            targets.Select(u => u.Id).Distinct().Count() != targets.Length ||
            targets.Select(u => u.Occupant.SourceOrder).Distinct().Count() != targets.Length ||
            !targets.SequenceEqual(targets.OrderBy(u => u.Occupant.SourceOrder)) ||
            targets.Any(u => u.Id == caster.Id && u != caster))
            return Reject(DefaultAbilityAiProjectionRejection.UnsupportedContext);
        var ready = MagicCastQuery.PrepareReady(new(caster.Current, spell, MagicDamageWeather.None,
            MagicFormulaEvaluationMode.AiSimulation, ResultProfile, new(true, true, true, true, true)));
        if (!ready.Accepted) return Reject(DefaultAbilityAiProjectionRejection.UnsupportedContext);
        if (ready.Cast is null) return Reject(DefaultAbilityAiProjectionRejection.ResourceBlocked);
        var result = MagicCastQuery.ResolveTargets(ready.Cast, targets.Select(t => t.Current).ToImmutableArray(), new(true, true), rng);
        if (!result.Accepted) return Reject(result.Rejection == FaithMagicResolutionRejection.RandomDrawCountExhausted
            ? DefaultAbilityAiProjectionRejection.RandomDrawCountExhausted : DefaultAbilityAiProjectionRejection.UnsupportedContext);
        return new(true, DefaultAbilityAiProjectionRejection.None,
            new(caster with { Current = caster.Current with { Mp = result.Outcome!.CandidateMp } }, result.Outcome), result.RngState);
    }
}
