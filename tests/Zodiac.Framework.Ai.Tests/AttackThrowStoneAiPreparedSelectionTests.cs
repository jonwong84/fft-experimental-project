using System.Collections.Immutable;
using Xunit;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiPreparedSelectionTests
{
    private static readonly AttackThrowStoneAiPreparedSelectionProfile Selection = new(true, true, true, true, true);

    [Fact]
    public void Fresh_preparation_prefix_three_Attacks_and_Throw_Stone_feed_one_lottery_with_literal_plans_and_RNG()
    {
        foreach (var (power, kind, actFirst) in new[] { (2, AttackThrowStoneAiPlanKind.Attack, true), (1, AttackThrowStoneAiPlanKind.ThrowStone, false) })
        {
            var result = Select(Battle(), power: power); var outcome = Success(result); var selected = outcome.Selection;
            Assert.Equal(new TilePosition(1, 3, 0), outcome.MovementScenario.Scenario.BaselineDestination);
            Assert.Equal(new TilePosition(1, 4, 0), selected.Plan.Destination);
            Assert.Equal((kind, actFirst), (selected.Plan.Kind, selected.Plan.ActBeforeMove)); Assert.True(selected.Plan.ChooseFacingAtWait);
            Assert.Equal(3, selected.Composition.Attack.Candidates.Length);
            Assert.Equal(new FftRandomState(2111915288, 22), selected.Composition.Movement[^1].RngState);
            Assert.Equal(new FftRandomState(3776134388, 42), selected.LotteryEntryRng);
            Assert.Equal(new AiLotteryRoll(0, 19067, 67, 100, true), Assert.Single(selected.Lottery));
            Assert.Equal(new FftRandomState(3397090333, 43), result.RngState); // 1 refresh + 14 movement + 15 Attack + 5 Stone + 1 lottery.
        }
        var ko = Select(Battle(hp: 10)); var death = Success(ko);
        Assert.Equal(320, death.Selection.Composition.RankedActions[0]!.Score.Priority);
        Assert.Equal(new FftRandomState(1775811865, 47), ko.RngState); // Each of four KO candidates needs two upkeep draws.
    }

    [Fact]
    public void Immobile_prepared_pair_preserves_second_slot_all_failed_and_empty_movement_fallback()
    {
        var second = Select(Battle(move: 0, evade: 75), power: 4, rng: new(10, 7)); var outcome = Success(second).Selection;
        Assert.Equal(1, outcome.SelectedRankedIndex); Assert.Equal(AttackThrowStoneAiPlanKind.ThrowStone, outcome.Plan.Kind);
        Assert.True(outcome.Plan.ActBeforeMove); Assert.Equal(Origin, outcome.Plan.Destination);
        Assert.Equal(new FftRandomState(2148343772, 21), second.RngState);
        var failed = Select(Battle(move: 0, evade: 75), power: 4); outcome = Success(failed).Selection;
        Assert.Equal(AiLotteryReason.AllRollsFailedUseFirst, outcome.Reason); Assert.Equal(AttackThrowStoneAiPlanKind.Attack, outcome.Plan.Kind);
        Assert.Equal(8, outcome.Lottery.Length); Assert.Equal(new FftRandomState(3823029677, 27), failed.RngState);
        var empty = Select(Battle(move: 0, evade: 100)); outcome = Success(empty).Selection;
        Assert.Equal(AiLotteryReason.MovementFallback, outcome.Reason); Assert.Equal(AttackThrowStoneAiPlanKind.MoveOnly, outcome.Plan.Kind);
        Assert.Null(outcome.Plan.Action); Assert.Equal(Origin, outcome.Plan.Destination); Assert.False(outcome.Plan.ActBeforeMove);
        Assert.Equal(8, outcome.Lottery.Length); Assert.Equal(new FftRandomState(3535774167, 25), empty.RngState);
    }

    [Fact]
    public void Owned_pipeline_matches_fresh_public_scenario_and_supplied_selection_chain_without_rescoring_in_production()
    {
        var state = Battle(evade: 75); var prepared = Select(state); var outcome = Success(prepared);
        var scenario = AttackThrowStoneAiMovementScenarioTests.Derive(state); Assert.True(scenario.Accepted);
        var p = Profile(); var afterRefresh = new FftBiosRandom().Next(Decision).State;
        var supplied = AttackThrowStoneAiSelection.Evaluate(state, Request(), scenario.Outcome!.Scenario, new(Sword, 2), p,
            new(true, true, true), afterRefresh);
        Assert.True(supplied.Accepted); Assert.Equal(Serialize(scenario.Outcome), Serialize(outcome.MovementScenario));
        Assert.Equal(Serialize(supplied.Outcome), Serialize(outcome.Selection)); Assert.Equal(supplied.RngState, prepared.RngState);
    }

    [Fact]
    public void Exact_and_short_full_query_budgets_cover_lottery_and_late_composition_failure_without_partial_data()
    {
        foreach (var (move, evade, hp, seed, power, draws, failedSlot) in new[] {
            (3, 0, 50, 1u, 2, 36ul, 0), (3, 0, 10, 1u, 2, 40ul, 0),
            (0, 75, 50, 10u, 4, 14ul, 1), (0, 75, 50, 1u, 4, 20ul, 7), (0, 100, 50, 1u, 2, 18ul, 7) })
        {
            var state = Battle(move: move, evade: evade, hp: hp); var before = Snapshot(state);
            var rng = new FftRandomState(seed, ulong.MaxValue - draws); var fits = Select(state, power: power, rng: rng); Success(fits);
            Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount);
            var shortBudget = rng with { DrawCount = rng.DrawCount + 1 };
            var failure = Atomic(Select(state, power: power, rng: shortBudget), AttackThrowStoneAiPreparedSelectionStage.Selection, shortBudget);
            Assert.Equal(failedSlot, failure.SelectionFailure!.LotteryIndex); Assert.Equal(before, Snapshot(state));
        }
        var stoneBudget = new FftRandomState(1, ulong.MaxValue - 34);
        var stone = Atomic(Select(Battle(), rng: stoneBudget), AttackThrowStoneAiPreparedSelectionStage.Composition, stoneBudget);
        Assert.Equal(AttackThrowStoneAiStage.ThrowStone, stone.CompositionFailure!.Stage);
        var attackBudget = new FftRandomState(1, ulong.MaxValue - 29);
        var attack = Atomic(Select(Battle(), rng: attackBudget), AttackThrowStoneAiPreparedSelectionStage.Composition, attackBudget);
        Assert.Equal(AttackThrowStoneAiStage.Attack, attack.CompositionFailure!.Stage);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Unrepresented_remaining_facts_and_wrong_branches_reject_without_claiming_derived_certificates()
    {
        foreach (var profile in new[] { Selection with { NormalScoring = false }, Selection with { CanonicalCoordinates = false },
            Selection with { NoOtherScenarioOverrides = false }, Selection with { ClearDirectTrajectories = false }, Selection with { NoDefendSupport = false } })
            Assert.Equal(AttackThrowStoneAiPreparedSelectionRejection.UnsupportedProfile,
                Atomic(Select(Battle(), profile: profile), AttackThrowStoneAiPreparedSelectionStage.Admission).Reason);
        var far = Atomic(Select(Battle(target: new(6,1,0))), AttackThrowStoneAiPreparedSelectionStage.Scenario);
        Assert.Equal(AttackThrowStoneAiScenarioRejection.OutOfReach, far.ScenarioFailure!.Reason);
        var team = Atomic(Select(Battle(), team: SwordAiScenarioTeam.Blue), AttackThrowStoneAiPreparedSelectionStage.Scenario);
        Assert.Equal(AttackThrowStoneAiPreparationRejection.UnsupportedTeam, team.ScenarioFailure!.PreparationFailure!.Reason);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_and_collection_policy_orders_replay_plans_without_live_changes()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = Battle(second: second); var before = Snapshot(state); var request = FreshRequest(second);
            var result = Select(state, request: request); var outcome = Success(result);
            Assert.Equal(request.ActorId, outcome.Selection.Plan.ActorId); Assert.IsType<AttackThrowStoneAiAttack>(outcome.Selection.Plan.Action);
            Assert.Equal(Serialize(result), Serialize(Select(state, request: request)));
            Assert.Equal(Serialize(result), Serialize(Select(Battle(second: second, reverse: true),
                request: request with { Policies = request.Policies.Reverse().ToImmutableArray() })));
            Assert.Equal(before, Snapshot(state));
        }
    }

    private static AttackThrowStoneAiPreparedSelectionResult Select(AttackBattleSnapshot state, int power = 2,
        SwordAiRefreshRequest? request = null, AttackThrowStoneAiPreparedSelectionProfile? profile = null,
        SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, FftRandomState? rng = null)
    {
        return AttackThrowStoneAiPreparedSelection.Evaluate(state, new(request ?? FreshRequest(), Stone, new(Sword, power)),
            SelectionConfiguration(profile ?? Selection, team), rng ?? Decision);
    }
    private static AttackThrowStoneAiPreparedSelectionOutcome Success(AttackThrowStoneAiPreparedSelectionResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); return Assert.IsType<AttackThrowStoneAiPreparedSelectionOutcome>(result.Outcome); }
    private static AttackThrowStoneAiPreparedSelectionFailure Atomic(AttackThrowStoneAiPreparedSelectionResult result,
        AttackThrowStoneAiPreparedSelectionStage stage, FftRandomState? rng = null)
    { Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(rng ?? Decision, result.RngState);
        var failure = Assert.IsType<AttackThrowStoneAiPreparedSelectionFailure>(result.Failure); Assert.Equal(stage, failure.Stage); return failure; }
}
