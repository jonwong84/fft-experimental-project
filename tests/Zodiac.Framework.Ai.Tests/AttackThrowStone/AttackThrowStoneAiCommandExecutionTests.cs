using System.Collections.Immutable;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiMoveFirstExecutionTests;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiPostActExecutionTests;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiCommandExecutionTests
{
    [Fact]
    public void One_entry_derives_reach_and_dispatches_all_existing_pre_ACT_shapes_with_literal_streams()
    {
        // FinalRng is the internal state word/cumulative draw count after the published command, from Ready(seed1,count7).
        var cases = new[] {
            new { Name = "In-reach Stone Move-first", State = Ready(), Power = 1, Outside = false,
                Kind = AttackThrowStoneAiCommandKind.Move, FinalRng = new RandomState(3397090333,43) },
            new { Name = "In-reach Attack Move-first", State = Ready(left:true), Power = 5, Outside = false,
                Kind = AttackThrowStoneAiCommandKind.Move, FinalRng = new RandomState(3791773014,24) },
            new { Name = "In-reach empty Move-first", State = Ready(left:true,evade:100), Power = 1, Outside = false,
                Kind = AttackThrowStoneAiCommandKind.Move, FinalRng = new RandomState(2473755763,29) },
            new { Name = "In-reach Attack ACT-first", State = Ready(), Power = 2, Outside = false,
                Kind = AttackThrowStoneAiCommandKind.Attack, FinalRng = new RandomState(995405759,49) },
            new { Name = "In-reach Stone ACT-first", State = Ready(move:0), Power = 1, Outside = false,
                Kind = AttackThrowStoneAiCommandKind.ThrowStone, FinalRng = new RandomState(3823029677,27) },
            new { Name = "Out-of-reach Stone Move-first", State = Ready(move:1,target:new(6,1,0)), Power = 1, Outside = true,
                Kind = AttackThrowStoneAiCommandKind.Move, FinalRng = new RandomState(3210001534,16) },
            new { Name = "Out-of-reach Stone ACT-first", State = Ready(move:0,target:new(4,1,0)), Power = 2, Outside = true,
                Kind = AttackThrowStoneAiCommandKind.ThrowStone, FinalRng = new RandomState(3297241969,23) },
            new { Name = "Out-of-reach empty END", State = Ready(move:0,target:new(6,1,0)), Power = 2, Outside = true,
                Kind = AttackThrowStoneAiCommandKind.EndTurn, FinalRng = new RandomState(180171308,18) } };
        foreach (var example in cases)
        {
            var state = example.State; string before = Snapshot(state.Battle);
            var result = Run(state, example.Power); var o = Success(result);
            Assert.Equal(example.Kind, o.Kind); Assert.Equal(example.Outside, o.Decision.OutOfReach);
            Assert.IsType<AttackThrowStoneAiCommandSelection.BeforeAct>(o.Decision);
            Assert.Equal(example.FinalRng, result.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(example.FinalRng, result.State.Battle.TurnBattle.Battle.RngState);
            Assert.Equal(state.Revision + 1, result.State.Revision);
            Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, result.State.Battle.TurnBattle.Turns.Ticks);
            Assert.Equal(example.Kind == AttackThrowStoneAiCommandKind.Move, result.Continuation is not null);
            Assert.Equal(example.Kind == AttackThrowStoneAiCommandKind.Move, result.Path is not null);
            // Separate pure selectors are comparison oracles only; the authoritative entry never accepts their results.
            var request = new AttackThrowStoneAiSelectionRequest(FreshRequest(), Stone, new(Sword, example.Power));
            AttackThrowStoneAiSelectionOutcome pure; RandomState decisionRng;
            if (example.Outside)
            {
                var selected = AttackThrowStoneAiOutOfReachSelection.Evaluate(state.Battle, request, Config(), state.Battle.TurnBattle.Turns.RngState);
                Assert.True(selected.Accepted); pure = selected.Outcome!.Selection; decisionRng = selected.RngState;
            }
            else
            {
                var selected = AttackThrowStoneAiPreparedSelection.Evaluate(state.Battle, request, Config(), state.Battle.TurnBattle.Turns.RngState);
                Assert.True(selected.Accepted); pure = selected.Outcome!.Selection; decisionRng = selected.RngState;
            }
            Assert.Equal(Serialize(pure), Serialize(((AttackThrowStoneAiCommandSelection.BeforeAct)o.Decision).Selection));
            Assert.Equal(decisionRng, o.DecisionRng);
            Assert.Equal(before, Snapshot(state.Battle));
        }
        var sameTile = AttackThrowStoneAiFallbackExecutionTests.Input(false);
        var ended = Run(sameTile); var end = Success(ended);
        Assert.Equal(AttackThrowStoneAiCommandKind.EndTurn, end.Kind); Assert.False(end.Decision.OutOfReach);
        Assert.Equal(55, ended.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
    }

    [Fact]
    public void Actual_ACT_then_fresh_reentry_derives_changed_reach_and_settles_without_predicted_D1_or_extra_ACT()
    {
        foreach (var example in new[] {
            new { State = Ready(), Power = 2, BeforeOutside = false, AfterOutside = false, Rng = new RandomState(1223573862,72) },
            new { State = Ready(move:0,entryWord:20), Power = 1, BeforeOutside = false, AfterOutside = true, Rng = new RandomState(713082951,38) },
            new { State = Ready(move:0,target:new(4,1,0)), Power = 2, BeforeOutside = true, AfterOutside = true, Rng = new RandomState(1116175964,34) },
            new { State = Ready(move:0,evade:75), Power = 1, BeforeOutside = false, AfterOutside = false, Rng = new RandomState(1448521415,41) } })
        {
            var actual = Run(example.State, example.Power); var first = Success(actual);
            Assert.Equal(example.BeforeOutside, first.Decision.OutOfReach); Assert.Null(actual.Continuation);
            Assert.True(actual.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).ActionTaken);
            string before = Snapshot(actual.State.Battle);
            // Post-ACT skips actions, so it does not require a trajectory assertion for a scan that never runs.
            var config = Config() with { Selection = Selection with { ClearDirectTrajectories = false } };
            var settled = Run(actual.State, example.Power, configuration: config); var second = Success(settled);
            var decision = Assert.IsType<AttackThrowStoneAiCommandSelection.AfterAct>(second.Decision);
            Assert.Equal(example.AfterOutside, decision.OutOfReach);
            Assert.Equal(example.Rng, second.DecisionRng); Assert.Equal(example.Rng, settled.State.Battle.TurnBattle.Turns.RngState);
            Assert.Null(settled.Continuation); Assert.Null(settled.State.PendingFacing);
            Assert.Empty(settled.Events.OfType<AttackResolvedEvent>()); Assert.Empty(settled.Events.OfType<ThrowStoneResolvedEvent>());
            Assert.Single(settled.Events.OfType<FacingSettledEvent>());
            Assert.False(settled.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).HasTurn);
            var request = new AttackThrowStoneAiSelectionRequest(FreshRequest(), Stone, new(Sword, example.Power));
            var pure = example.AfterOutside
                ? AttackThrowStoneAiPostActSelection.EvaluateOutOfReach(actual.State.Battle, request, config, actual.State.Battle.TurnBattle.Turns.RngState)
                : AttackThrowStoneAiPostActSelection.EvaluateInReach(actual.State.Battle, request, config, actual.State.Battle.TurnBattle.Turns.RngState);
            Assert.True(pure.Accepted); Assert.Equal(Serialize(pure.Outcome), Serialize(decision.Selection));
            Assert.Equal(before, Snapshot(actual.State.Battle));
        }
    }

    [Fact]
    public void Exact_and_short_budgets_distinguish_unpublished_decisions_from_already_committed_commands()
    {
        var act = Run(Ready(count:ulong.MaxValue-42), 2); Success(act);
        Assert.Equal(ulong.MaxValue, act.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        var shortAct = Ready(count:ulong.MaxValue-41); var failedAct = Run(shortAct, 2);
        Atomic(shortAct, failedAct, AttackThrowStoneAiCommandRejection.BoundaryRejected);
        Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, failedAct.Execution.Transition.AttackRejection);
        var exactEnd = Run(Ready(move:0,target:new(6,1,0),count:ulong.MaxValue-11)); Success(exactEnd);
        Assert.Equal(ulong.MaxValue, exactEnd.Outcome!.DecisionRng.DrawCount);
        var shortEnd = Ready(move:0,target:new(6,1,0),count:ulong.MaxValue-10);
        Atomic(shortEnd, Run(shortEnd), AttackThrowStoneAiCommandRejection.SelectionRejected);
        var moved = Run(Ready(count:ulong.MaxValue-42), 1); Success(moved);
        var followup = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation);
        Assert.False(followup.Accepted); Assert.Same(moved.State, followup.State); Assert.Empty(followup.Events);
        Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, followup.Transition.AttackRejection);
        var publishedAct = Run(Ready(count:ulong.MaxValue-64), 2); Success(publishedAct);
        var post = Run(publishedAct.State, 2); Atomic(publishedAct.State, post, AttackThrowStoneAiCommandRejection.SelectionRejected);
        Assert.Equal(7, post.Failure!.AfterActFailure!.FailedLotteryIndex);
        Assert.Equal(40, publishedAct.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Unknown_cast_priority_choice_source_context_and_terminal_guards_never_turn_into_another_branch()
    {
        var unknown = Ready(move:1,target:new(4,1,0)); var failed = Run(unknown);
        Atomic(unknown, failed, AttackThrowStoneAiCommandRejection.SelectionRejected);
        Assert.Equal(AttackThrowStoneAiPreparedSelectionRejection.UnwrittenCastPriority, failed.Failure!.BeforeActFailure!.Reason);
        var state = Ready(); Atomic(state, Run(state, dispatch:new()), AttackThrowStoneAiCommandRejection.UnsupportedDispatchContext);
        Atomic(state, Run(state, configuration:Config() with { Selection = Selection with { NoDefendSupport = false } }), AttackThrowStoneAiCommandRejection.SelectionRejected);
        Atomic(state, Run(state, configuration:Config() with { Refresh = Config().Refresh with { FreshRefreshEntry = false } }), AttackThrowStoneAiCommandRejection.SelectionRejected);
        Atomic(state, Run(state, request:FreshRequest(second:true)), AttackThrowStoneAiCommandRejection.BoundaryRejected);
        var moved = Run(state, 1); Success(moved);
        Atomic(moved.State, Run(moved.State), AttackThrowStoneAiCommandRejection.UnavailableChoices);
        var lethal = Run(Ready(move:0,target:new(4,1,0),hp:10)); Success(lethal);
        var terminal = Run(lethal.State); Atomic(lethal.State, terminal, AttackThrowStoneAiCommandRejection.SelectionRejected);
        Assert.Equal(SwordAiRefreshRejection.TerminalInput, terminal.Failure!.AfterActFailure!.ScenarioFailure!.PreparationFailure!.RefreshRejection);
        Assert.Equal(0, lethal.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actors_reordered_inputs_and_exact_continuations_replay_without_live_changes()
    {
        foreach (bool second in new[] { false, true })
        foreach (bool outside in new[] { false, true })
        {
            var request = FreshRequest(second);
            var state = Ready(move:outside ? 1:3, target:outside ? new(6,1,0):null, second:second);
            string before = Snapshot(state.Battle);
            var first = Run(state, 1, request); Success(first); var replay = Run(state, 1, request); Success(replay);
            var reversed = Run(Ready(move:outside ? 1:3,target:outside ? new(6,1,0):null,second:second,reverse:true), 1,
                request with { Policies = request.Policies.Reverse().ToImmutableArray() }); Success(reversed);
            Assert.Equal(Serialize(first.Outcome), Serialize(replay.Outcome)); Assert.Equal(Serialize(first.Outcome), Serialize(reversed.Outcome));
            Assert.Equal(Snapshot(first.State.Battle), Snapshot(reversed.State.Battle));
            var actor = second ? B:A;
            Assert.Equal(FacingMoveRejection.StaleContinuation, FacingMoveDecisionBattle.Continue(replay.State, actor, first.Continuation).Rejection);
            var actual = FacingMoveDecisionBattle.Continue(first.State, actor, first.Continuation);
            var replayActual = FacingMoveDecisionBattle.Continue(reversed.State, actor, reversed.Continuation);
            Assert.True(actual.Accepted); Assert.True(replayActual.Accepted);
            Assert.Equal(Snapshot(actual.State.Battle), Snapshot(replayActual.State.Battle));
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    private static AttackThrowStoneAiSelectionConfiguration Config() => SelectionConfiguration(Selection);
    private static AttackThrowStoneAiCommandResult Run(FacingTurnState state, int power = 2, SwordAiRefreshRequest? request = null,
        AttackThrowStoneAiSelectionConfiguration? configuration = null, SwordAiCommandDispatchProfile? dispatch = null) =>
        AttackThrowStoneAiCommandExecution.Execute(state, new(request ?? FreshRequest(), Stone, new(Sword,power)), configuration ?? Config(), dispatch ?? Dispatch);
    private static AttackThrowStoneAiCommandOutcome Success(AttackThrowStoneAiCommandResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); return Assert.IsType<AttackThrowStoneAiCommandOutcome>(result.Outcome); }
    private static void Atomic(FacingTurnState state, AttackThrowStoneAiCommandResult result, AttackThrowStoneAiCommandRejection reason)
    {
        Assert.False(result.Accepted); Assert.Equal(reason, result.Failure!.Reason); Assert.Same(state, result.State);
        Assert.Empty(result.Events); Assert.Null(result.Path); Assert.Null(result.Continuation); Assert.Null(result.Outcome);
    }
}
