using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public enum MagicFormulaEvaluationMode { Execute, AiSimulation, Preview }
public enum MagicProcInvocation { FormulaMissed, ElementNullified, FamilyTail }
public enum MagicProcDisposition { FormulaMissed, ElementNullified, NoConditionalBranch, Passed, Failed }
public enum MagicProcRejection { None, UnsupportedContext, RandomDrawCountExhausted }

public sealed record MagicConditionalProcRequest(FaithMagicAmountKind Formula, MagicProcInvocation Invocation,
    MagicFormulaEvaluationMode Mode);

/// <summary>Remaining caller/work facts. False is uncertified; execution/AI require a known-clear added flag.</summary>
public sealed record MagicConditionalProcProfile(bool OrdinaryPrimaryContext = false,
    bool NoPendingWeaponProcOrReaction = false, bool AddedEffectFlagKnownClear = false);

/// <summary>Status-call decision and a flag-set delta, not a final flag value or applied status.</summary>
public sealed record MagicConditionalProcOutcome(MagicProcDisposition Disposition, bool SetAddedEffectFlag,
    uint? Raw = null, int? Percent = null)
{
    public bool Invoked => Disposition is MagicProcDisposition.Passed or MagicProcDisposition.Failed;
    public bool ShouldApplyStatus => Disposition == MagicProcDisposition.Passed;
}

public sealed record MagicConditionalProcResult(bool Accepted, MagicProcRejection Rejection,
    MagicConditionalProcOutcome? Outcome, RandomState RngState);

/// <summary>One bounded original 0x08 conditional-proc decision, with explicit evaluation mode and RNG ownership.</summary>
/// <remarks>ZOD-340; source 11464943bbdbd2a0af022d90cb7f10455cf911df. No status application, command or live RNG adoption.</remarks>
public static class MagicConditionalProc
{
    public static MagicConditionalProcResult EvaluateAfterHit(FaithMagicElementAmount projection,
        MagicFormulaEvaluationMode mode, MagicConditionalProcProfile? profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return Evaluate(new(projection.Kind,projection.Nullified ? MagicProcInvocation.ElementNullified : MagicProcInvocation.FamilyTail,mode),profile,rng);
    }

    public static MagicConditionalProcResult Evaluate(MagicConditionalProcRequest request,
        MagicConditionalProcProfile? profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Formula) || !Enum.IsDefined(request.Invocation) || !Enum.IsDefined(request.Mode))
            throw new ArgumentOutOfRangeException(nameof(request));
        if (request.Formula == FaithMagicAmountKind.Healing && request.Invocation == MagicProcInvocation.ElementNullified)
            throw new ArgumentException("The healing formula has no damage-element nullification stage.",nameof(request));
        MagicConditionalProcResult WithoutDraw(MagicProcDisposition reason) => new(true,MagicProcRejection.None,new(reason,false),rng);
        if (request.Invocation == MagicProcInvocation.FormulaMissed) return WithoutDraw(MagicProcDisposition.FormulaMissed);
        if (request.Formula == FaithMagicAmountKind.Healing) return WithoutDraw(MagicProcDisposition.NoConditionalBranch);
        if (request.Invocation == MagicProcInvocation.ElementNullified) return WithoutDraw(MagicProcDisposition.ElementNullified);
        if (profile is null || !profile.OrdinaryPrimaryContext || !profile.NoPendingWeaponProcOrReaction ||
            (request.Mode != MagicFormulaEvaluationMode.Preview && !profile.AddedEffectFlagKnownClear))
            return new(false,MagicProcRejection.UnsupportedContext,null,rng);
        // Preview returns before reading the existing added-effect bit. Neither
        // preview nor AI enters the execution-only inner roll or sets that bit.
        if (request.Mode == MagicFormulaEvaluationMode.Preview) return WithoutDraw(MagicProcDisposition.Passed);
        if (request.Mode == MagicFormulaEvaluationMode.AiSimulation) return WithoutDraw(MagicProcDisposition.Failed);
        try
        {
            var draw = new RandomStateGenerator().Next(rng);
            int percent = (int)(draw.Value * 100 / 32768);
            bool passed = percent < 19;
            return new(true,MagicProcRejection.None,new(passed ? MagicProcDisposition.Passed : MagicProcDisposition.Failed,
                passed,draw.Value,percent),draw.State);
        }
        catch (OverflowException) { return new(false,MagicProcRejection.RandomDrawCountExhausted,null,rng); }
    }
}
