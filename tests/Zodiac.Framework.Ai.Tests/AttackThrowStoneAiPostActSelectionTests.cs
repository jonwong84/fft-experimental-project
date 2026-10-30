using System.Collections.Immutable;
using Xunit;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiMoveFirstExecutionTests;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiPostActPreparationTests;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiPostActScenarioTests;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiPostActSelectionTests
{
    [Fact]
    public void Actual_effects_feed_fresh_scenarios_then_eight_zero_hit_rolls_and_a_pure_deferred_facing_plan()
    {
        // ScenarioRng is the checkpoint after refresh/scoring/reach adjustment; FinalRng adds exactly eight lottery draws.
        // Words are internal RNG state and counts are cumulative. No attack or formula is evaluated during this query.
        var cases = new[] {
            new { Name = "Attack hit / move", State = Act(Ready()).Battle, Outside = false, Power = 2,
                Destination = new TilePosition(1, 4, 0), ScenarioRng = new FftRandomState(2240736910, 64),
                FinalRng = new FftRandomState(1223573862, 72), FirstRoll = 31327u, LastRoll = 18670u },
            new { Name = "Stone hit / stay", State = Act(Ready(move: 0), power: 1).Battle, Outside = false, Power = 1,
                Destination = Origin, ScenarioRng = new FftRandomState(2473755763, 29),
                FinalRng = new FftRandomState(3834121963, 37), FirstRoll = 20495u, LastRoll = 25736u },
            new { Name = "Stone miss / stay", State = Act(Ready(move: 0, evade: 75), power: 1).Battle, Outside = false, Power = 1,
                Destination = Origin, ScenarioRng = new FftRandomState(1969681615, 33),
                FinalRng = new FftRandomState(1448521415, 41), FirstRoll = 17031u, LastRoll = 22102u },
            new { Name = "Stone push / stay", State = Act(Ready(move: 0, entryWord: 20), power: 1).Battle, Outside = true, Power = 1,
                Destination = Origin, ScenarioRng = new FftRandomState(2438815311, 30),
                FinalRng = new FftRandomState(713082951, 38), FirstRoll = 22423u, LastRoll = 10880u },
            new { Name = "Stone far push / move", State = Pushed(move: 3, originalTargetX: 5), Outside = true, Power = 2,
                Destination = new TilePosition(3, 2, 0), ScenarioRng = new FftRandomState(2000392299, 34),
                FinalRng = new FftRandomState(1073193443, 42), FirstRoll = 18463u, LastRoll = 16375u } };
        foreach (var example in cases)
        {
            string before = Snapshot(example.State);
            var result = Select(example.State, example.Outside, weaponPower: example.Power); var o = Success(result);
            Assert.Equal(example.ScenarioRng, o.LotteryEntryRng); Assert.Equal(example.FinalRng, result.RngState);
            Assert.Equal(Enumerable.Range(0, 8), o.Lottery.Select(r => r.Slot));
            Assert.All(o.Lottery, r => { Assert.Equal(0, r.HitPercent); Assert.False(r.Passed); });
            Assert.Equal(example.FirstRoll, o.Lottery[0].Raw); Assert.Equal(example.LastRoll, o.Lottery[^1].Raw);
            Assert.Equal(AiLotteryReason.MovementFallback, o.Reason); Assert.Equal(AttackThrowStoneAiPlanKind.MoveOnly, o.Plan.Kind);
            Assert.Equal(A, o.Plan.ActorId); Assert.Equal(example.Destination, o.Plan.Destination);
            Assert.False(o.Plan.ActBeforeMove); Assert.Null(o.Plan.Action); Assert.True(o.Plan.ChooseFacingAtWait);
            var separate = Evaluate(example.State, example.Outside, weaponPower: example.Power); Assert.True(separate.Accepted);
            Assert.Equal(Serialize(separate.Outcome), Serialize(o.Scenarios)); Assert.Equal(example.ScenarioRng, separate.RngState);
            Assert.Equal(before, Snapshot(example.State));
        }
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Unwritten_in_range_actor_is_unread_while_no_Defend_branch_and_actual_choice_guards_remain_explicit()
    {
        var state = Pushed(move: 1, originalTargetX: 4); string before = Snapshot(state);
        var result = Select(state, outside: true); var o = Success(result);
        Assert.Equal(4, o.Scenarios.TargetDistance); Assert.Null(o.Scenarios.Cells.Single(c => c.Tile == Origin).WrittenPriority);
        Assert.Contains(Origin, o.Scenarios.Scenario1); Assert.Contains(Origin, o.Scenarios.Scenario2);
        Assert.All(o.Scenarios.Preparation.ActorEntries, e => Assert.False(e.Usable));
        Assert.Equal(new TilePosition(2, 1, 0), o.Plan.Destination); Assert.Equal(new FftRandomState(713082951, 38), result.RngState);
        Assert.False(Config().Selection.ClearDirectTrajectories); // No trajectory certificate is manufactured for skipped actions.
        var noDefend = Config() with { Selection = Config().Selection with { NoDefendSupport = false } };
        Assert.Equal(AttackThrowStoneAiPostActSelectionRejection.UnsupportedProfile,
            Atomic(state, Select(state, outside: true, configuration: noDefend)).Reason);
        var badScenario = Config() with { Selection = Config().Selection with { NoOtherScenarioOverrides = false } };
        Assert.Equal(AttackThrowStoneAiScenarioStage.Admission,
            Atomic(state, Select(state, true, configuration: badScenario)).ScenarioFailure!.Stage);
        Assert.Equal(AttackThrowStoneAiScenarioRejection.OutOfReach, Atomic(state, Select(state)).ScenarioFailure!.Reason);
        var notActed = Battle();
        Assert.Equal(SwordAiRefreshRejection.UnavailableAction,
            Atomic(notActed, Select(notActed)).ScenarioFailure!.PreparationFailure!.RefreshRejection);
        var terminal = Act(Ready(move: 0, hp: 10), power: 1).Battle;
        Assert.Equal(SwordAiRefreshRejection.TerminalInput,
            Atomic(terminal, Select(terminal, weaponPower: 1)).ScenarioFailure!.PreparationFailure!.RefreshRejection);
        Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Exact_total_budgets_fit_and_first_or_last_lottery_exhaustion_rolls_back_every_owned_prefix()
    {
        var cases = new[] {
            new { State = Act(Ready()).Battle, Outside = false, Power = 2, ScenarioCost = 15ul },
            new { State = Act(Ready(move: 0), power: 1).Battle, Outside = false, Power = 1, ScenarioCost = 2ul },
            new { State = Act(Ready(move: 0, evade: 75), power: 1).Battle, Outside = false, Power = 1, ScenarioCost = 2ul },
            new { State = Pushed(move: 1, originalTargetX: 4), Outside = true, Power = 2, ScenarioCost = 3ul },
            new { State = Pushed(move: 3, originalTargetX: 5), Outside = true, Power = 2, ScenarioCost = 7ul } };
        foreach (var example in cases)
        {
            string before = Snapshot(example.State); var original = example.State.TurnBattle.Turns.RngState;
            var exact = original with { DrawCount = ulong.MaxValue - example.ScenarioCost - 8 };
            var fits = Select(example.State, example.Outside, example.Power, rng: exact); Success(fits);
            Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount);
            foreach (ulong availableRolls in new[] { 0ul, 7ul })
            {
                var shortRng = original with { DrawCount = ulong.MaxValue - example.ScenarioCost - availableRolls };
                var failure = Atomic(example.State, Select(example.State, example.Outside, example.Power, rng: shortRng), shortRng);
                Assert.Equal(AttackThrowStoneAiPostActSelectionRejection.LotteryRejected, failure.Reason);
                Assert.Equal((int)availableRolls, failure.FailedLotteryIndex);
            }
            var exhausted = original with { DrawCount = ulong.MaxValue };
            Assert.Equal(AttackThrowStoneAiPostActSelectionRejection.ScenariosRejected,
                Atomic(example.State, Select(example.State, example.Outside, example.Power, rng: exhausted), exhausted).Reason);
            Assert.Equal(before, Snapshot(example.State));
        }
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_reach_branches_actor_identities_and_reordered_inputs_replay_without_live_changes()
    {
        foreach (bool second in new[] { false, true })
        foreach (bool outside in new[] { false, true })
        {
            var turn = FreshRequest(second);
            var state = outside ? Pushed(1, 4, second) : Act(Ready(second: second), request: turn).Battle;
            string before = Snapshot(state); var request = new AttackThrowStoneAiSelectionRequest(turn, Stone, new(Sword, 2));
            var first = Select(state, outside, request: request); Success(first);
            var replay = Select(state, outside, request: request); Success(replay);
            var reversedTurn = turn with { Policies = turn.Policies.Reverse().ToImmutableArray() };
            var reversed = outside ? Pushed(1, 4, second, reverse: true) : Act(Ready(second: second, reverse: true), request: reversedTurn).Battle;
            var reordered = Select(reversed, outside, request: request with { Turn = reversedTurn }); Success(reordered);
            Assert.Equal(Serialize(first), Serialize(replay)); Assert.Equal(Serialize(first), Serialize(reordered));
            Assert.Equal(before, Snapshot(state));
        }
    }

    private static AttackThrowStoneAiSelectionConfiguration Config() => Configuration() with {
        Selection = Configuration().Selection with { NoDefendSupport = true } };
    private static AttackThrowStoneAiPostActSelectionResult Select(AttackBattleSnapshot state, bool outside = false, int weaponPower = 2,
        AttackThrowStoneAiSelectionRequest? request = null, AttackThrowStoneAiSelectionConfiguration? configuration = null, FftRandomState? rng = null) =>
        outside ? AttackThrowStoneAiPostActSelection.EvaluateOutOfReach(state, request ?? new(FreshRequest(), Stone, new(Sword, weaponPower)),
            configuration ?? Config(), rng ?? state.TurnBattle.Turns.RngState) :
            AttackThrowStoneAiPostActSelection.EvaluateInReach(state, request ?? new(FreshRequest(), Stone, new(Sword, weaponPower)),
                configuration ?? Config(), rng ?? state.TurnBattle.Turns.RngState);
    private static AttackThrowStoneAiPostActSelectionOutcome Success(AttackThrowStoneAiPostActSelectionResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); return Assert.IsType<AttackThrowStoneAiPostActSelectionOutcome>(result.Outcome); }
    private static AttackThrowStoneAiPostActSelectionFailure Atomic(AttackBattleSnapshot state, AttackThrowStoneAiPostActSelectionResult result, FftRandomState? rng = null)
    { Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(rng ?? state.TurnBattle.Turns.RngState, result.RngState); return Assert.IsType<AttackThrowStoneAiPostActSelectionFailure>(result.Failure); }
}
