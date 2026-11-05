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

    /// <summary>Compatibility entry for projected work; delegates to the shared formula's ordered tail.</summary>
    public static MagicResultCompositionResult EvaluateAfterHit(FaithMagicElementAmount projection, bool targetIsUndead,
        ushort accuracy, MagicFormulaEvaluationMode mode, MagicResultFinalizationProfile? profile, RandomState rng) =>
        FaithMagicFormula.ResolveProjectedAfterHit(projection, targetIsUndead, accuracy, mode, profile, rng);

    private static short Cap(short value) => value > 999 ? (short)999 : value;
    private static MagicResultFinalizationResult Reject(MagicResultFinalizationRejection reason) => new(false,reason,null);
    internal static bool Supported(MagicResultFinalizationProfile? p) => p is
    {
        LivingTarget: true, NoPetrifyOrWall: true, NoGolem: true, EmptyStatusRecord: true,
        NoPendingKnockback: true, OnlySupportedHpResultWork: true,
        EntryProcContext: { OrdinaryPrimaryContext: true, NoPendingWeaponProcOrReaction: true }
    };
}
