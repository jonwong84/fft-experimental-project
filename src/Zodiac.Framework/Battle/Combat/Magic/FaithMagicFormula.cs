using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>One already-admitted target's immutable formula inputs; geometry and live state belong to the caller.</summary>
public sealed record FaithMagicFormulaRequest(FaithMagicAmountInput Amount, bool TargetIsUndead,
    ushort Accuracy, MagicFormulaEvaluationMode Mode, MagicResultFinalizationProfile? Profile,
    MagicDamageElementContext? Elements = null);

/// <summary>Positive source-entry certificates; these do not perform earlier reaction or reflection work.</summary>
/// <remarks>FreshResultKnown includes zero supported work and a known-clear stored result reaction; the source initializer preserves that field.</remarks>
public sealed record FaithMagicEntryProfile(bool FreshResultKnown = false, bool NoEarlierReactionsOrReflection = false,
    bool NonzeroAbilityKnown = false);

public sealed record FaithMagicResolutionRequest(FaithMagicFormulaRequest Formula, FaithMagicEntryProfile? Entry,
    bool SecondaryEvadeable, MagicEvadeEquipmentInput? Equipment = null, MagicEvadeModifiers? EvadeModifiers = null);

public enum FaithMagicResolutionRejection { None, UnsupportedContext, RandomDrawCountExhausted }

/// <summary>Bounded formula result and separate evade diagnostics, never a live-state transition.</summary>
public sealed record FaithMagicResolutionResult(bool Accepted, FaithMagicResolutionRejection Rejection,
    MagicResultFrame? Outcome, MagicEvasionOutcome? Evasion, MagicConditionalProcOutcome? Proc, RandomState RngState);

/// <summary>Shared 0x08/0x0C calculation from explicit inputs through bounded result finalization.</summary>
/// <remarks>
/// ZOD-343 consolidation; source 11464943bbdbd2a0af022d90cb7f10455cf911df.
/// ResolveAfterHit assumes earlier admission/evasion is resolved; Resolve certifies the earlier boundary and owns magic evasion.
/// Returns result data and candidate RNG only;
/// it selects no targets and publishes no HP, status, resources, commands or live RNG.
/// </remarks>
public static class FaithMagicFormula
{
    /// <summary>Calculate from a certified fresh formula entry, including damage evasion; healing bypasses evasion.</summary>
    public static FaithMagicResolutionResult Resolve(FaithMagicResolutionRequest request, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Formula);
        var formula = request.Formula;
        if (!Enum.IsDefined(formula.Mode)) throw new ArgumentOutOfRangeException(nameof(request));
        // Validate/project immutable amount inputs privately, without RNG or publishing work on a miss.
        var projection = FaithMagicAmountFormula.ProjectWithElementsAfterHit(formula.Amount, formula.Elements);
        FaithMagicResolutionResult Reject(FaithMagicResolutionRejection reason) => new(false, reason, null, null, null, rng);
        if (request.Entry is not { FreshResultKnown: true, NoEarlierReactionsOrReflection: true, NonzeroAbilityKnown: true } ||
            formula.Accuracy != 100 || !MagicResultFinalization.Supported(formula.Profile) ||
            !formula.Profile!.EntryProcContext!.AddedEffectFlagKnownClear)
            return Reject(FaithMagicResolutionRejection.UnsupportedContext);

        MagicEvasionOutcome? evasion = null;
        var next = rng;
        ushort accuracy = formula.Accuracy;
        if (projection.Kind == FaithMagicAmountKind.Damage)
        {
            ArgumentNullException.ThrowIfNull(request.Equipment);
            ArgumentNullException.ThrowIfNull(request.EvadeModifiers);
            var prepared = MagicEvasion.Prepare(MagicEvasion.Load(request.Equipment), request.EvadeModifiers);
            var rolled = MagicEvasion.Resolve(prepared, request.SecondaryEvadeable, formula.Mode, accuracy, next);
            if (!rolled.Accepted) return Reject(FaithMagicResolutionRejection.RandomDrawCountExhausted);
            evasion = rolled.Outcome!; next = rolled.RngState; accuracy = evasion.Accuracy;
            if (!evasion.Hit)
            {
                // The formula never enters. Do not leak precomputed amount, affinity flags or proc work.
                var miss = MagicResultFinalization.Evaluate(new(false, accuracy, evasion.Miss, 0, 0, new(), new()), formula.Profile);
                return new(true, FaithMagicResolutionRejection.None, miss.Outcome, evasion, null, next);
            }
        }
        // Rounded accuracy0 can retain hit=true here, so the source tail still runs before finalization.
        var result = ResolveProjectedAfterHit(projection, formula.TargetIsUndead, accuracy, formula.Mode, formula.Profile, next);
        if (!result.Accepted)
            return Reject(result.ProcRejection == MagicProcRejection.RandomDrawCountExhausted
                ? FaithMagicResolutionRejection.RandomDrawCountExhausted : FaithMagicResolutionRejection.UnsupportedContext);
        return new(true, FaithMagicResolutionRejection.None, result.Finalization.Outcome, evasion, result.Proc, result.RngState);
    }

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
