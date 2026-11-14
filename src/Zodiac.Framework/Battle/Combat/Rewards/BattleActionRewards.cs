using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Pure ordinary reward projection. Does not apply raw growth, unlocks, learning, or battle/turn state.</summary>
public static class BattleActionRewards
{
    public static ActionRewardProjectionResult Project(ImmutableArray<ActionRewardUnit> roster, BattleUnitId actorId,
        byte originalActorJobId, byte earnedExperience, bool hamedo, bool canEarnExpJp,
        ImmutableArray<ushort> thresholds, bool priorLevelGained = false)
    {
        // Source clears display and tests these gates before looking up an actor.
        if (hamedo) return new(ActionRewardDisposition.Rejected, null);
        if (!canEarnExpJp) return new(ActionRewardDisposition.Skipped, null);
        if (roster.IsDefaultOrEmpty) throw new ArgumentException("Explicit source roster required.", nameof(roster));
        int slot = JobPointArithmetic.OriginalRewardSlot(originalActorJobId);
        var ids = new HashSet<BattleUnitId>(); var orders = new HashSet<int>();
        foreach (var unit in roster)
        {
            if (unit is null || string.IsNullOrWhiteSpace(unit.UnitId.Value) || !ids.Add(unit.UnitId) ||
                unit.SourceOrder < 0 || !orders.Add(unit.SourceOrder) || !Enum.IsDefined(unit.InitialTeam) || !Enum.IsDefined(unit.CurrentTeam))
                throw new ArgumentException("Distinct identities/order and classified teams required.", nameof(roster));
        }
        var actor = roster.FirstOrDefault(u => u.UnitId == actorId);
        if (actor is null || !actor.Active || actor.Crystal || actor.Dead || actor.Petrify || actor.Treasure)
            return new(ActionRewardDisposition.Rejected, null);
        var experience = ActionRewardCalculation.ProjectExperience(actor.Experience, earnedExperience, actor.Level);
        byte levelDisplay = experience.RequiresStatGrowth || priorLevelGained ? experience.Level : (byte)0;
        var amounts = ActionRewardCalculation.Calculate(actor.Level, actor.JobLevel, earnedExperience, actor.IsMonster, actor.GainedJpUp);
        if (actor.IsMonster)
            return new(ActionRewardDisposition.Projected, new(amounts, experience, levelDisplay, 0, []));
        if (actor.JobSlot != slot) throw new ArgumentException("Actor career capture must match its reward slot.", nameof(roster));
        var points = ImmutableArray.CreateBuilder<ActionJobPointProjection>();
        void Offer(ActionRewardUnit unit, int award, SpellTargetTeam team)
        {
            // Petrify is deliberately absent from recipient exclusions.
            if (!unit.Active || unit.Crystal || unit.Dead || unit.Treasure || unit.IsMonster || unit.InitialTeam != team ||
                slot == 17 && unit.IsFemale || slot == 18 && unit.IsMale) return;
            if (unit.JobSlot != slot) throw new ArgumentException("Recipient JP capture must match the actor job slot.", nameof(roster));
            points.Add(new(unit.UnitId, slot, award, JobPointArithmetic.Add(unit.CurrentJobPoints, unit.TotalJobPoints, award, thresholds)));
        }
        foreach (var unit in roster.OrderBy(u => u.SourceOrder))
            if (unit.UnitId != actorId) Offer(unit, amounts.SpilloverJp, actor.CurrentTeam);
        Offer(actor, amounts.ActorJp, actor.InitialTeam);
        var own = points.FirstOrDefault(p => p.UnitId == actorId);
        int nextJobLevel = own?.Balance.JobLevel ?? actor.JobLevel;
        if (nextJobLevel > byte.MaxValue) throw new NotSupportedException("Job-level display requires a byte result.");
        byte jobDisplay = nextJobLevel != actor.JobLevel ? (byte)nextJobLevel : (byte)0;
        return new(ActionRewardDisposition.Projected, new(amounts, experience, levelDisplay, jobDisplay, points.ToImmutable()));
    }
}
