namespace Zodiac.Framework;

/// <summary>Explicit compatible spell inputs, not a runtime original-table lookup or complete content package.</summary>
public sealed record MagicCastDefinition(AssetId Id, short OriginalAbilityId, FaithMagicSpell Spell,
    byte MpCost, byte RawChargeTime, bool AffectedBySilence, bool Persevere = false);

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

/// <summary>Pure scheduling-independent ready-cast work. No spell command, MP/HP/status or RNG publication.</summary>
public static class MagicCastQuery
{
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
