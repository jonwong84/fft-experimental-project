using Xunit;
using Zodiac.Framework.Random;
using SelfData = Zodiac.Framework.Ai.Tests.AccumulateAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class BasicSkillAiSelectionTests
{
    private static readonly BasicSkillAiSelectionProfile Selection = new(true, true, true);

    [Fact]
    public void Literal_sequential_lottery_can_choose_Accumulate_after_two_higher_priority_failures()
    {
        var state = Battle(); var before = Snapshot(state); var result = Select(state); var o = Success(result);
        Assert.Equal(new RandomState(3297241969, 23), o.LotteryEntryRng);
        Assert.Equal(new RandomState(3794901700, 26), result.RngState);
        Assert.Equal(new[] { 25089u, 21183u, 25137u }, o.Lottery.Select(r => r.Raw));
        Assert.Equal(new[] { 89, 83, 37 }, o.Lottery.Select(r => r.Modulo));
        Assert.Equal(new[] { false, false, true }, o.Lottery.Select(r => r.Passed));
        Assert.Equal((2, AiLotteryReason.FirstRoll, BasicSkillAiPlanKind.Accumulate, Far, true),
            (o.SelectedRankedIndex, o.Reason, o.Plan.Kind, o.Plan.Destination, o.Plan.ActBeforeMove));
        var action = Assert.IsType<BasicSkillAiAccumulate>(o.Plan.Action);
        Assert.Equal((A, B, SelfData.Self), (action.Value.Projection!.Outcome!.Candidate.ActorId,
            action.Value.Projection.Outcome.Candidate.OpponentId, action.Value.Projection.Outcome.Candidate.AbilityId));
        Assert.True(o.Plan.ChooseFacingAtWait); Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void First_ranked_Attack_and_standalone_Stone_use_the_same_source_lottery()
    {
        var attack = Select(Battle(evade: 0)); var a = Success(attack);
        Assert.Equal(BasicSkillAiPlanKind.Attack, a.Plan.Kind); Assert.Single(a.Lottery);
        Assert.Equal(new RandomState(3791773014, 24), attack.RngState);
        var stone = Select(Battle(evade: 0, target: new(4, 1, 0))); var s = Success(stone);
        Assert.Equal(BasicSkillAiPlanKind.ThrowStone, s.Plan.Kind); Assert.False(s.Plan.ActBeforeMove);
        Assert.Equal(new RandomState(267834847, 17), stone.RngState); Assert.Single(s.Lottery);
        Assert.False(s.Composition.Accumulate.Usefulness.SelfUseful);
    }

    [Fact]
    public void Eight_failed_rolls_with_a_nonzero_rank_keep_slot_zero_including_empty_slot_draws()
    {
        var result = Select(Battle(target: new(4, 1, 0))); var o = Success(result);
        Assert.Equal(AiLotteryReason.AllRollsFailedUseFirst, o.Reason); Assert.Equal(0, o.SelectedRankedIndex);
        Assert.Equal(BasicSkillAiPlanKind.ThrowStone, o.Plan.Kind); Assert.Equal(8, o.Lottery.Length);
        Assert.Equal(3, o.Lottery[0].HitPercent); Assert.All(o.Lottery.Skip(1), r => Assert.Equal(0, r.HitPercent));
        Assert.All(o.Lottery, r => Assert.False(r.Passed)); Assert.Equal(new RandomState(3791773014, 24), result.RngState);
    }

    [Fact]
    public void Empty_ranks_take_eight_draws_then_D0_move_only_with_deferred_facing()
    {
        var state = Battle(adjacent: false); var before = Snapshot(state); var result = Select(state); var o = Success(result);
        Assert.Equal(new RandomState(836760821, 19), result.RngState);
        Assert.Equal(new[] { 31051u, 5627u, 23010u, 7419u, 16212u, 4086u, 2749u, 12767u }, o.Lottery.Select(r => r.Raw));
        Assert.Equal(AiLotteryReason.MovementFallback, o.Reason); Assert.Null(o.SelectedRankedIndex); Assert.Null(o.Plan.Action);
        Assert.Equal((BasicSkillAiPlanKind.MoveOnly, Origin, false, true), (o.Plan.Kind, o.Plan.Destination, o.Plan.ActBeforeMove, o.Plan.ChooseFacingAtWait));
        Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Late_lottery_failure_discards_composition_and_exact_budgets_fit()
    {
        for (int draws = 16; draws < 19; draws++)
        {
            var rng = new RandomState(1, ulong.MaxValue - (ulong)draws); var result = Select(Battle(), rng: rng);
            Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(rng, result.RngState);
            Assert.Equal(BasicSkillAiSelectionStage.Lottery, result.Failure!.Stage); Assert.Equal(draws - 16, result.Failure.LotteryIndex);
        }
        var fits = Select(Battle(), rng: new(1, ulong.MaxValue - 19)); Success(fits); Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount);
        var fallbackShort = Select(Battle(adjacent: false), rng: new(1, ulong.MaxValue - 11));
        Assert.False(fallbackShort.Accepted); Assert.Equal(7, fallbackShort.Failure!.LotteryIndex); Assert.Null(fallbackShort.Outcome);
        var fallbackFits = Select(Battle(adjacent: false), rng: new(1, ulong.MaxValue - 12));
        Success(fallbackFits); Assert.Equal(ulong.MaxValue, fallbackFits.RngState.DrawCount);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Terminal_default_and_no_Defend_facts_remain_explicit_and_parent_failure_is_atomic()
    {
        foreach (var p in new[] { Selection with { TerminalThrowStoneEntry = false }, Selection with { NoDefendSupport = false },
            Selection with { FreshOrdinarySelectionDefaults = false } })
        { var result = Select(Battle(), selection: p); Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(Decision, result.RngState); }
        var failed = BasicSkillAiSelection.Evaluate(Battle(), SelfData.Request(), Scenario(), Profile() with { FreshEmptyRankedList = false }, Selection, Decision);
        Assert.False(failed.Accepted); Assert.Equal(BasicSkillAiSelectionStage.Composition, failed.Failure!.Stage);
        Assert.Equal(Decision, failed.RngState); Assert.Null(failed.Outcome);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_teams_actor_identities_and_collection_orders_replay_without_publishing_a_command()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        foreach (bool second in new[] { false, true })
        {
            var state = Battle(second: second); var before = Snapshot(state); var first = Select(state, team: team, second: second); Success(first);
            Assert.Equal(Serialize(first), Serialize(Select(state, team: team, second: second)));
            Assert.Equal(Serialize(first), Serialize(Select(Battle(second: second, reverse: true), team: team, second: second)));
            Assert.Equal(second ? B : A, first.Outcome!.Plan.ActorId); Assert.Equal(before, Snapshot(state));
        }
    }

    private static BasicSkillAiSelectionResult Select(AttackBattleSnapshot state, BasicSkillAiSelectionProfile? selection = null,
        RandomState? rng = null, SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, bool second = false) =>
        BasicSkillAiSelection.Evaluate(state, SelfData.Request(second), Scenario(), Profile(team), selection ?? Selection, rng ?? Decision);
    private static BasicSkillAiSelectionOutcome Success(BasicSkillAiSelectionResult result)
    { Assert.True(result.Accepted, result.Failure?.ToString()); return Assert.IsType<BasicSkillAiSelectionOutcome>(result.Outcome); }
}
