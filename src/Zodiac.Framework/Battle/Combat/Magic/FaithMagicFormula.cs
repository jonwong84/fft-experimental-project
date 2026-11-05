using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>One already-admitted target's immutable formula inputs; geometry and live state belong to the caller.</summary>
public sealed record FaithMagicFormulaRequest(FaithMagicAmountInput Amount, bool TargetIsUndead,
    ushort Accuracy, MagicFormulaEvaluationMode Mode, MagicResultFinalizationProfile? Profile,
    MagicDamageElementContext? Elements = null);

/// <summary>Shared 0x08/0x0C calculation from explicit inputs through bounded result finalization.</summary>
/// <remarks>
/// ZOD-343 consolidation; source 11464943bbdbd2a0af022d90cb7f10455cf911df.
/// Assumes earlier admission/evasion is resolved. Returns result data and candidate RNG only;
/// it selects no targets and publishes no HP, status, resources, commands or live RNG.
/// </remarks>
public static class FaithMagicFormula
{
    public static MagicResultCompositionResult ResolveAfterHit(FaithMagicFormulaRequest request, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(request);
        var projection = FaithMagicAmountFormula.ProjectWithElementsAfterHit(request.Amount, request.Elements);
        return ResolveProjectedAfterHit(projection, request.TargetIsUndead, request.Accuracy,
            request.Mode, request.Profile, rng);
    }

    // The projection-based compatibility API enters here too: one owner for the ordered tail.
    internal static MagicResultCompositionResult ResolveProjectedAfterHit(FaithMagicElementAmount projection,
        bool targetIsUndead, ushort accuracy, MagicFormulaEvaluationMode mode,
        MagicResultFinalizationProfile? profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        // Admission is managed policy: unsupported work publishes no private roll or result.
        // Even preview needs known entry flags here, because final flags must be retained.
        if (!MagicResultFinalization.Supported(profile) ||
            (!projection.Nullified && !profile!.EntryProcContext!.AddedEffectFlagKnownClear))
            return new(new(false, MagicResultFinalizationRejection.UnsupportedContext, null), null, rng);

        var hp = FaithMagicHpRouting.Route(projection, targetIsUndead);
        var proc = MagicConditionalProc.EvaluateAfterHit(projection, mode, profile!.EntryProcContext, rng);
        if (!proc.Accepted)
            return new(new(false, MagicResultFinalizationRejection.ProcDecisionRejected, null), null, rng, proc.Rejection);

        bool nullified = projection.Nullified;
        var frame = new MagicResultFrame(!nullified, nullified ? (ushort)0 : accuracy,
            nullified ? MagicResultMissKind.Nullified : MagicResultMissKind.Hit, hp.HpDamage, hp.HpHealing,
            new(hp.Route == FaithMagicHpRoute.Damage, hp.Route == FaithMagicHpRoute.Healing),
            new(projection.Elements?.Absorbed == true, projection.Elements?.WeaknessApplied == true,
                proc.Outcome!.SetAddedEffectFlag), hp.ClearFirstCanceledStatusByte);
        // The known empty status attempt preserves hit/accuracy/miss and changes no status set.
        var result = MagicResultFinalization.Evaluate(frame, profile);
        return result.Accepted ? new(result, proc.Outcome, proc.RngState) : new(result, null, rng);
    }
}
