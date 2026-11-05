namespace Zodiac.Framework;

public enum MagicActionRestriction { None, CannotAct, DeepWater }
public enum MagicResourceMenu { Default, Monster, Charge, Other }
public enum MagicMpBlock { None = 0, Silenced = 2, NotEnoughMp = 3, Frog = 5, CannotAct = 7, DeepWater = 8 }

public sealed record MagicMpActor(ushort Mp, bool HalfMp, bool IsMime, bool Frog, bool Silence,
    bool DontAct, MagicActionRestriction Restriction);
public sealed record MagicMpAbility(short OriginalAbilityId, byte BaseCost, MagicResourceMenu Menu, bool AffectedBySilence);
public sealed record MagicMpProfile(bool ActionStateBookkeepingKnown = false);
public sealed record MagicMpRequest(MagicMpActor Actor, MagicMpAbility Ability, MagicFormulaEvaluationMode Mode,
    MagicMpProfile? Profile);
public sealed record MagicMpOutcome(MagicMpBlock Block, byte RecordedCost, ushort CandidateMp, bool SubtractionPerformed)
{
    public bool Usable => Block == MagicMpBlock.None;
}
public sealed record MagicMpResult(bool Accepted, MagicMpOutcome? Outcome);

/// <summary>Pure resource work after certified action-state bookkeeping; no MP/status/turn publication.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; original ability ranges are compatibility branches, not managed catalog limits.</remarks>
public static class MagicResources
{
    public static MagicMpResult EvaluateMp(MagicMpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Actor);
        ArgumentNullException.ThrowIfNull(request.Ability);
        var a = request.Actor; var s = request.Ability;
        if (!Enum.IsDefined(a.Restriction) || !Enum.IsDefined(s.Menu) || !Enum.IsDefined(request.Mode))
            throw new ArgumentOutOfRangeException(nameof(request));
        if (request.Profile is not { ActionStateBookkeepingKnown: true }) return new(false, null);
        MagicMpResult Result(MagicMpBlock block = MagicMpBlock.None, byte cost = 0) => new(true, new(block, cost, a.Mp, false));
        var restriction = a.DontAct ? MagicActionRestriction.CannotAct : a.Restriction;
        if (restriction != MagicActionRestriction.None)
            return Result(restriction == MagicActionRestriction.CannotAct ? MagicMpBlock.CannotAct : MagicMpBlock.DeepWater);
        if (a.Frog && (s.OriginalAbilityId is not (0 or 29 or 367) || s.Menu == MagicResourceMenu.Charge))
            return Result(MagicMpBlock.Frog);
        if (s.OriginalAbilityId is >= 0 and < 368)
        {
            if (s.Menu is MagicResourceMenu.Default or MagicResourceMenu.Monster && s.AffectedBySilence && a.Silence)
                return Result(MagicMpBlock.Silenced);
            if (s.Menu == MagicResourceMenu.Default && !a.IsMime)
            {
                byte cost = a.HalfMp ? (byte)(s.BaseCost >> 1) : s.BaseCost;
                // The original reads the unsigned MP field through signed16 before comparing it.
                if (unchecked((short)a.Mp) < cost) return Result(MagicMpBlock.NotEnoughMp, cost);
                bool subtract = request.Mode != MagicFormulaEvaluationMode.Preview;
                return new(true, new(MagicMpBlock.None, cost, subtract ? (ushort)(a.Mp - cost) : a.Mp, subtract));
            }
        }
        return Result();
    }
}
