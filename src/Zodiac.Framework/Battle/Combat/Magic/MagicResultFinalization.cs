using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Known source labels, preserved as data; the corresponding miss behaviors are not implemented here.</summary>
public enum MagicResultMissKind : byte
{
    Hit, AccessoryEvade, RightHandEvade, LeftHandEvade, ClassEvade, Nullified, AccuracyMiss,
    ForcedFailure, Canceled, Reflected, GolemGuard, BladeGraspOrFingerGuard, BuggedReflection, Catch
}

public readonly record struct MagicResultTypes(bool HpDamage = false, bool HpHealing = false, bool PseudoStatus = false);
public readonly record struct MagicResultEffects(bool Absorption = false, bool Weakness = false, bool AddedEffect = false)
{
    public bool Any => Absorption || Weakness || AddedEffect;
}

/// <summary>Supported signed HP work and known flags, before or after bounded result finalization.</summary>
public sealed record MagicResultFrame(bool Hit, ushort Accuracy, MagicResultMissKind Miss,
    short HpDamage, short HpHealing, MagicResultTypes Types, MagicResultEffects Effects,
    bool ClearFirstCanceledStatusByte = false);

/// <summary>Positive remaining facts. EntryProcContext's clear-flag fact describes entry, not post-proc flags.</summary>
public sealed record MagicResultFinalizationProfile(bool LivingTarget = false, bool NoPetrifyOrWall = false,
    bool NoGolem = false, bool EmptyStatusRecord = false, bool NoPendingKnockback = false,
    bool OnlySupportedHpResultWork = false, MagicConditionalProcProfile? EntryProcContext = null);

public enum MagicResultFinalizationRejection { None, UnsupportedContext, ProcDecisionRejected }
public sealed record MagicResultFinalizationResult(bool Accepted, MagicResultFinalizationRejection Rejection,
    MagicResultFrame? Outcome);
public sealed record MagicResultCompositionResult(MagicResultFinalizationResult Finalization,
    MagicConditionalProcOutcome? Proc, RandomState RngState, MagicProcRejection ProcRejection = MagicProcRejection.None)
{
    public bool Accepted => Finalization.Accepted;
}

/// <summary>Bounded raw HP result finalization, then source special-effect to pseudo-status typing.</summary>
/// <remarks>ZOD-341; source 11464943bbdbd2a0af022d90cb7f10455cf911df. No HP, status, command or live RNG publication.</remarks>
public static class MagicResultFinalization
{
    public static MagicResultFinalizationResult Evaluate(MagicResultFrame frame, MagicResultFinalizationProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!Enum.IsDefined(frame.Miss)) throw new ArgumentOutOfRangeException(nameof(frame));
        if (!Supported(profile)) return Reject(MagicResultFinalizationRejection.UnsupportedContext);
        return new(true,MagicResultFinalizationRejection.None,frame with
        {
            Hit = frame.Hit && frame.Accuracy != 0,
            HpDamage = Cap(frame.HpDamage), HpHealing = Cap(frame.HpHealing),
            Types = frame.Types with { PseudoStatus = frame.Types.PseudoStatus || frame.Effects.Any },
        });
    }

    /// <summary>Privately compose routing, the proc decision, empty-status preservation and finalization from an admitted projection.</summary>
    public static MagicResultCompositionResult EvaluateAfterHit(FaithMagicElementAmount projection, bool targetIsUndead,
        ushort accuracy, MagicFormulaEvaluationMode mode, MagicResultFinalizationProfile? profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        // Admission is managed policy: unsupported work publishes no private roll or result.
        // Even preview needs known entry flags here, because final flags must be retained.
        if (!Supported(profile) || (!projection.Nullified && !profile!.EntryProcContext!.AddedEffectFlagKnownClear))
            return new(Reject(MagicResultFinalizationRejection.UnsupportedContext),null,rng);
        var hp = FaithMagicHpRouting.Route(projection,targetIsUndead);
        var proc = MagicConditionalProc.EvaluateAfterHit(projection,mode,profile!.EntryProcContext,rng);
        if (!proc.Accepted) return new(Reject(MagicResultFinalizationRejection.ProcDecisionRejected),null,rng,proc.Rejection);
        bool nullified = projection.Nullified;
        var frame = new MagicResultFrame(!nullified,nullified ? (ushort)0 : accuracy,
            nullified ? MagicResultMissKind.Nullified : MagicResultMissKind.Hit,hp.HpDamage,hp.HpHealing,
            new(hp.Route == FaithMagicHpRoute.Damage,hp.Route == FaithMagicHpRoute.Healing),
            new(projection.Elements?.Absorbed == true,projection.Elements?.WeaknessApplied == true,proc.Outcome!.SetAddedEffectFlag),
            hp.ClearFirstCanceledStatusByte);
        // The known empty status attempt preserves hit/accuracy/miss and changes no status set.
        var result = Evaluate(frame,profile);
        return result.Accepted ? new(result,proc.Outcome,proc.RngState) : new(result,null,rng);
    }

    private static short Cap(short value) => value > 999 ? (short)999 : value;
    private static MagicResultFinalizationResult Reject(MagicResultFinalizationRejection reason) => new(false,reason,null);
    private static bool Supported(MagicResultFinalizationProfile? p) => p is
    {
        LivingTarget: true, NoPetrifyOrWall: true, NoGolem: true, EmptyStatusRecord: true,
        NoPendingKnockback: true, OnlySupportedHpResultWork: true,
        EntryProcContext: { OrdinaryPrimaryContext: true, NoPendingWeaponProcOrReaction: true }
    };
}
