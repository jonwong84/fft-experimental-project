using System.Collections.Immutable;

namespace Zodiac.Framework;

internal static class OrdinaryMagicLearningExecution
{
    internal static OrdinaryMagicBattleState Scan(OrdinaryMagicBattleState state, BattleUnitId actorId,
        AssetId abilityId, List<OrdinaryMagicEvent> events)
    {
        if (state.Definition.Learning is not { } learning)
            return state;
        var ability = learning.Abilities[abilityId];
        var scan = LearnOnHit.Scan(actorId, abilityId, ability.LearnOnHit, ability.Rate,
            state.Units.Select(u => new LearnOnHitCandidate(u.Id, u.Occupant.SourceOrder, u.OutcomeMarker,
                learning.PrimarySlots(u.Definition.RewardJobId), u.LearnedAbilitySlots[u.Definition.RewardJobId])).ToImmutableArray(),
            state.Turns.RngState);
        var next = state.With(units: state.Units.Select((u, i) => u with { OutcomeMarker = scan.Units[i].Marker }).ToImmutableArray(),
            turns: new(state.Turns.Units, scan.RngState, state.Turns.Ticks, state.Turns.Phase));
        if (scan.LearnerId is not { } id)
            return next;
        var learner = next.Units[OrdinaryMagicBattle.Index(next, id)];
        var report = new OrdinaryMagicLearningReport(checked(state.Revision + 1), id, abilityId, learner.Definition.RewardJobId);
        if (!learner.Definition.PlayerControlled)
        {
            // Disabled prompt -> CLOSE/NONE -> command7 -> PROMPT_YES, not UNRESOLVED_FF.
            events.Add(new OrdinaryMagicEvent.LearningAnswered(id, abilityId, true, Automatic: true));
            return Learn(next, report);
        }
        events.Add(new OrdinaryMagicEvent.LearningOffered(report.Id, id, abilityId));
        return next.With(learningReport: report);
    }

    internal static OrdinaryMagicBattleState Answer(OrdinaryMagicBattleState state, OrdinaryMagicCommand.AnswerLearning answer,
        List<OrdinaryMagicEvent> events)
    {
        if (state.LearningReport is not { } report || report.Id != answer.ReportId || report.LearnerId != answer.UnitId)
            throw new OrdinaryMagicExecutionException("That learning report is no longer available.");
        var next = state;
        if (answer.Accept)
            next = Learn(state, report);
        events.Add(new OrdinaryMagicEvent.LearningAnswered(report.LearnerId, report.AbilityId, answer.Accept));
        return OrdinaryMagicCastExecution.FinishObjective(next.With(clearLearningReport: true), events);
    }

    private static OrdinaryMagicBattleState Learn(OrdinaryMagicBattleState state, OrdinaryMagicLearningReport report)
    {
        int index = OrdinaryMagicBattle.Index(state, report.LearnerId);
        var unit = state.Units[index];
        var learned = LearnOnHit.Accept(report.AbilityId, state.Definition.Learning!.PrimarySlots(report.JobId),
            unit.LearnedAbilitySlots[report.JobId]);
        return state.With(units: state.Units.SetItem(index, unit with
            { LearnedAbilitySlots = unit.LearnedAbilitySlots.SetItem(report.JobId, learned) }));
    }
}
