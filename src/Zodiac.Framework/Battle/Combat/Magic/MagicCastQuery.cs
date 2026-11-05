using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Explicit compatible spell inputs, not a runtime original-table lookup or complete content package.</summary>
public sealed record MagicCastDefinition(AssetId Id, short OriginalAbilityId, FaithMagicSpell Spell,
    byte MpCost, byte RawChargeTime, bool AffectedBySilence, bool Persevere = false, OrdinarySpellGeometry? Targeting = null);

public sealed record MagicReadyCastProfile(bool ChargeReadyKnown = false, bool ActionStateBookkeepingKnown = false,
    bool NoHamedoKnown = false, bool SingleOrdinaryDefaultStrikeKnown = false, bool FreshResultsKnown = false);

public sealed record MagicReadyCastRequest(MagicCurrentUnit Caster, MagicCastDefinition Definition,
    MagicDamageWeather Weather, MagicFormulaEvaluationMode Mode, MagicResultFinalizationProfile? ResultProfile,
    MagicReadyCastProfile? ReadyProfile);

/// <summary>Derived immutable MP proposal for one certified ready action; no command or live-state authority.</summary>
public sealed record PreparedMagicCast
{
    public MagicCurrentUnit Caster { get; }
    public MagicCastDefinition Definition { get; }
    public MagicDamageWeather Weather { get; }
    public MagicFormulaEvaluationMode Mode { get; }
    public MagicResultFinalizationProfile ResultProfile { get; }
    public MagicMpOutcome Resource { get; }
    internal PreparedMagicCast(MagicReadyCastRequest r, MagicMpOutcome resource)
    { Caster = r.Caster; Definition = r.Definition; Weather = r.Weather; Mode = r.Mode; ResultProfile = r.ResultProfile!; Resource = resource; }
}

public sealed record MagicReadyCastResult(bool Accepted, MagicMpOutcome? Resource, PreparedMagicCast? Cast);

public sealed record MagicCastTargetProfile(bool EligibilityAndOrderKnown = false, bool NoEarlierTargetEffectsKnown = false);
public sealed record MagicCastTargetResult(BattleUnitId TargetId, MagicResultFrame Result, MagicEvasionOutcome? Evasion,
    MagicConditionalProcOutcome? Proc);
public sealed record MagicCastOutcome(ushort CandidateMp, byte RecordedCost, ImmutableArray<MagicCastTargetResult> Targets);
public sealed record MagicCastResolutionResult(bool Accepted, FaithMagicResolutionRejection Rejection,
    MagicCastOutcome? Outcome, RandomState RngState);

/// <summary>Pure scheduling-independent ready-cast work. No spell command, MP/HP/status or RNG publication.</summary>
public static class MagicCastQuery
{
    /// <summary>Resolve a certified nonempty, unique target order with one ready-cast MP proposal and private RNG.</summary>
    public static MagicCastResolutionResult ResolveTargets(PreparedMagicCast cast, ImmutableArray<MagicCurrentUnit> targets,
        MagicCastTargetProfile? profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(cast);
        MagicCastResolutionResult Reject(FaithMagicResolutionRejection reason) => new(false, reason, null, rng);
        if (profile is not { EligibilityAndOrderKnown: true, NoEarlierTargetEffectsKnown: true } || targets.IsDefaultOrEmpty)
            return Reject(FaithMagicResolutionRejection.UnsupportedContext);
        var actor = cast.Caster with { Mp = cast.Resource.CandidateMp };
        var normalized = ImmutableArray.CreateBuilder<MagicCurrentUnit>(targets.Length);
        var ids = new HashSet<BattleUnitId>();
        foreach (var original in targets)
        {
            FaithMagicInputs.ValidateCurrentUnit(original);
            if (!ids.Add(original.UnitId)) return Reject(FaithMagicResolutionRejection.UnsupportedContext);
            var target = original;
            if (target.UnitId == actor.UnitId)
            {
                if (target != cast.Caster && target != actor) return Reject(FaithMagicResolutionRejection.UnsupportedContext);
                target = actor; // The source MP proposal precedes self-target calculation too.
            }
            if (cast.Definition.Spell.Kind == FaithMagicAmountKind.Damage)
            {
                ArgumentNullException.ThrowIfNull(target.Equipment);
                if (target.Equipment.TargetIsMonster != target.Identity.IsMonster)
                    throw new ArgumentException("Equipment and identity monster facts must agree.", nameof(targets));
            }
            normalized.Add(target);
        }
        var results = ImmutableArray.CreateBuilder<MagicCastTargetResult>(targets.Length);
        var next = rng;
        foreach (var target in normalized)
        {
            var r = FaithMagicInputs.Resolve(new(actor, target, cast.Definition.Spell, cast.Weather, cast.Mode,
                cast.ResultProfile, new(true, true, true)), next);
            if (!r.Accepted) return Reject(r.Rejection);
            results.Add(new(target.UnitId, r.Outcome!, r.Evasion, r.Proc)); next = r.RngState;
        }
        return new(true, FaithMagicResolutionRejection.None,
            new(cast.Resource.CandidateMp, cast.Resource.RecordedCost, results.MoveToImmutable()), next);
    }

    public static MagicReadyCastResult PrepareReady(MagicReadyCastRequest request)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Definition);
        ArgumentNullException.ThrowIfNull(request.Definition.Spell); FaithMagicInputs.ValidateCurrentUnit(request.Caster);
        var d = request.Definition; var c = request.Caster; var s = c.Statuses;
        if (string.IsNullOrWhiteSpace(d.Id.Value)) throw new ArgumentException("A cast definition requires a stable ID.", nameof(request));
        if (!Enum.IsDefined(request.Mode) || !Enum.IsDefined(d.Spell.Kind)) throw new ArgumentOutOfRangeException(nameof(request));
        if (d.Spell.Kind == FaithMagicAmountKind.Damage && !Enum.IsDefined(request.Weather)) throw new ArgumentOutOfRangeException(nameof(request));
        if (request.ReadyProfile is not { ChargeReadyKnown: true, ActionStateBookkeepingKnown: true, NoHamedoKnown: true,
                SingleOrdinaryDefaultStrikeKnown: true, FreshResultsKnown: true } ||
            !MagicResultFinalization.Supported(request.ResultProfile) || !request.ResultProfile!.EntryProcContext!.AddedEffectFlagKnownClear ||
            d.OriginalAbilityId is < 1 or >= 368 || d.Persevere || c.IsMime ||
            s.Dead || s.Petrify || s.Wall || s.Charging || s.Performing || s.Defending ||
            !c.ActionUnrestricted && !s.DontAct)
            return new(false, null, null);
        var mp = MagicResources.EvaluateMp(new(new(c.Mp, c.Supports.HalfMp, c.IsMime, s.Frog, s.Silence,
            s.DontAct, MagicActionRestriction.None), new(d.OriginalAbilityId, d.MpCost, MagicResourceMenu.Default, d.AffectedBySilence),
            request.Mode, new(true)));
        return new(mp.Accepted, mp.Outcome, mp.Outcome is { Usable: true } ? new(request, mp.Outcome) : null);
    }
}
