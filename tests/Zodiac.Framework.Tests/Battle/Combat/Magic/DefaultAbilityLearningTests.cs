using System.Collections.Immutable;
using Zodiac.Framework.Random;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class DefaultAbilityLearningTests
{
    private static readonly BattleUnitId Mage = DefaultAbilityBattleTestData.Mage;
    private static readonly BattleUnitId Target = DefaultAbilityBattleTestData.Target;
    private static readonly AssetId Fire = DefaultAbilityBattleTestData.Fire;

    internal static DefaultAbilityBattleDefinition Definition(byte rate = 100, byte experience = 95, byte charge = 4,
        ImmutableArray<AbilityLearningSlot>? targetSlots = null, ImmutableHashSet<int>? known = null)
    {
        var d = DefaultAbilityBattleTestData.Definition(experience: experience, charge: charge);
        var slots = targetSlots ?? [new(3, Fire)];
        var learning = new DefaultAbilityLearningDefinition([new(Fire, true, rate)],
            new Dictionary<AssetId, ImmutableArray<AbilityLearningSlot>>
            {
                [d.Units[0].RewardJobId] = [new(0, Fire)], [d.Units[1].RewardJobId] = slots
            });
        return new(d.Map, [d.Units[0] with { LearnedPrimarySlots = [0] }, d.Units[1] with { LearnedPrimarySlots = known ?? [] }],
            d.Spells.Values, d.Jobs, d.JobThresholds, d.InitialRng, learning: learning);
    }

    internal static DefaultAbilityBattleResult Resolve(DefaultAbilityBattleDefinition d) => DefaultAbilityBattle.Apply(
        DefaultAbilityBattleTestData.BeforeResolution(d), new DefaultAbilityCommand.Advance());

    [Fact]
    public void OfferFollowsGrowthWithoutLearningUntilAccepted()
    {
        var d = Definition();
        var result = Resolve(d);
        Assert.True(result.Accepted, result.Rejection);
        var state = result.State;
        var baseline = Resolve(DefaultAbilityBattleTestData.Definition(experience: 95)).State;
        var report = Assert.IsType<DefaultAbilityLearningReport>(state.LearningReport);
        Assert.Equal(Target, report.LearnerId);
        Assert.Equal(11, state.Units[0].Level);
        Assert.Equal(baseline.Turns.RngState.DrawCount + 1, state.Turns.RngState.DrawCount);
        Assert.Equal(new RandomStateGenerator().Next(baseline.Turns.RngState).State, state.Turns.RngState);
        Assert.True(result.Events.IndexOf(result.Events.OfType<DefaultAbilityEvent.Rewarded>().Single()) <
            result.Events.IndexOf(result.Events.OfType<DefaultAbilityEvent.LearningOffered>().Single()));
        Assert.Empty(state.Units[1].LearnedAbilitySlots[report.JobId]);
        Assert.DoesNotContain(Fire, DefaultAbilityBattle.KnownSpells(state, Target));
        Assert.Equal(AbilityOutcomeMarker.LearnChecked, state.Units[1].OutcomeMarker);
        var accepted = DefaultAbilityBattleTestData.Apply(state, new DefaultAbilityCommand.AnswerLearning(report.Id, Target, true));
        Assert.Null(accepted.LearningReport);
        Assert.Contains(Fire, DefaultAbilityBattle.KnownSpells(accepted, Target));
        Assert.Equal(state.Turns, accepted.Turns);
        Assert.Equal(state.Units[1].JobPoints, accepted.Units[1].JobPoints);
        Assert.Empty(d.Units[1].LearnedSpells);
        Assert.Empty(d.Units[1].LearnedPrimarySlots!);
    }

    [Fact]
    public void PendingReportGatesEveryOtherCommandAndRejectsStaleAnswers()
    {
        var state = Resolve(Definition()).State;
        var report = state.LearningReport!;
        DefaultAbilityCommand[] blocked = [new DefaultAbilityCommand.Advance(), new DefaultAbilityCommand.Wait(Target),
            new DefaultAbilityCommand.Cast(Target, Fire, new(0, 2, 0)), new DefaultAbilityCommand.Step(Target, new(3, 2, 0)),
            new DefaultAbilityCommand.Move(Target, new(3, 2, 0)),
            new DefaultAbilityCommand.AnswerLearning(report.Id + 1, Target, true),
            new DefaultAbilityCommand.AnswerLearning(report.Id, Mage, true)];
        foreach (var command in blocked)
        {
            var rejected = DefaultAbilityBattle.Apply(state, command);
            Assert.False(rejected.Accepted);
            Assert.Same(state, rejected.State);
            Assert.Empty(rejected.Events);
        }
        var next = DefaultAbilityBattleTestData.Apply(state, new DefaultAbilityCommand.AnswerLearning(report.Id, Target, false));
        Assert.False(DefaultAbilityBattle.Apply(next, new DefaultAbilityCommand.AnswerLearning(report.Id, Target, true)).Accepted);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DuplicateAcceptanceAndDeclineCloseExactlyOneReport(bool accept)
    {
        var state = Resolve(Definition(targetSlots: [new(0, Fire), new(8, Fire), new(21, Fire)], known: [0])).State;
        var report = state.LearningReport!;
        var next = DefaultAbilityBattleTestData.Apply(state, new DefaultAbilityCommand.AnswerLearning(report.Id, Target, accept));
        Assert.True(next.Units[1].LearnedAbilitySlots[report.JobId].SetEquals(accept ? [0, 8, 21] : [0]));
        Assert.Equal(state.Turns.RngState, next.Turns.RngState);
        Assert.Null(next.LearningReport);
    }

    [Fact]
    public void IneligibleAffectedTargetStillBecomesCheckedWithoutDraws()
    {
        var state = Resolve(Definition(targetSlots: [])).State;
        var baseline = Resolve(DefaultAbilityBattleTestData.Definition(experience: 95)).State;
        Assert.Null(state.LearningReport);
        Assert.Equal(AbilityOutcomeMarker.LearnChecked, state.Units[1].OutcomeMarker);
        Assert.Equal(baseline.Turns.RngState, state.Turns.RngState);
    }

    [Fact]
    public void RateZeroDrawsAndDoesNotOffer()
    {
        var state = Resolve(Definition(rate: 0)).State;
        Assert.Null(state.LearningReport);
        Assert.Equal(AbilityOutcomeMarker.LearnChecked, state.Units[1].OutcomeMarker);
        Assert.Equal(Resolve(DefaultAbilityBattleTestData.Definition(experience: 95)).State.Turns.RngState.DrawCount + 1,
            state.Turns.RngState.DrawCount);
    }

    [Fact]
    public void LearningCounterExhaustionRollsBackHpMpRewardsMarkersAndEvents()
    {
        var before = DefaultAbilityBattleTestData.BeforeResolution(Definition(experience: 0));
        before = before.With(turns: new(before.Turns.Units, new(57, ulong.MaxValue - 5), before.Turns.Ticks, before.Turns.Phase));
        var result = DefaultAbilityBattle.Apply(before, new DefaultAbilityCommand.Advance());
        Assert.False(result.Accepted);
        Assert.Same(before, result.State);
        Assert.Empty(result.Events);
        Assert.Equal((ushort)200, before.Units[1].Vitals.Hp);
        Assert.Equal((ushort)75, before.Units[0].Current.Mp);
    }

    [Fact]
    public void InstantCastReportResumesUnfinishedActiveTurn()
    {
        var state = DefaultAbilityBattleTestData.Apply(Definition(charge: 0).Start(), new DefaultAbilityCommand.Advance());
        state = DefaultAbilityBattleTestData.Apply(state, new DefaultAbilityCommand.Cast(Mage, Fire, new(4, 2, 0)));
        Assert.Equal(Mage, state.Turns.ActiveUnitId);
        var report = state.LearningReport!;
        state = DefaultAbilityBattleTestData.Apply(state, new DefaultAbilityCommand.AnswerLearning(report.Id, Target, true));
        state = DefaultAbilityBattleTestData.Apply(state, new DefaultAbilityCommand.Wait(Mage));
        Assert.Null(state.Turns.ActiveUnitId);
    }

    [Fact]
    public void RestartRestoresInitialSlotsAndCareer()
    {
        var d = Definition();
        var state = Resolve(d).State;
        state = DefaultAbilityBattleTestData.Apply(state, new DefaultAbilityCommand.AnswerLearning(state.LearningReport!.Id, Target, true));
        Assert.Contains(Fire, DefaultAbilityBattle.KnownSpells(state, Target));
        var reset = d.Start();
        Assert.DoesNotContain(Fire, DefaultAbilityBattle.KnownSpells(reset, Target));
        Assert.Null(reset.LearningReport);
        Assert.Equal(d.InitialRng, reset.Turns.RngState);
        Assert.Empty(reset.Units[1].LearnedAbilitySlots[reset.Units[1].Definition.RewardJobId]);
    }
}
