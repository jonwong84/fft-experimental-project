using Zodiac.Framework.CombatRules;

namespace Zodiac.Framework;

/// <summary>Pure preparation/countdown proposals; no MP spend, source status bookkeeping, events or global clock.</summary>
public static class MagicChargePlanQuery
{
    public static MagicChargePlanResult Prepare(MagicChargePlanRequest request)
    {
        var prepared = OrdinaryMagicPreparation.Prepare(request);
        return new(prepared.Accepted, prepared.Outcome?.Plan);
    }

    public static MagicChargePlanStep Tick(PendingMagicCast plan, bool turnStatusBlocksWait)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var step = MagicCharge.Tick(plan.Counter, turnStatusBlocksWait);
        return new(step.Changed ? new(plan.Definition, plan.CasterId, plan.Aim, step.Counter) : plan, step.Ready, step.Changed);
    }
}
