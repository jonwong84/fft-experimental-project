using System.Collections.Immutable;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class OrdinaryMagicLearningOrderTests
{
    private static readonly BattleUnitId First = OrdinaryMagicBattleTestData.Target;
    private static readonly BattleUnitId Later = new("later");
    private static readonly AssetId Fire = OrdinaryMagicBattleTestData.Fire;

    private static OrdinaryMagicBattleDefinition Definition(bool controlled = true, bool ko = false, bool known = false,
        SpellTargetTeam team = SpellTargetTeam.Red)
    {
        var d = OrdinaryMagicLearningTests.Definition(experience: 0, charge: 0,
            targetSlots: [new(0, Fire), new(8, Fire)], known: known ? [0, 8] : []);
        var target = d.Units[1] with { PlayerControlled = controlled,
            Occupant = d.Units[1].Occupant with { InitialTeam = team, CurrentTeam = team } };
        if (ko) target = target with { Vitals = target.Vitals with { Hp = 1, Critical = true } };
        var later = d.Units[1] with { Current = d.Units[1].Current with { UnitId = Later },
            Occupant = d.Units[1].Occupant with { UnitId = Later, SourceOrder = 2, Position = new(4, 3, 0) },
            LearnedPrimarySlots = [] };
        // Deliberately reversed enumeration; the definition preserves original slot order.
        return new(d.Map, [later, target, d.Units[0]], d.Spells.Values, d.Jobs, d.JobThresholds, d.InitialRng, learning: d.Learning);
    }

    private static OrdinaryMagicBattleResult Cast(OrdinaryMagicBattleDefinition d)
    {
        var state = OrdinaryMagicBattleTestData.Apply(d.Start(), new OrdinaryMagicCommand.Advance());
        return OrdinaryMagicBattle.Apply(state, new OrdinaryMagicCommand.Cast(OrdinaryMagicBattleTestData.Mage, Fire, new(4, 2, 0)));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void One_actual_multi_target_action_offers_only_first_success_and_never_rescans_after_answer(bool accept)
    {
        var result = Cast(Definition()); Assert.True(result.Accepted, result.Rejection);
        Assert.Equal(2, result.Events.OfType<OrdinaryMagicEvent.Applied>().Count());
        var report = Assert.IsType<OrdinaryMagicLearningReport>(result.State.LearningReport);
        Assert.Equal(First, report.LearnerId);
        Assert.Equal(AbilityOutcomeMarker.LearnChecked, result.State.Units[1].OutcomeMarker);
        Assert.Equal(AbilityOutcomeMarker.Affected, result.State.Units[2].OutcomeMarker);
        var next = OrdinaryMagicBattleTestData.Apply(result.State, new OrdinaryMagicCommand.AnswerLearning(report.Id, First, accept));
        Assert.Null(next.LearningReport);
        Assert.DoesNotContain(Fire, OrdinaryMagicBattle.KnownSpells(next, Later));
        Assert.Equal(AbilityOutcomeMarker.Affected, next.Units[2].OutcomeMarker);
        Assert.Equal(result.State.Turns.RngState, next.Turns.RngState);
        Assert.True(next.Units[1].LearnedAbilitySlots[report.JobId].SetEquals(accept ? [0, 8] : []));
    }

    [Theory]
    [InlineData(SpellTargetTeam.Blue, false)]
    [InlineData(SpellTargetTeam.Red, false)]
    [InlineData(SpellTargetTeam.Blue, true)]
    [InlineData(SpellTargetTeam.Red, true)]
    public void Explicit_control_flag_drives_prompt_or_source_automatic_yes_independently_of_team(SpellTargetTeam team, bool controlled)
    {
        var result = Cast(Definition(controlled: controlled, team: team));
        Assert.True(result.Accepted, result.Rejection);
        Assert.Equal(controlled, result.State.LearningReport is not null);
        Assert.Equal(!controlled, OrdinaryMagicBattle.KnownSpells(result.State, First).Contains(Fire));
        Assert.DoesNotContain(Fire, OrdinaryMagicBattle.KnownSpells(result.State, Later));
        Assert.Equal(AbilityOutcomeMarker.Affected, result.State.Units[2].OutcomeMarker);
        if (!controlled)
        {
            Assert.DoesNotContain(result.Events, e => e is OrdinaryMagicEvent.LearningOffered);
            Assert.True(Assert.Single(result.Events.OfType<OrdinaryMagicEvent.LearningAnswered>()).Automatic);
            var manual = Cast(Definition(controlled: true, team: team));
            Assert.Equal(manual.State.Turns.RngState, result.State.Turns.RngState);
        }
    }

    [Fact]
    public void Known_first_recipient_is_checked_without_roll_before_next_recipient_succeeds()
    {
        var result = Cast(Definition(known: true)); Assert.True(result.Accepted, result.Rejection);
        Assert.Equal(Later, result.State.LearningReport!.LearnerId);
        Assert.All(result.State.Units.Skip(1), u => Assert.Equal(AbilityOutcomeMarker.LearnChecked, u.OutcomeMarker));
        // Two targets each consume five formula draws; one upkeep + one learning draw.
        Assert.Equal(21UL, result.State.Turns.RngState.DrawCount); // Initial counted draw9 +12.
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void Mixed_ko_and_living_learner_defers_authored_objective_until_report_closes(bool accept)
    {
        var result = Cast(Definition(ko: true)); Assert.True(result.Accepted, result.Rejection);
        Assert.True(result.State.Units[1].Current.Statuses.Dead);
        Assert.Equal(AbilityOutcomeMarker.Targeted, result.State.Units[1].OutcomeMarker);
        Assert.False(result.State.Finished);
        var report = result.State.LearningReport!; Assert.Equal(Later, report.LearnerId);
        var answered = OrdinaryMagicBattle.Apply(result.State, new OrdinaryMagicCommand.AnswerLearning(report.Id, Later, accept));
        Assert.True(answered.Accepted, answered.Rejection); Assert.True(answered.State.Finished);
        Assert.Single(answered.Events.OfType<OrdinaryMagicEvent.EncounterEnded>());
        Assert.Equal(result.State.Turns.RngState, answered.State.Turns.RngState);
        Assert.Null(answered.State.LearningReport);
        Assert.Empty(answered.State.Pending);
    }

    [Fact]
    public void A_newly_critical_living_target_remains_eligible()
    {
        var d = OrdinaryMagicLearningTests.Definition(experience: 0, charge: 0);
        d = new(d.Map, d.Units.SetItem(1, d.Units[1] with { Vitals = d.Units[1].Vitals with { Hp = 80 } }),
            d.Spells.Values, d.Jobs, d.JobThresholds, d.InitialRng, learning: d.Learning);
        var result = Cast(d); Assert.True(result.Accepted, result.Rejection);
        Assert.Equal((ushort)26, result.State.Units[1].Vitals.Hp);
        Assert.True(result.State.Units[1].Vitals.Critical);
        Assert.Equal(First, result.State.LearningReport!.LearnerId);
    }

    [Fact]
    public void A_charging_learner_keeps_its_other_pending_cast_frozen_until_answer()
    {
        var d = OrdinaryMagicLearningTests.Definition(experience: 0);
        d = new(d.Map, d.Units.SetItem(1, d.Units[1] with { LearnedSpells = [Fire] }),
            d.Spells.Values, d.Jobs, d.JobThresholds, d.InitialRng, learning: d.Learning);
        var state = OrdinaryMagicBattleTestData.Queued(d);
        state = OrdinaryMagicBattleTestData.Apply(state, new OrdinaryMagicCommand.Advance());
        state = OrdinaryMagicBattleTestData.Apply(state, new OrdinaryMagicCommand.Cast(First, Fire, new(0, 2, 0)));
        state = OrdinaryMagicBattleTestData.Apply(state, new OrdinaryMagicCommand.Wait(First));
        state = OrdinaryMagicBattleTestData.Apply(state, new OrdinaryMagicCommand.Advance());
        Assert.True(state.Units[1].Current.Statuses.Charging);
        Assert.Equal(First, state.LearningReport!.LearnerId); // Secondary grant does not mark the primary row known.
        var pending = state.Pending[First];
        var blocked = OrdinaryMagicBattle.Apply(state, new OrdinaryMagicCommand.Advance());
        Assert.False(blocked.Accepted); Assert.Same(state, blocked.State);
        state = OrdinaryMagicBattleTestData.Apply(state, new OrdinaryMagicCommand.AnswerLearning(state.LearningReport.Id, First, true));
        Assert.Same(pending, state.Pending[First]);
        Assert.True(state.Units[1].Current.Statuses.Charging);
        while (!state.Pending.IsEmpty)
            state = OrdinaryMagicBattleTestData.Apply(state, state.Turns.ActiveUnitId is { } id
                ? new OrdinaryMagicCommand.Wait(id) : new OrdinaryMagicCommand.Advance());
        Assert.Equal((ushort)66, state.Units[0].Vitals.Hp);
        Assert.False(state.Units[1].Current.Statuses.Charging);
    }
}
