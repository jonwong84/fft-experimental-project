namespace Zodiac.Framework;

/// <summary>Pure resource work after certified action-state bookkeeping; no MP/status/turn publication.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; original ability ranges are compatibility branches, not managed catalog limits.</remarks>
public static class MagicResources
{
    public static MagicMpResult EvaluateMp(MagicMpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Actor);
        ArgumentNullException.ThrowIfNull(request.Ability);
        var a = request.Actor; var s = request.Ability;
        ArgumentOutOfRangeException.ThrowIfNegative(s.BaseCost, nameof(request.Ability.BaseCost));
        if (!Enum.IsDefined(a.Restriction) || !Enum.IsDefined(s.Menu) || !Enum.IsDefined(request.Mode))
            throw new ArgumentOutOfRangeException(nameof(request));
        if (request.Profile is not { ActionStateBookkeepingKnown: true }) return new(false, null);
        MagicMpResult Result(MagicMpBlock block = MagicMpBlock.None, int cost = 0) => new(true, new(block, cost, a.Mp, false));
        var restriction = a.DontAct ? MagicActionRestriction.CannotAct : a.Restriction;
        if (restriction != MagicActionRestriction.None)
            return Result(restriction == MagicActionRestriction.CannotAct ? MagicMpBlock.CannotAct : MagicMpBlock.DeepWater);
        if (a.Frog && (s.OriginalAbilityId is not (0 or 29 or 367) || s.Menu == MagicResourceMenu.Charge))
            return Result(MagicMpBlock.Frog);
        if (s.OriginalAbilityId is >= 0 and < 368)
        {
            if ((s.Menu is MagicResourceMenu.Default or MagicResourceMenu.Monster) && s.AffectedBySilence && a.Silence)
                return Result(MagicMpBlock.Silenced);
            if (s.Menu == MagicResourceMenu.Default && !a.IsMime)
            {
                int cost = a.HalfMp ? s.BaseCost >> 1 : s.BaseCost;
                // The original reads the unsigned MP field through signed16 before comparing it.
                if (unchecked((short)a.Mp) < cost) return Result(MagicMpBlock.NotEnoughMp, cost);
                bool subtract = request.Mode != MagicFormulaEvaluationMode.Preview;
                return new(true, new(MagicMpBlock.None, cost, subtract ? (ushort)(a.Mp - cost) : a.Mp, subtract));
            }
        }
        return Result();
    }
}
