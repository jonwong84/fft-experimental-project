using Zodiac.Framework.Random;

namespace Zodiac.Framework;

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
