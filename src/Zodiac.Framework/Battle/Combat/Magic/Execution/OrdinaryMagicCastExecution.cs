using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

internal static class OrdinaryMagicCastExecution
{
    private static MagicResultFinalizationProfile ResultProfile => new(true, true, true, true, true, true,
        new(true, true, true));

    internal static OrdinaryMagicBattleState Resolve(OrdinaryMagicBattleState state, BattleUnitId casterId,
        List<OrdinaryMagicEvent> events)
    {
        int actorIndex = OrdinaryMagicBattle.Index(state, casterId);
        var plan = state.Pending[casterId];
        var actor = state.Units[actorIndex];
        actor = actor with { Current = actor.Current with { Statuses = new() } };
        var next = state.With(units: state.Units.SetItem(actorIndex, actor), pending: state.Pending.Remove(casterId));
        var ready = MagicCastQuery.PrepareReady(new(actor.Current, plan.Definition, MagicDamageWeather.None,
            MagicFormulaEvaluationMode.Execute, ResultProfile, new(true, true, true, true, true)));
        if (!ready.Accepted)
            throw new OrdinaryMagicExecutionException("The ready spell exceeded the admitted calculation context.");
        if (ready.Cast is null)
        {
            events.Add(new OrdinaryMagicEvent.Fizzled(casterId, plan.Definition.Id, 0, "Not enough MP at readiness."));
            return next;
        }
        var resource = ready.Resource!;
        SpellAimAnchor? anchor = plan.Aim.TargetUnitId is { } targetId
            ? next.Units.Where(u => u.Id == targetId).Select(u => new SpellAimAnchor(u.Id, u.Position)).SingleOrDefault() : null;
        var aim = SpellAimResolver.Resolve(next.Definition.Map, plan.Aim, anchor, new(true, true));
        ImmutableArray<BattleUnitId> ids = [];
        if (aim.Accepted)
        {
            var area = DefaultAbilityTargeting.Area(next.Definition.Map, actor.Position, aim.Position!.Value,
                plan.Definition.Targeting!, OrdinaryMagicBattle.GeometryProfile);
            if (!area.Accepted)
                throw new OrdinaryMagicExecutionException("The stored aim exceeded admitted spell geometry.");
            var selected = DefaultAbilityTargeting.SelectTargets(area.Outcome!, casterId,
                next.Units.Select(u => u.Occupant).ToImmutableArray(), new(true));
            if (!selected.Accepted)
                throw new OrdinaryMagicExecutionException("The spell roster could not be resolved.");
            ids = selected.Outcome!.UnitIds;
        }
        var units = next.Units.Select(u => u with { OutcomeMarker = AbilityOutcomeMarker.None }).ToImmutableArray();
        units = units.SetItem(actorIndex, units[actorIndex] with { Current = actor.Current with { Mp = resource.CandidateMp } });
        var rng = next.Turns.RngState;
        byte earned = 0;
        ImmutableArray<MagicCastTargetResult> frames = [];
        if (!ids.IsEmpty)
        {
            var current = ids.Select(id => next.Units[OrdinaryMagicBattle.Index(next, id)].Current).ToImmutableArray();
            var resolved = MagicCastQuery.ResolveTargets(ready.Cast, current, new(true, true), rng);
            if (!resolved.Accepted)
            {
                if (resolved.Rejection == FaithMagicResolutionRejection.RandomDrawCountExhausted)
                    throw new OverflowException();
                throw new OrdinaryMagicExecutionException("The spell calculation exceeded the admitted context.");
            }
            frames = resolved.Outcome!.Targets;
            rng = resolved.RngState;
            foreach (var target in frames)
            {
                int index = OrdinaryMagicBattle.Index(next, target.TargetId);
                var unit = units[index];
                var applied = OrdinaryMagicStatusRules.Apply(unit.Current, unit.Vitals, unit.KoCount,
                    unit.DeathCounter, unit.OutcomeMarker, target.Result);
                units = units.SetItem(index, unit with
                {
                    Current = applied.Current,
                    Vitals = applied.Vitals,
                    KoCount = applied.KoCount,
                    DeathCounter = applied.DeathCounter,
                    OutcomeMarker = applied.Marker
                });
                earned = ActionExperience.Accumulate(earned, actor.Level, unit.Level, applied.KoCount,
                    applied.Outcome, true, false);
                events.Add(new OrdinaryMagicEvent.Applied(unit.Id, applied.Vitals.Hp,
                    applied.Vitals.Critical, applied.Current.Statuses.Dead));
            }
            events.Add(new OrdinaryMagicEvent.Resolved(casterId, plan.Definition.Id, resource.RecordedCost, aim.Position!.Value, frames));
        }
        else
            events.Add(new OrdinaryMagicEvent.Fizzled(casterId, plan.Definition.Id, resource.RecordedCost,
                aim.Accepted ? "No targets; MP was spent." : "The stored aim failed; MP was spent."));

        // Fire's separate actor work is a miss; primary application still overwrites its marker.
        units = units.SetItem(actorIndex, units[actorIndex] with { OutcomeMarker = AbilityOutcomeMarker.Targeted });
        next = next.With(units: units, turns: WithRng(next.Turns, rng));
        next = Rewards(next, actorIndex, earned, events);
        next = OrdinaryMagicLearningExecution.Scan(next, casterId, plan.Definition.Id, events);
        return next.LearningReport is null ? FinishObjective(next, events) : next;
    }

