using System.Collections.Immutable;
using Xunit;
using Zodiac.Framework.Random;
using SelfData = Zodiac.Framework.Ai.Tests.AccumulateAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class BasicSkillAiTurnExecutionTests
{
    private static readonly SwordAiCommandDispatchProfile Dispatch = new(true, true, true, true);
    private static readonly TilePosition Near = new(2, 1, 0);
    private static ThrowStoneAiScanScenario AttackScenario => new([Origin, Near], [Origin, Near], Origin, Near);
    private static ThrowStoneAiScanScenario EmptyMoveScenario => new([Origin, Far], [Origin, Far], Far, Far);
    private static ThrowStoneAiScanScenario OriginScenario => new([Origin], [Origin], Origin, Origin);
    private static BasicSkillAiTurnConfiguration Configuration(SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue) =>
        new(Profile(team), new(true, true, true), new(true, true, true));
    private static BasicSkillAiTurnResult Run(FacingTurnState state, ThrowStoneAiScanScenario? scenario = null,
        BasicSkillAiTurnConfiguration? configuration = null, AccumulateAiPreparationRequest? request = null,
        SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, bool second = false) =>
        BasicSkillAiTurnExecution.Execute(state, request ?? SelfData.Request(second), scenario ?? Scenario(), configuration ?? Configuration(team), Dispatch);
    private static FacingTurnState Self(FacingTurnState state, bool second = false)
    {
        var result = FacingTurnBattle.UseImmediateAbility(state, new(second ? B : A, SelfData.Self), SelfData.Profile().Ability);
        Assert.True(result.Accepted); return result.State;
    }

    [Fact]
    public void Automatic_post_ACT_routing_matches_each_explicit_branch_and_owns_exactly_one_refresh()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        foreach (bool outside in new[] { false, true })
        {
            var state = Self(Ready(move: outside ? 0 : 3, target: outside ? new(7, 7, 0) : new(2, 1, 0)));
            var c = new BasicSkillAiPostActConfiguration(SelfData.Configuration(team), new(true, true, true), true, true, true);
            var automatic = BasicSkillAiPostActSelection.Evaluate(state.Battle, SelfData.Request(), c, new(1, 7));
            var explicitResult = outside
                ? BasicSkillAiPostActSelection.EvaluateOutOfReach(state.Battle, SelfData.Request(), c, new(1, 7))
                : BasicSkillAiPostActSelection.EvaluateInReach(state.Battle, SelfData.Request(), c, new(1, 7));
            Assert.True(automatic.Accepted); Assert.True(explicitResult.Accepted);
            Assert.Equal(Serialize(explicitResult.Outcome), Serialize(automatic.Outcome)); Assert.Equal(explicitResult.RngState, automatic.RngState);
            Assert.Equal(outside, automatic.Outcome!.OutOfReach is not null);
            Assert.Equal(new RandomState(outside ? team == SwordAiScenarioTeam.NonBlue ? 180171308u : 267834847u : 1343201072u,
                outside ? team == SwordAiScenarioTeam.NonBlue ? 18ul : 17ul : 30ul), automatic.RngState);
            var executed = BasicSkillAiPostActExecution.Execute(state, SelfData.Request(), c, Dispatch);
            var old = outside ? BasicSkillAiPostActExecution.ExecuteOutOfReach(state, SelfData.Request(), c, Dispatch)
                : BasicSkillAiPostActExecution.ExecuteInReach(state, SelfData.Request(), c, Dispatch);
            Assert.True(executed.Accepted); Assert.True(old.Accepted); Assert.Equal(Serialize(old.Outcome), Serialize(executed.Outcome));
            Assert.Equal(Snapshot(old.State.Battle), Snapshot(executed.State.Battle)); Assert.Equal(Serialize(old.Events), Serialize(executed.Events));
        }
    }

    [Fact]
    public void Automatic_reach_uses_the_actual_push_and_the_prepared_reach_before_any_halving_roll()
    {
        var ready = Ready(move: 0, evade: 0, word: 2711922929, count: 20);
        var stone = FacingTurnBattle.UseTargetedAbility(ready, new(A, Stone, B), CandidateProfile().Ability);
        Assert.True(stone.Accepted); Assert.Single(stone.Events.OfType<ThrowStoneKnockbackEvent>());
        var c = new BasicSkillAiPostActConfiguration(SelfData.Configuration(), new(true, true, true), true, true, true);
        var result = BasicSkillAiPostActSelection.Evaluate(stone.State.Battle, SelfData.Request(), c, stone.State.Battle.TurnBattle.Turns.RngState);
        Assert.True(result.Accepted); Assert.NotNull(result.Outcome!.OutOfReach);
        Assert.Equal(1, result.Outcome.Preparation.Common.EnemyAbilityReach); Assert.Equal(2, result.Outcome.MovementScenario.TargetDistance);
        Assert.Equal(new TilePosition(3, 1, 0), result.Outcome.Preparation.Common.FocusPosition);
        Assert.Equal(Origin, result.Outcome.Plan.Destination);
    }

    [Fact]
    public void Complete_self_Attack_Stone_and_empty_turns_preserve_literal_streams_choices_and_event_order()
    {
        var examples = new[] {
            new { Name="Self ACT-first", State=Ready(), Scenario=Scenario(), Kind=AiCommandKind.Accumulate, Rng=new RandomState(995405759,49), Ct=15, Pa=6, Hp=50, Decisions=2 },
            new { Name="Attack ACT-first", State=Ready(evade:0), Scenario=Scenario(), Kind=AiCommandKind.Attack, Rng=new RandomState(2058258651,53), Ct=15, Pa=5, Hp=40, Decisions=2 },
            new { Name="Stone ACT-first", State=Ready(evade:0,target:new(4,1,0)), Scenario=OriginScenario, Kind=AiCommandKind.ThrowStone, Rng=new RandomState(995405759,49), Ct=15, Pa=5, Hp=45, Decisions=2 },
            new { Name="Stone Move-first", State=Ready(evade:0,target:new(4,1,0)), Scenario=Scenario(), Kind=AiCommandKind.Move, Rng=new RandomState(3791773014,24), Ct=15, Pa=5, Hp=40, Decisions=1 },
            new { Name="Attack Move-first", State=Ready(evade:0,target:new(3,1,0)), Scenario=AttackScenario, Kind=AiCommandKind.Move, Rng=new RandomState(1343201072,30), Ct=15, Pa=5, Hp=40, Decisions=1 },
            new { Name="Empty Move-first", State=Ready(target:new(7,7,0)), Scenario=EmptyMoveScenario, Kind=AiCommandKind.Move, Rng=new RandomState(836760821,19), Ct=35, Pa=5, Hp=50, Decisions=1 },
            new { Name="Same-tile END", State=Ready(target:new(7,7,0)), Scenario=Scenario(), Kind=AiCommandKind.End, Rng=new RandomState(836760821,19), Ct=55, Pa=5, Hp=50, Decisions=1 },
            new { Name="At-cap self END", State=Ready(move:0,pa:99,word:5), Scenario=new ThrowStoneAiScanScenario([Origin],[],Origin,Origin), Kind=AiCommandKind.Accumulate, Rng=new RandomState(736722860,22), Ct=35, Pa=99, Hp=50, Decisions=2 }
        };
        foreach (var e in examples)
        {
            var before = Snapshot(e.State.Battle); var result = Run(e.State, e.Scenario); Success(result);
            Assert.Equal(e.Decisions, result.Decisions.Length);
            Assert.Equal(e.Kind, Assert.IsType<BasicSkillAiTurnDecision.BeforeAct>(result.Decisions[0]).Outcome.Kind);
            Assert.Equal(e.Rng, result.State.Battle.TurnBattle.Turns.RngState); Assert.Equal(e.Rng, result.State.Battle.TurnBattle.Battle.RngState);
            Assert.Equal(e.Ct, result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
            Assert.Equal(e.Pa, result.State.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
            Assert.Equal(e.Hp, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.Single(result.Events.OfType<FacingSettledEvent>()); Assert.IsType<FacingSettledEvent>(result.Events[^1]);
            Assert.Equal(e.State.Battle.TurnBattle.Turns.Ticks, result.State.Battle.TurnBattle.Turns.Ticks); Assert.Equal(before, Snapshot(e.State.Battle));
            if (e.Decisions == 2) Assert.IsType<BasicSkillAiTurnDecision.AfterAct>(result.Decisions[1]);
        }
    }

    [Fact]
    public void Complete_output_matches_manual_composition_of_existing_authoritative_commands()
    {
        foreach (var (state, scenario) in new[] { (Ready(), Scenario()), (Ready(evade: 0, target: new(4, 1, 0)), Scenario()),
            (Ready(target: new(7, 7, 0)), EmptyMoveScenario) })
        {
            var first = BasicSkillAiCommandExecution.Execute(state, SelfData.Request(), scenario, Profile(), new(true, true, true), Dispatch);
            Assert.True(first.Accepted); var current = first.State; var events = first.Events;
            if (first.Continuation is not null)
            {
                var retained = FacingMoveDecisionBattle.Continue(current, A, first.Continuation); Assert.True(retained.Accepted);
                current = retained.State; events = events.AddRange(retained.Events);
                if (current.PendingFacing is not null)
                {
                    var facing = FacingTurnBattle.FinishFacing(current, A, current.PendingFacing, new FacingChoice.ChooseClosedEnemy(B));
                    Assert.True(facing.Accepted); current = facing.State; events = events.AddRange(facing.Events);
                }
            }
            else
            {
                var post = BasicSkillAiPostActExecution.Execute(current, SelfData.Request(),
                    new(SelfData.Configuration(), new(true, true, true), true, true, true), Dispatch);
                Assert.True(post.Accepted); current = post.State; events = events.AddRange(post.Events);
            }
            var complete = Run(state, scenario); Success(complete);
            Assert.Equal(Snapshot(current.Battle), Snapshot(complete.State.Battle)); Assert.Equal(current.Revision, complete.State.Revision);
            Assert.Equal(Serialize(events), Serialize(complete.Events));
        }
    }

    [Fact]
    public void Exact_whole_turn_budgets_fit_without_settlement_draws_or_next_clock_ticks()
    {
        foreach (var (state, scenario) in new[] {
            (Ready(count:ulong.MaxValue-42),Scenario()),
            (Ready(count:ulong.MaxValue-17,evade:0,target:new(4,1,0)),Scenario()),
            (Ready(count:ulong.MaxValue-23,evade:0,target:new(3,1,0)),AttackScenario),
            (Ready(count:ulong.MaxValue-12,target:new(7,7,0)),EmptyMoveScenario) })
        {
            var result = Run(state, scenario); Success(result);
            Assert.Equal(ulong.MaxValue, result.State.Battle.TurnBattle.Turns.RngState.DrawCount);
            Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, result.State.Battle.TurnBattle.Turns.Ticks);
        }
    }

    [Fact]
    public void First_command_rejection_publishes_nothing_and_resume_retries_only_that_frozen_stage()
    {
        var state = Ready(count: ulong.MaxValue - 1); var result = Run(state);
        Assert.False(result.Completed); Assert.Same(state, result.State); Assert.Empty(result.Events); Assert.Empty(result.Decisions);
        Assert.Equal(AiTurnStage.FirstCommand, result.Failure!.Stage); Assert.NotNull(result.Failure.FirstFailure);
        var retry = BasicSkillAiTurnExecution.Resume(state, result.Recovery!);
        Assert.False(retry.Completed); Assert.Same(state, retry.State); Assert.Empty(retry.Events); Assert.Empty(retry.Decisions);
        Assert.Equal(result.Failure.Stage, retry.Failure!.Stage);
    }

    [Fact]
    public void Retained_ACT_and_post_ACT_failures_preserve_published_progress_without_replaying_events()
    {
        foreach (bool self in new[] { false, true })
        {
            var input = self ? Ready(count: ulong.MaxValue - 41) : Ready(evade: 0, target: new(4, 1, 0), count: ulong.MaxValue - 16);
            var result = Run(input); Assert.False(result.Completed); Assert.NotSame(input, result.State);
            Assert.Single(result.Decisions); Assert.NotEmpty(result.Events);
            Assert.Equal(self ? AiTurnStage.PostActCommand : AiTurnStage.RetainedCommand, result.Failure!.Stage);
            Assert.Equal(self ? 6 : 5, result.State.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
            Assert.Equal(50, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            var actor = result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A);
            Assert.Equal(self, actor.ActionTaken); Assert.Equal(!self, actor.MovementTaken);
            var before = Snapshot(result.State.Battle); var retry = BasicSkillAiTurnExecution.Resume(result.State, result.Recovery!);
            Assert.False(retry.Completed); Assert.Same(result.State, retry.State); Assert.Empty(retry.Events); Assert.Empty(retry.Decisions);
            Assert.Equal(result.Failure.Stage, retry.Failure!.Stage); Assert.Equal(before, Snapshot(result.State.Battle));
            Assert.Equal(result.Failure.Stage, retry.Recovery!.NextStep);
            var replay = Run(input); Assert.False(replay.Completed); Assert.Equal(before, Snapshot(replay.State.Battle));
            var foreign = BasicSkillAiTurnExecution.Resume(replay.State, result.Recovery!);
            Assert.Equal(AiTurnStage.RecoveryBinding, foreign.Failure!.Stage); Assert.Same(replay.State, foreign.State);
            Assert.Empty(foreign.Events); Assert.Empty(foreign.Decisions); Assert.Null(foreign.Recovery);
        }
    }

    [Fact]
    public void Late_facing_failures_retain_the_exact_receipt_or_the_published_self_ACT_at_the_right_stage()
    {
        foreach (bool self in new[] { false, true })
        {
            var ready = self ? Ready() : Ready(evade: 0, target: new(4, 1, 0));
            var input = new FacingTurnState(ready.Battle, ready.Profile, ulong.MaxValue - 2, null, ready.Lineage);
            var result = Run(input); Assert.False(result.Completed);
            Assert.Equal(self ? AiTurnStage.PostActCommand : AiTurnStage.FinishFacing, result.Failure!.Stage);
            if (self)
            {
                Assert.Null(result.State.PendingFacing); Assert.Equal(6, result.State.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
                Assert.Equal(new RandomState(3794901700, 26), result.State.Battle.TurnBattle.Turns.RngState);
                Assert.Equal(Origin, result.State.Battle.TurnBattle.Battle.Units.Single(u => u.Id == A).Position);
            }
            else
            {
                Assert.NotNull(result.State.PendingFacing); Assert.Equal(40, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
                Assert.Equal(FacingTurnRejection.RevisionExhausted, result.Failure.FacingFailure!.FacingRejection);
            }
            var retry = BasicSkillAiTurnExecution.Resume(result.State, result.Recovery!);
            Assert.False(retry.Completed); Assert.Same(result.State, retry.State); Assert.Empty(retry.Events); Assert.Empty(retry.Decisions);
            Assert.Same(result.State.PendingFacing, retry.State.PendingFacing);
        }
    }

    [Fact]
    public void Lethal_actual_ACT_is_preserved_at_post_ACT_or_facing_boundary_without_inventing_terminal_policy()
    {
        foreach (bool moveFirst in new[] { false, true })
        {
            var state = Ready(evade: 0, target: moveFirst ? new(3, 1, 0) : new(2, 1, 0));
            var result = Run(state, moveFirst ? AttackScenario : Scenario(), request: SelfData.Request() with { Weapon = new(SelfData.SwordId, 10) });
            Assert.False(result.Completed); Assert.Equal(0, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.Single(result.Events.OfType<AttackResolvedEvent>()); Assert.Empty(result.Events.OfType<FacingSettledEvent>());
            Assert.Equal(moveFirst ? AiTurnStage.FinishFacing : AiTurnStage.PostActCommand, result.Failure!.Stage);
            if (moveFirst) { Assert.NotNull(result.State.PendingFacing); Assert.Equal(FacingTurnRejection.NoLivingEnemy, result.Failure.FacingFailure!.FacingRejection); }
            else Assert.Equal(AccumulateAiCandidateRejection.TerminalInput, result.Failure.PostActFailure!.Selection!.Preparation!.AccumulateReason);
            var retry = BasicSkillAiTurnExecution.Resume(result.State, result.Recovery!);
            Assert.False(retry.Completed); Assert.Same(result.State, retry.State); Assert.Empty(retry.Events); Assert.Empty(retry.Decisions);
        }
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_teams_identities_and_collection_policy_orders_replay_complete_turns_from_unchanged_input()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        foreach (bool second in new[] { false, true })
        foreach (bool moveFirst in new[] { false, true })
        {
            var state = Ready(evade: moveFirst ? 0 : 97, target: moveFirst ? new(4, 1, 0) : new(2, 1, 0), second: second);
            var before = Snapshot(state.Battle); var result = Run(state, team: team, second: second); Success(result);
            var reverse = Run(Ready(evade: moveFirst ? 0 : 97, target: moveFirst ? new(4, 1, 0) : new(2, 1, 0), second: second, reverse: true),
                team: team, second: second, request: SelfData.Request(second) with { Turn = SelfData.RefreshRequest(second) with { Policies = SelfData.RefreshRequest(second).Policies.Reverse().ToImmutableArray() } });
            Success(reverse); Assert.Equal(Snapshot(result.State.Battle), Snapshot(reverse.State.Battle)); Assert.Equal(Serialize(result.Events), Serialize(reverse.Events));
            Assert.Equal(result.Decisions.Select(d => Serialize(d)), reverse.Decisions.Select(d => Serialize(d))); Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    private static void Success(BasicSkillAiTurnResult result)
    {
        Assert.True(result.Completed, result.Failure?.ToString()); Assert.Null(result.Failure); Assert.Null(result.Recovery);
        Assert.Null(result.State.PendingFacing); Assert.Null(result.State.Battle.TurnBattle.Turns.ActiveUnitId);
    }
}
