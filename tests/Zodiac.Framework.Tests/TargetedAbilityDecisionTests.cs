using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class TargetedAbilityDecisionTests
{
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly AssetId Stone = new("sample:ability/throw-stone");
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");

    [Fact]
    public void Fresh_planner_adopts_only_RNG_then_uses_real_current_PA_damage_push_or_four_draw_miss()
    {
        foreach (var (pa, evade, hit, damage, pushed, word, count) in new[] {
            (5, 0, true, 5, true, 3297241969u, 23ul), (6, 0, true, 6, true, 3297241969u, 23ul),
            (5, 100, false, 0, false, 595337866u, 20ul) })
        {
            var state = Ready(pa: pa, evade: evade); string before = Snapshot(state); int calls = 0;
            var result = FacingDecisionBattle.Execute(state, A, context => {
                calls++; Assert.Same(state.Battle, context.Battle);
                var planned = PlanRng(context.RngState); Assert.Equal(new FftRandomState(3210001534, 16), planned);
                return context.PrepareTargetedAbility(new(A, Stone, B), Profile(), planned, "fresh");
            });
            Assert.True(result.Accepted); Assert.Equal(1, calls); Assert.Equal("fresh", result.Diagnostic);
            Assert.Equal(new ThrowStoneResolvedEvent(A, Stone, B, hit, damage, pushed), Assert.Single(result.Events.OfType<ThrowStoneResolvedEvent>()));
            Assert.Equal(50 - damage, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.Equal(new TilePosition(pushed ? 7 : 6, 1, 0), Position(result.State, B));
            Assert.Equal(new FftRandomState(word, count), result.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(result.State.Battle.TurnBattle.Turns.RngState, result.State.Battle.TurnBattle.Battle.RngState);
            Assert.Equal(state.Revision + 1, result.State.Revision); Assert.Null(result.State.PendingFacing);
            var actor = result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A);
            Assert.True(actor.ActionTaken); Assert.False(actor.MovementTaken); Assert.Equal(15, actor.Ct);
            Assert.Equal(A, result.State.Battle.TurnBattle.Turns.ActiveUnitId);
            Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, result.State.Battle.TurnBattle.Turns.Ticks);
            Assert.Equal(state.Battle.TurnBattle.Turns.Units.Single(u => u.Id == B), result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == B));
            Assert.Equal(before, Snapshot(state));
        }
    }

    [Fact]
    public void Already_moved_ACT_receipt_binds_final_push_KO_or_blocked_push_and_facing_adds_no_refund_or_draw()
    {
        foreach (var (hp, targetX, pushed) in new[] { (50, 6, true), (5, 6, false), (50, 7, false) })
        {
            var state = Moved(Ready(hp: hp, target: new(targetX, 1, 0))); string before = Snapshot(state);
            var result = Cast(state); Assert.True(result.Accepted);
            var receipt = Assert.IsType<FacingCompletionReceipt>(result.State.PendingFacing);
            Assert.Same(result.State.Battle, receipt.ExpectedBattle); Assert.Equal(result.State.Revision, receipt.Revision);
            Assert.Equal(hp - 5, receipt.ExpectedBattle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.Equal(new TilePosition(pushed ? 7 : targetX, 1, 0), Position(result.State, B));
            Assert.Equal(new FftRandomState(3297241969, 23), result.State.Battle.TurnBattle.Turns.RngState);
            Assert.Null(result.State.Battle.TurnBattle.Turns.ActiveUnitId);
            Assert.Equal(15, result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
            Assert.Equal(hp == 5, result.State.Battle.Combatants.Single(u => u.UnitId == B).Status == AttackUnitStatus.Dead);
            // A replay has distinct session/receipt identity even when its resulting values match.
            var replay = Cast(state); Assert.True(replay.Accepted);
            Assert.Equal(FacingTurnRejection.StaleCompletion,
                FacingTurnBattle.FinishFacing(replay.State, A, receipt, new FacingChoice.KeepCurrent()).FacingRejection);
            var finished = FacingTurnBattle.FinishFacing(result.State, A, receipt, new FacingChoice.Fixed(BattleFacing.North));
            Assert.True(finished.Accepted); Assert.Null(finished.State.PendingFacing);
            Assert.Equal(result.State.Revision + 1, finished.State.Revision);
            Assert.Equal(result.State.Battle.TurnBattle.Turns.RngState, finished.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(result.State.Battle.TurnBattle.Turns, finished.State.Battle.TurnBattle.Turns);
            Assert.Single(finished.Events); Assert.IsType<FacingSettledEvent>(finished.Events[0]);
            Assert.Equal(before, Snapshot(state));
        }
    }

    [Fact]
    public void Exact_actual_budgets_fit_and_late_command_failure_discards_adoption_without_rewinding_prior_Move()
    {
        foreach (var (evade, actualDraws) in new[] { (0, 7ul), (100, 4ul) })
        foreach (bool moved in new[] { false, true })
        {
            var state = Ready(count: ulong.MaxValue - 1 - 8 - actualDraws, evade: evade);
            if (moved) state = Moved(state);
            var result = Cast(state); Assert.True(result.Accepted);
            Assert.Equal(ulong.MaxValue, result.State.Battle.TurnBattle.Turns.RngState.DrawCount);
            Assert.Equal(ulong.MaxValue, result.State.Battle.TurnBattle.Battle.RngState!.Value.DrawCount);
            var shortState = Ready(count: ulong.MaxValue - 8 - actualDraws, evade: evade);
            if (moved) shortState = Moved(shortState);
            string before = Snapshot(shortState);
            var failed = Cast(shortState); Atomic(shortState, failed, FacingDecisionRejection.CommandRejected);
            Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, failed.Transition.AttackRejection);
            Assert.Equal(before, Snapshot(shortState));
            Assert.Equal(new TilePosition(moved ? 3 : 2, 1, 0), Position(failed.State, A));
            Assert.Equal(moved, failed.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).MovementTaken);
        }
    }

    [Fact]
    public void Wrong_actor_and_invalid_ability_grant_range_or_spent_ACT_publish_no_adopted_state()
    {
        var state = Ready();
        var wrong = Cast(state, command: new(B, Stone, A)); Atomic(state, wrong, FacingDecisionRejection.AuthorityRejected);
        Assert.Equal(FacingTurnRejection.WrongActor, wrong.Transition.FacingRejection);
        foreach (var (command, profile, reason) in new[] {
            (new UseTargetedAbilityCommand(A, new("sample:ability/unknown"), B), Profile(), AttackCommandRejection.UnknownAbility),
            (new UseTargetedAbilityCommand(A, Stone, B), Profile(granted: false), AttackCommandRejection.AbilityNotGranted),
            (new UseTargetedAbilityCommand(A, Stone, A), Profile(), AttackCommandRejection.SelfTarget),
            (new UseTargetedAbilityCommand(A, Stone, new("sample:unit/missing")), Profile(), AttackCommandRejection.UnknownTarget),
            (new UseTargetedAbilityCommand(A, Stone, B), Profile(ground: new("sample:terrain/other")), AttackCommandRejection.UnsupportedSetup) })
        {
            var failed = Cast(state, command: command, profile: profile); Atomic(state, failed, FacingDecisionRejection.CommandRejected);
            Assert.Equal(reason, failed.Transition.AttackRejection);
        }
        var far = Ready(target: new(7, 1, 0)); var range = Cast(far); Atomic(far, range, FacingDecisionRejection.CommandRejected);
        Assert.Equal(AttackCommandRejection.OutOfRange, range.Transition.AttackRejection);
        var acted = Cast(state); Assert.True(acted.Accepted);
        var twice = Cast(acted.State); Atomic(acted.State, twice, FacingDecisionRejection.CommandRejected);
        Assert.Equal(AttackCommandRejection.ActionAlreadyTaken, twice.Transition.AttackRejection);
    }

    [Fact]
    public void Targeted_proposals_remain_invocation_bound_contexts_close_and_invalid_RNG_never_reaches_combat()
    {
        var state = Ready(); FacingDecisionContext? expired = null; FacingDecision<string>? saved = null;
        var first = FacingDecisionBattle.Execute(state, A, c => {
            expired = c; saved = c.PrepareTargetedAbility(new(A, Stone, B), Profile(), PlanRng(c.RngState), "saved"); return saved;
        });
        Assert.True(first.Accepted);
        Atomic(state, FacingDecisionBattle.Execute(state, A, _ => saved!), FacingDecisionRejection.InvalidDecision);
        Assert.Throws<InvalidOperationException>(() => expired!.PrepareTargetedAbility(new(A, Stone, B), Profile(), PlanRng(expired.RngState), "closed"));
        FacingDecisionContext? throwing = null;
        Assert.Throws<InvalidOperationException>(() => FacingDecisionBattle.Execute<string>(state, A, c => {
            throwing = c; throw new InvalidOperationException("planner failed");
        }));
        Assert.Throws<InvalidOperationException>(() => throwing!.PrepareTargetedAbility(new(A, Stone, B), Profile(), throwing.RngState, "closed"));
        var rng = state.Battle.TurnBattle.Turns.RngState;
        foreach (var bad in new[] { rng with { Value = rng.Value ^ 1 }, rng with { DrawCount = rng.DrawCount - 1 },
            PlanRng(rng) with { Value = PlanRng(rng).Value ^ 1 } })
            Atomic(state, FacingDecisionBattle.Execute(state, A, c => c.PrepareTargetedAbility(new(A, Stone, B), Profile(), bad, "bad")),
                FacingDecisionRejection.InvalidRandomProgress);
    }

    [Fact]
    public void Revision_and_pending_facing_guards_run_before_planning_while_ACT_and_facing_have_separate_budgets()
    {
        var original = Moved(Ready()); var last = new FacingTurnState(original.Battle, original.Profile, ulong.MaxValue - 1, null, original.Lineage);
        var act = Cast(last); Assert.True(act.Accepted); Assert.Equal(ulong.MaxValue, act.State.Revision);
        Assert.Equal(FacingTurnRejection.RevisionExhausted,
            FacingTurnBattle.FinishFacing(act.State, A, act.State.PendingFacing, new FacingChoice.KeepCurrent()).FacingRejection);
        var max = new FacingTurnState(original.Battle, original.Profile, ulong.MaxValue, null, original.Lineage);
        foreach (var (state, reason) in new[] { (max, FacingTurnRejection.RevisionExhausted),
            (Cast(original).State, FacingTurnRejection.FacingPending) })
        {
            int calls = 0;
            var failed = FacingDecisionBattle.Execute(state, A, c => { calls++; return c.PrepareTargetedAbility(new(A, Stone, B), Profile(), c.RngState, "private"); });
            Atomic(state, failed, FacingDecisionRejection.AuthorityRejected); Assert.Equal(0, calls);
            Assert.Equal(reason, failed.Transition.FacingRejection);
        }
        var direct = FacingTurnBattle.UseTargetedAbility(original, new(A, Stone, B), Profile());
        Assert.True(direct.Accepted); Assert.Equal(original.Revision + 1, direct.State.Revision);
        Assert.Same(direct.State.Battle, direct.State.PendingFacing!.ExpectedBattle);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_and_collection_orders_replay_the_same_commands_without_mutating_inputs()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = Ready(second: second); string before = Snapshot(state);
            var result = Cast(state); Assert.True(result.Accepted);
            Assert.Equal(Snapshot(result.State), Snapshot(Cast(state).State));
            Assert.Equal(Snapshot(result.State), Snapshot(Cast(Ready(second: second, reverse: true)).State));
            Assert.Equal(before, Snapshot(state));
        }
    }

    private static TargetedAbilityBattleProfile Profile(bool granted = true, TerrainSurfaceId? ground = null) =>
        new(ground ?? Ground, [new(Stone)], [new(A, granted ? [Stone] : [], 0), new(B, granted ? [Stone] : [], 0)]);
    private static FftRandomState PlanRng(FftRandomState rng)
    { for (int i = 0; i < 8; i++) rng = new FftBiosRandom().Next(rng).State; return rng; }
    private static FacingDecisionResult<string> Cast(FacingTurnState state, UseTargetedAbilityCommand? command = null,
        TargetedAbilityBattleProfile? profile = null)
    {
        var actor = state.Battle.TurnBattle.Turns.ActiveUnitId!.Value; var target = state.Battle.Combatants.Single(u => u.UnitId != actor).UnitId;
        return FacingDecisionBattle.Execute(state, actor, c => c.PrepareTargetedAbility(command ?? new(actor, Stone, target),
            profile ?? Profile(), PlanRng(c.RngState), "fresh"));
    }
    private static void Atomic(FacingTurnState state, FacingDecisionResult<string> result, FacingDecisionRejection reason)
    { Assert.False(result.Accepted); Assert.Equal(reason, result.Rejection); Assert.Same(state, result.State);
        Assert.Empty(result.Events); Assert.Null(result.Diagnostic); Assert.Null(result.Path); }
    private static TilePosition Position(FacingTurnState state, BattleUnitId id) => state.Battle.TurnBattle.Battle.Units.Single(u => u.Id == id).Position;
    private static string Snapshot(FacingTurnState state) => JsonSerializer.Serialize(new {
        state.Revision, Combat = state.Battle.Combatants.OrderBy(u => u.UnitId.Value),
        Turns = state.Battle.TurnBattle.Turns.Units.OrderBy(u => u.Id.Value), state.Battle.TurnBattle.Turns.Phase,
        state.Battle.TurnBattle.Turns.RngState, state.Battle.TurnBattle.Turns.Ticks,
        BattleRng = state.Battle.TurnBattle.Battle.RngState, Bodies = state.Battle.TurnBattle.Battle.Units.OrderBy(u => u.Id.Value)
            .Select(u => new { u.Id, u.Position, u.HasMoved, u.Move }) });
    private static FacingTurnState Moved(FacingTurnState state)
    { var moved = FacingTurnPathMovement.MoveAlongPath(state, new(A, new(3, 1, 0)), new(Ground, A, B)); Assert.True(moved.Accepted); return moved.State; }
    private static FacingTurnState Ready(ulong count = 7, int pa = 5, int evade = 0, int hp = 50,
        TilePosition? target = null, bool second = false, bool reverse = false)
    {
        var actor = second ? B : A; var enemy = second ? A : B; var rng = new FftRandomState(1, count);
        var map = new BattleMapDefinition(8, 8, 1, Enumerable.Repeat(new BattleTile(Ground, 10), 64).ToArray());
        var movement = new UnitMovementProfile(3, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        BattleUnitState[] bodies = [new(actor, new(2, 1, 0), movement), new(enemy, target ?? new(6, 1, 0), movement)];
        TurnUnitState[] turns = [new(actor, 20, 95, 2), new(enemy, 1, 0, 17)];
        AttackUnitState[] units = [new(actor, 50, 50, 0, 0, pa, 0, 0, 0, 0, 70, 0, BattleFacing.West, AttackUnitStatus.None, 0, null),
            new(enemy, hp, hp, 0, 0, 5, evade, 0, 0, 0, 70, 0, BattleFacing.West, AttackUnitStatus.None, 0, null)];
        var state = FacingTurnBattle.Begin(new(new(new(map, reverse ? bodies.Reverse() : bodies, rng),
            new(reverse ? turns.Reverse() : turns, rng)), reverse ? units.Reverse() : units), new(Ground));
        var ready = FacingTurnBattle.Advance(state); Assert.True(ready.Accepted); return ready.State;
    }
}