    internal static OrdinaryMagicBattleState FinishObjective(OrdinaryMagicBattleState next, List<OrdinaryMagicEvent> events)
    {
        var knockedOut = next.Units.FirstOrDefault(u => u.Current.Statuses.Dead);
        if (knockedOut is not null)
        {
            var settled = next.Units.Select(u => u with { Current = u.Current with
                { Statuses = u.Current.Statuses with { Charging = false } } }).ToImmutableArray();
            var turns = next.Turns.Units.Select(u => u.Update(u.Ct, false, u.MovementTaken, u.ActionTaken));
            next = next.With(units: settled, turns: new(turns, next.Turns.RngState, next.Turns.Ticks, TurnCyclePhase.PickUnit),
                pending: ImmutableDictionary<BattleUnitId, PendingMagicCast>.Empty, phase: OrdinaryMagicClockPhase.PickUnit,
                finished: true, fallen: next.PlayerUnitFallen || next.Units.Any(u => u.Current.Statuses.Dead && u.Occupant.InitialTeam == SpellTargetTeam.Blue));
            events.Add(new OrdinaryMagicEvent.EncounterEnded(knockedOut.Id));
        }
        return next;
    }

    private static OrdinaryMagicBattleState Rewards(OrdinaryMagicBattleState state, int actorIndex, byte earned,
        List<OrdinaryMagicEvent> events)
    {
        var actor = state.Units[actorIndex];
        var jobId = actor.Definition.RewardJobId;
        int slot = JobPointArithmetic.OriginalRewardSlot(actor.Definition.OriginalJobId);
        var roster = state.Units.Select(u => new ActionRewardUnit(u.Id, u.Occupant.SourceOrder, u.Occupant.InitialTeam,
            u.Occupant.CurrentTeam, slot, u.Level, u.Experience, (byte)u.JobPoints[jobId].JobLevel,
            u.JobPoints[jobId].Current, u.JobPoints[jobId].Total, Dead: u.Current.Statuses.Dead,
            IsMale: u.Current.Identity.IsMale, IsFemale: u.Current.Identity.IsFemale)).ToImmutableArray();
        var result = BattleActionRewards.Project(roster, actor.Id, actor.Definition.OriginalJobId, earned,
            false, true, state.Definition.JobThresholds);
        if (result.Projection is not { } reward)
            return state;
        var units = state.Units;
        var rng = state.Turns.RngState;
        if (reward.ActorExperience.RequiresStatGrowth)
        {
            var growth = BattleRawStatGrowth.Calculate(actor.RawStats, actor.Definition.Growth, actor.Level, false, rng);
            if (!growth.Accepted)
                throw new OverflowException();
            rng = growth.RngState;
            var refreshed = EquipmentAttributeCalculation.CalculateMode1(growth.Stats!, actor.Definition.Multipliers,
                actor.Vitals.Hp, actor.Current.Mp, actor.Definition.BaseAttributes, actor.Definition.Move, actor.Definition.Jump, []);
            actor = actor with
            {
                RawStats = growth.Stats!,
                Current = actor.Current with { Mp = refreshed.Attributes.Mp, MagicAttack = refreshed.ActualAttributes.MagicAttack },
                Vitals = OrdinaryMagicStatusRules.RefreshCritical(actor.Vitals with
                    { Hp = refreshed.Attributes.Hp, MaxHp = refreshed.Attributes.MaxHp, MaxMp = refreshed.Attributes.MaxMp })
            };
        }
        actor = actor with { Level = reward.ActorExperience.Level, Experience = reward.ActorExperience.Experience };
        units = units.SetItem(actorIndex, actor);
        foreach (var point in reward.JobPoints)
        {
            int index = OrdinaryMagicBattle.Index(state, point.UnitId);
            var unit = units[index];
            var jobs = unit.JobPoints.SetItem(jobId, point.Balance);
            units = units.SetItem(index, unit with
            {
                JobPoints = jobs,
                UnlockedJobs = state.Definition.Jobs.Calculate(jobs.ToImmutableDictionary(p => p.Key, p => (byte)p.Value.JobLevel),
                    unit.Current.Identity.IsMale, unit.Current.Identity.IsFemale)
            });
        }
        events.Add(new OrdinaryMagicEvent.Rewarded(actor.Id, reward.Amounts.EarnedExperience, reward.Amounts.ActorJp,
            reward.LevelForDisplay, reward.JobLevelForDisplay));
        return state.With(units: units, turns: WithRng(state.Turns, rng));
    }

    private static TurnCycleState WithRng(TurnCycleState turns, RandomState rng) =>
        new(turns.Units, rng, turns.Ticks, turns.Phase);
}
