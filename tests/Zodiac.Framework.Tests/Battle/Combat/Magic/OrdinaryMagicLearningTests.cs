using System.Collections.Immutable;
using Zodiac.Framework.Random;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class OrdinaryMagicLearningTests
{
    private static readonly BattleUnitId Mage = OrdinaryMagicBattleTestData.Mage;
    private static readonly BattleUnitId Target = OrdinaryMagicBattleTestData.Target;
    private static readonly AssetId Fire = OrdinaryMagicBattleTestData.Fire;

    internal static OrdinaryMagicBattleDefinition Definition(byte rate = 100, byte experience = 95, byte charge = 4,
        ImmutableArray<AbilityLearningSlot>? targetSlots = null, ImmutableHashSet<int>? known = null)
    {
        var d = OrdinaryMagicBattleTestData.Definition(experience: experience, charge: charge);
        var slots = targetSlots ?? [new(3, Fire)];
        var learning = new OrdinaryMagicLearningDefinition([new(Fire, true, rate)],
            new Dictionary<AssetId, ImmutableArray<AbilityLearningSlot>>
            {
                [d.Units[0].RewardJobId] = [new(0, Fire)], [d.Units[1].RewardJobId] = slots
            });
        return new(d.Map, [d.Units[0] with { LearnedPrimarySlots = [0] }, d.Units[1] with { LearnedPrimarySlots = known ?? [] }],
            d.Spells.Values, d.Jobs, d.JobThresholds, d.InitialRng, learning: learning);
    }

    internal static OrdinaryMagicBattleResult Resolve(OrdinaryMagicBattleDefinition d) => OrdinaryMagicBattle.Apply(
        OrdinaryMagicBattleTestData.BeforeResolution(d), new OrdinaryMagicCommand.Advance());

    [Fact]
    public void OfferFollowsGrowthWithoutLearningUntilAccepted()
    {
        var d = Definition();
        var result = Resolve(d);
        Assert.True(result.Accepted, result.Rejection);
        var state = result.State;
        var baseline = Resolve(OrdinaryMagicBattleTestData.Definition(experience: 95)).State;
        var report = Assert.IsType<OrdinaryMagicLearningReport>(state.LearningReport);
        Assert.Equal(Target, report.LearnerId);
        Assert.Equal(11, state.Units[0].Level);
        Assert.Equal(baseline.Turns.RngState.DrawCount + 1, state.Turns.RngState.DrawCount);
        Assert.Equal(new RandomStateGenerator().Next(baseline.Turns.RngState).State, state.Turns.RngState);
        Assert.True(result.Events.IndexOf(result.Events.OfType<OrdinaryMagicEvent.Rewarded>().Single()) <
            result.Events.IndexOf(result.Events.OfType<OrdinaryMagicEvent.LearningOffered>().Single()));
        Assert.Empty(state.Units[1].LearnedAbilitySlots[report.JobId]);
        Assert.DoesNotContain(Fire, OrdinaryMagicBattle.KnownSpells(state, Target));
        Assert.Equal(AbilityOutcomeMarker.LearnChecked, state.Units[1].OutcomeMarker);
        var accepted = OrdinaryMagicBattleTestData.Apply(state, new OrdinaryMagicCommand.AnswerLearning(report.Id, Target, true));
        Assert.Null(accepted.LearningReport);
        Assert.Contains(Fire, OrdinaryMagicBattle.KnownSpells(accepted, Target));
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
        OrdinaryMagicCommand[] blocked = [new OrdinaryMagicCommand.Advance(), new OrdinaryMagicCommand.Wait(Target),
            new OrdinaryMagicCommand.Cast(Target, Fire, new(0, 2, 0)), new OrdinaryMagicCommand.Step(Target, new(3, 2, 0)),
            new OrdinaryMagicCommand.Move(Target, new(3, 2, 0)),
            new OrdinaryMagicCommand.AnswerLearning(report.Id + 1, Target, true),
            new OrdinaryMagicCommand.AnswerLearning(report.Id, Mage, true)];
        foreach (var command in blocked)
        {
            var rejected = OrdinaryMagicBattle.Apply(state, command);
            Assert.False(rejected.Accepted);
            Assert.Same(state, rejected.State);
            Assert.Empty(rejected.Events);
        }
        var next = OrdinaryMagicBattleTestData.Apply(state, new OrdinaryMagicCommand.AnswerLearning(report.Id, Target, false));
        Assert.False(OrdinaryMagicBattle.Apply(next, new OrdinaryMagicCommand.AnswerLearning(report.Id, Target, true)).Accepted);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DuplicateAcceptanceAndDeclineCloseExactlyOneReport(bool accept)
    {
        var state = Resolve(Definition(targetSlots: [new(0, Fire), new(8, Fire), new(21, Fire)], known: [0])).State;
        var report = state.LearningReport!;
        var next = OrdinaryMagicBattleTestData.Apply(state, new OrdinaryMagicCommand.AnswerLearning(report.Id, Target, accept));
        Assert.True(next.Units[1].LearnedAbilitySlots[report.JobId].SetEquals(accept ? [0, 8, 21] : [0]));
        Assert.Equal(state.Turns.RngState, next.Turns.RngState);
        Assert.Null(next.LearningReport);
    }

    [Fact]
    public void IneligibleAffectedTargetStillBecomesCheckedWithoutDraws()
    {
        var state = Resolve(Definition(targetSlots: [])).State;
        var baseline = Resolve(OrdinaryMagicBattleTestData.Definition(experience: 95)).State;
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
        Assert.Equal(Resolve(OrdinaryMagicBattleTestData.Definition(experience: 95)).State.Turns.RngState.DrawCount + 1,
            state.Turns.RngState.DrawCount);
    }

    [Fact]
    public void LearningCounterExhaustionRollsBackHpMpRewardsMarkersAndEvents()
    {
        var before = OrdinaryMagicBattleTestData.BeforeResolution(Definition(experience: 0));
        before = before.With(turns: new(before.Turns.Units, new(57, ulong.MaxValue - 5), before.Turns.Ticks, before.Turns.Phase));
        var result = OrdinaryMagicBattle.Apply(before, new OrdinaryMagicCommand.Advance());
        Assert.False(result.Accepted);
        Assert.Same(before, result.State);
        Assert.Empty(result.Events);
        Assert.Equal((ushort)200, before.Units[1].Vitals.Hp);
        Assert.Equal((ushort)75, before.Units[0].Current.Mp);
    }

    [Fact]
    public void InstantCastReportResumesUnfinishedActiveTurn()
    {
        var state = OrdinaryMagicBattleTestData.Apply(Definition(charge: 0).Start(), new OrdinaryMagicCommand.Advance());
        state = OrdinaryMagicBattleTestData.Apply(state, new OrdinaryMagicCommand.Cast(Mage, Fire, new(4, 2, 0)));
        Assert.Equal(Mage, state.Turns.ActiveUnitId);
        var report = state.LearningReport!;
        state = OrdinaryMagicBattleTestData.Apply(state, new OrdinaryMagicCommand.AnswerLearning(report.Id, Target, true));
        state = OrdinaryMagicBattleTestData.Apply(state, new OrdinaryMagicCommand.Wait(Mage));
        Assert.Null(state.Turns.ActiveUnitId);
    }

    [Fact]
    public void RestartRestoresInitialSlotsAndCareer()
    {
        var d = Definition();
        var state = Resolve(d).State;
        state = OrdinaryMagicBattleTestData.Apply(state, new OrdinaryMagicCommand.AnswerLearning(state.LearningReport!.Id, Target, true));
        Assert.Contains(Fire, OrdinaryMagicBattle.KnownSpells(state, Target));
        var reset = d.Start();
        Assert.DoesNotContain(Fire, OrdinaryMagicBattle.KnownSpells(reset, Target));
        Assert.Null(reset.LearningReport);
        Assert.Equal(d.InitialRng, reset.Turns.RngState);
        Assert.Empty(reset.Units[1].LearnedAbilitySlots[reset.Units[1].Definition.RewardJobId]);
    }
}
