using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

/// <summary>FTF-001..018 implementation coverage; source-derived, not original executable equivalence.</summary>
public sealed class FacingTurnBattleTests
{
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly FlatSwordBattleProfile Profile = new(Ground);
    private static readonly FacingChoice Keep = new FacingChoice.KeepCurrent();

    public static IEnumerable<object[]> Directions()
    {
        using var json = Contract();
        foreach (var row in json.RootElement.GetProperty("vectors").EnumerateArray().Where(r =>
            r.GetProperty("family").GetString() == "direction" && r.GetProperty("expected").GetProperty("facing").GetString() != "Overlap" &&
            r.GetProperty("inputs").GetProperty("target")[0].GetInt32() < 128))
            yield return [row.GetProperty("id").GetString()!];
    }

    [Theory] [MemberData(nameof(Directions))]
    public void Source_cardinal_and_diagonal_vectors_choose_and_settle_facing(string id)
    {
        using var json = Contract();
        var row = json.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        var input = row.GetProperty("inputs"); var expected = row.GetProperty("expected");
        var state = Active(actor: Point(input.GetProperty("origin")), target: Point(input.GetProperty("target")));
        var actor = A; var enemy = B;
        string before = Snapshot(state);
        var query = FacingTurnBattle.Probe(state, actor, new FacingChoice.ChooseClosedEnemy(enemy));
        Assert.True(query.Accepted); Assert.Equal((BattleFacing)expected.GetProperty("managedFacing").GetInt32(), query.Facing);
        var result = FacingTurnBattle.WaitWithFacing(state, actor, new FacingChoice.ChooseClosedEnemy(enemy));
        Assert.True(result.Accepted); Assert.Equal(query.Facing, Unit(result.State, actor).Facing);
        Assert.Equal(before, Snapshot(state)); Assert.Equal(state.Battle.TurnBattle.Turns.RngState, result.State.Battle.TurnBattle.Turns.RngState);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Actor_identity_and_collection_order_preserve_facing_choice_and_settlement(bool second)
    {
        var state = Active(second: second); var reversed = Active(second: second, reverse: true);
        string before = Snapshot(state), reversedBefore = Snapshot(reversed);
        var actor = second ? B : A; var choice = new FacingChoice.ChooseClosedEnemy(second ? A : B);
        var probe = FacingTurnBattle.Probe(state, actor, choice);
        Assert.True(probe.Accepted); Assert.Equal(probe, FacingTurnBattle.Probe(reversed, actor, choice));
        Assert.Equal(BattleFacing.East, probe.Facing);
        var result = FacingTurnBattle.WaitWithFacing(state, actor, choice);
        var other = FacingTurnBattle.WaitWithFacing(reversed, actor, choice);
        var after = Accepted(result); var otherAfter = Accepted(other);
        Assert.Equal(probe.Facing, Unit(after, actor).Facing);
        Assert.Equal(new FacingSettledEvent(actor, Unit(state, actor).Facing, BattleFacing.East), result.Events[^1]);
        Assert.Equal(result.Events.ToArray(), other.Events.ToArray());
        Assert.Equal(after.Revision, otherAfter.Revision);
        Assert.Equal(Unit(after, actor), Unit(otherAfter, actor));
        Assert.Equal(Turn(after, actor), Turn(otherAfter, actor));
        Assert.Equal(after.Battle.TurnBattle.Turns.RngState, otherAfter.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(before, Snapshot(state)); Assert.Equal(reversedBefore, Snapshot(reversed));
    }

    public static IEnumerable<object[]> TypedFacings()
    {
        foreach (var face in Enum.GetValues<BattleFacing>())
            yield return [face];
    }

    [Theory] [MemberData(nameof(TypedFacings))]
    public void Fixed_and_KeepCurrent_need_no_legal_adjacent_move(BattleFacing face)
    {
        var actor = A;
        var state = Active(actorFacing: face, actor: new(0, 0, 0), target: new(1, 0, 0));
        Assert.Equal(face, FacingTurnBattle.Probe(state, actor, Keep).Facing);
        foreach (var requested in Enum.GetValues<BattleFacing>())
        {
            var result = FacingTurnBattle.WaitWithFacing(state, actor, new FacingChoice.Fixed(requested));
            Assert.True(result.Accepted); Assert.Equal(new FacingSettledEvent(actor, face, requested), result.Events[^1]);
            Assert.Null(result.State.PendingFacing); Assert.Equal(2ul, result.State.Revision);
        }
    }

    [Theory]
    [InlineData("fixed-south-ignores-viability")] [InlineData("fixed-west-ignores-viability")]
    [InlineData("fixed-north-ignores-viability")] [InlineData("fixed-east-ignores-viability")]
    [InlineData("keep-current-without-enemy")] [InlineData("one-enemy-east")]
    [InlineData("post-move-uses-settled-origin")] [InlineData("post-push-uses-settled-enemy")]
    public void Executes_eight_admitted_ZOD118_hint_and_closed_chooser_vectors(string id)
    {
        using var json = Contract();
        var row = json.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        var input = row.GetProperty("inputs"); var expected = row.GetProperty("expected");
        string hint = input.GetProperty("hint").GetString()!;
        var enemies = input.GetProperty("enemies");
        var state = Active(actor: Point(input.GetProperty("actor")), target: enemies.GetArrayLength() == 1
            ? Point(enemies[0].GetProperty("position")) : null, actorFacing: Enum.Parse<BattleFacing>(input.GetProperty("current").GetString()!));
        FacingChoice choice = hint == "ChooseAtWait" ? new FacingChoice.ChooseClosedEnemy(B)
            : hint == "KeepCurrent" ? Keep : new FacingChoice.Fixed(Enum.Parse<BattleFacing>(hint));
        var result = FacingTurnBattle.WaitWithFacing(state, A, choice);
        Assert.True(result.Accepted);
        Assert.Equal(expected.GetProperty("facing").GetString(), Unit(result.State, A).Facing.ToString());
        Assert.Equal(expected.GetProperty("managedFacing").GetInt32(), (int)Unit(result.State, A).Facing);
    }

    [Theory] [InlineData("none", 55)] [InlineData("move", 35)] [InlineData("act", 35)] [InlineData("cap", 60)]
    public void Explicit_Wait_refunds_unused_choices_once_and_has_one_end_event(string choices, int expectedCt)
    {
        var state = Active(ct: choices == "cap" ? 149 : 105);
        if (choices == "move") state = Accepted(FacingTurnBattle.Move(state, new(A, new(2, 3, 0))));
        if (choices == "act") state = Accepted(FacingTurnBattle.Attack(state, new(A, B), new(Weapon, 0)));
        var result = FacingTurnBattle.WaitWithFacing(state, A, Keep);
        Assert.True(result.Accepted); Assert.Equal(expectedCt, Turn(result.State, A).Ct);
        Assert.Single(Ended(result.Events)); Assert.IsType<FacingSettledEvent>(result.Events[^1]);
        Assert.Equal(state.Battle.TurnBattle.Turns.RngState, result.State.Battle.TurnBattle.Turns.RngState);
        Atomic(result.State, FacingTurnBattle.WaitWithFacing(result.State, A, Keep), FacingTurnRejection.InactiveActor);
        Atomic(result.State, FacingTurnBattle.FinishFacing(result.State, A, null, Keep), FacingTurnRejection.MissingCompletion);
    }

    [Theory] [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void Both_choice_orders_and_actors_issue_receipt_after_full_settlement(bool moveFirst, bool second)
    {
        var actor = second ? B : A; var target = second ? A : B;
        var state = Active(second: second, actor: moveFirst ? new(2, 3, 0) : new(2, 2, 0));
        FacingTurnResult result;
        if (moveFirst)
        {
            state = Accepted(FacingTurnBattle.Move(state, new(actor, new(2, 2, 0))));
            result = FacingTurnBattle.Attack(state, new(actor, target), new(Weapon, 5));
        }
        else
        {
            state = Accepted(FacingTurnBattle.Attack(state, new(actor, target), new(Weapon, 5)));
            result = FacingTurnBattle.Move(state, new(actor, new(2, 4, 0)));
        }
        var pending = Accepted(result);
        Assert.Null(pending.Battle.TurnBattle.Turns.ActiveUnitId); Assert.Single(Ended(result.Events));
        var receipt = Assert.IsType<FacingCompletionReceipt>(pending.PendingFacing);
        Assert.Equal(actor, receipt.ActorId); Assert.Equal(pending.Revision, receipt.Revision);
        Assert.Same(pending.Battle, receipt.ExpectedBattle);
        Assert.Equal(5, Unit(pending, target).Hp); Assert.Equal(AttackUnitStatus.Critical, Unit(pending, target).Status);
        Assert.Equal(new TilePosition(4, 2, 0), Position(pending, target));
        Assert.Equal(new FftRandomState(3448561432, 8), pending.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(15, Turn(pending, actor).Ct);
        string before = Snapshot(pending);
        var finish = FacingTurnBattle.FinishFacing(pending, actor, receipt, new FacingChoice.ChooseClosedEnemy(target));
        var settled = Accepted(finish);
        // Act-Move from (2,4) faces East toward pushed (4,2); stale (3,2) would incorrectly choose South.
        Assert.Equal(BattleFacing.East, Unit(settled, actor).Facing);
        Assert.Single(finish.Events); Assert.IsType<FacingSettledEvent>(finish.Events[0]); Assert.Empty(Ended(finish.Events));
        Assert.Same(pending.Battle.TurnBattle, settled.Battle.TurnBattle);
        Assert.Same(Unit(pending, target), Unit(settled, target));
        Assert.Equal(before, Snapshot(pending)); Assert.Null(settled.PendingFacing);
        Assert.Equal(pending.Revision + 1, settled.Revision);
    }

    [Fact]
    public void Pending_barrier_precedes_every_delegated_command_and_invalid_choice_preserves_committed_attack()
    {
        var state = Pending(); string before = Snapshot(state); var receipt = state.PendingFacing;
        foreach (var result in new[] { FacingTurnBattle.Advance(state), FacingTurnBattle.Move(state, new(A, new(0, 0, 0))),
            FacingTurnBattle.Attack(state, new(A, B), null), FacingTurnBattle.WaitWithFacing(state, A, Keep) })
            Atomic(state, result, FacingTurnRejection.FacingPending);
        Atomic(state, FacingTurnBattle.FinishFacing(state, A, receipt, new FacingChoice.Fixed((BattleFacing)99)), FacingTurnRejection.UnsupportedFacing);
        Assert.Same(receipt, state.PendingFacing); Assert.Equal(before, Snapshot(state));
        Assert.Equal(5, Unit(state, B).Hp); Assert.Equal(8ul, state.Battle.TurnBattle.Turns.RngState.DrawCount);
    }

    [Fact]
    public void Receipts_reject_missing_stale_other_actor_cross_branch_and_cross_run_requests()
    {
        var active = Active(actor: new(2, 3, 0));
        Atomic(active, FacingTurnBattle.FinishFacing(active, A, null, Keep), FacingTurnRejection.MissingCompletion);
        var moved = Accepted(FacingTurnBattle.Move(active, new(A, new(2, 2, 0))));
        var left = Accepted(FacingTurnBattle.Attack(moved, new(A, B), new(Weapon, 5)));
        var right = Accepted(FacingTurnBattle.Attack(moved, new(A, B), new(Weapon, 5)));
        Assert.Equal(Snapshot(left), Snapshot(right)); Assert.NotSame(left.PendingFacing, right.PendingFacing);
        Atomic(right, FacingTurnBattle.FinishFacing(right, A, left.PendingFacing, Keep), FacingTurnRejection.StaleCompletion);
        Atomic(left, FacingTurnBattle.FinishFacing(left, B, left.PendingFacing, Keep), FacingTurnRejection.WrongActor);
        Atomic(left, FacingTurnBattle.FinishFacing(left, A, null, Keep), FacingTurnRejection.StaleCompletion);
        var other = Pending();
        Atomic(left, FacingTurnBattle.FinishFacing(left, A, other.PendingFacing, Keep), FacingTurnRejection.StaleCompletion);
        var settled = Accepted(FacingTurnBattle.FinishFacing(left, A, left.PendingFacing, Keep));
        Assert.Equal(Unit(left, A).Facing, Unit(settled, A).Facing);
        Atomic(settled, FacingTurnBattle.FinishFacing(settled, A, left.PendingFacing, Keep), FacingTurnRejection.MissingCompletion);
        // Replaying an old immutable input is legal and does not consult a global consumed-token set.
        var replay = Accepted(FacingTurnBattle.FinishFacing(left, A, left.PendingFacing, Keep));
        Assert.Equal(Snapshot(settled), Snapshot(replay));
    }

    [Theory] [InlineData("revision")] [InlineData("snapshot")] [InlineData("lineage")]
    public void Receipt_binding_checks_every_internal_component(string mismatch)
    {
        var pending = Pending();
        var state = new FacingTurnState(mismatch == "snapshot" ? new(pending.Battle.TurnBattle, pending.Battle.Combatants) : pending.Battle,
            Profile, mismatch == "revision" ? pending.Revision + 1 : pending.Revision, pending.PendingFacing,
            mismatch == "lineage" ? new object() : pending.Lineage);
        Atomic(state, FacingTurnBattle.FinishFacing(state, A, pending.PendingFacing, Keep), FacingTurnRejection.StaleCompletion);
    }

    [Fact]
    public void No_living_enemy_rejects_chooser_but_fixed_and_keep_can_finish_without_repeating_KO()
    {
        var pending = Pending(hp: 45);
        Assert.Equal(0, Unit(pending, B).Hp); Assert.Equal(0, Unit(pending, B).KoCount);
        Assert.Equal(FacingTurnRejection.NoLivingEnemy, FacingTurnBattle.Probe(pending, A, new FacingChoice.ChooseClosedEnemy(B)).Rejection);
        Atomic(pending, FacingTurnBattle.FinishFacing(pending, A, pending.PendingFacing, new FacingChoice.ChooseClosedEnemy(B)), FacingTurnRejection.NoLivingEnemy);
        foreach (FacingChoice choice in new FacingChoice[] { Keep, new FacingChoice.Fixed(BattleFacing.South) })
        {
            var result = FacingTurnBattle.FinishFacing(pending, A, pending.PendingFacing, choice);
            Assert.True(result.Accepted); Assert.Same(Unit(pending, B), Unit(result.State, B)); Assert.Single(result.Events);
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Counter_limit_finishes_at_max_or_rejects_without_any_effect(bool room)
    {
        var pending = Counters(Pending(), revision: room ? ulong.MaxValue - 1 : ulong.MaxValue, draws: ulong.MaxValue);
        string before = Snapshot(pending);
        for (int i = 0; i < 3; i++) Assert.True(FacingTurnBattle.Probe(pending, A, Keep).Accepted);
        var result = FacingTurnBattle.FinishFacing(pending, A, pending.PendingFacing, Keep);
        if (!room) Atomic(pending, result, FacingTurnRejection.RevisionExhausted);
        else
        {
            Assert.True(result.Accepted); Assert.Equal(ulong.MaxValue, result.State.Revision);
            Assert.Equal(ulong.MaxValue, result.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        }
        Assert.Equal(before, Snapshot(pending));
    }

    [Theory] [InlineData("advance")] [InlineData("move")] [InlineData("attack")] [InlineData("wait")]
    public void Revision_exhaustion_is_checked_before_delegating_or_refunding(string kind)
    {
        var state = Counters(kind == "advance" ? Fresh() : Active(), revision: ulong.MaxValue, draws: ulong.MaxValue);
        var result = kind switch
        {
            "advance" => FacingTurnBattle.Advance(state), "move" => FacingTurnBattle.Move(state, new(A, new(2, 3, 0))),
            "attack" => FacingTurnBattle.Attack(state, new(A, B), new(Weapon, 5)),
            _ => FacingTurnBattle.WaitWithFacing(state, A, Keep)
        };
        Atomic(state, result, FacingTurnRejection.RevisionExhausted);
    }

    [Fact]
    public void Zero_draw_Wait_at_exhausted_RNG_succeeds_and_later_Advance_preserves_legacy_rejection()
    {
        var active = Counters(Active(), draws: ulong.MaxValue);
        var wait = FacingTurnBattle.WaitWithFacing(active, A, new FacingChoice.ChooseClosedEnemy(B));
        var ended = Accepted(wait);
        Assert.Equal(ulong.MaxValue, ended.Battle.TurnBattle.Turns.RngState.DrawCount);
        var advance = FacingTurnBattle.Advance(ended);
        Assert.False(advance.Accepted); Assert.Same(ended, advance.State);
        Assert.Equal(TurnCycleRejection.RandomDrawCountExhausted, advance.TurnRejection);
        Assert.Empty(advance.Events);
    }

    [Fact]
    public void Current_PA_HP_KO_and_other_stats_survive_only_the_actor_facing_change()
    {
        var start = Active(pa: 6, hp: 100);
        var result = FacingTurnBattle.WaitWithFacing(start, A, new FacingChoice.Fixed(BattleFacing.West));
        var settled = Accepted(result);
        var before = Unit(start, A); var after = Unit(settled, A);
        Assert.Equal(6, after.PhysicalAttack); Assert.Equal(before.WithResult(before.Hp, BattleFacing.West, before.KoCount), after);
        Assert.Same(Unit(start, B), Unit(settled, B));
    }

    [Fact]
    public void KeepCurrent_after_Move_uses_supplied_facing_without_inventing_path_heading()
    {
        var start = Active(actor: new(1, 2, 0), actorFacing: BattleFacing.South);
        var moved = Accepted(FacingTurnBattle.Move(start, new(A, new(2, 2, 0))));
        Assert.Equal(BattleFacing.South, FacingTurnBattle.Probe(moved, A, Keep).Facing);
        var settled = Accepted(FacingTurnBattle.WaitWithFacing(moved, A, Keep));
        Assert.Equal(BattleFacing.South, Unit(settled, A).Facing);
    }

    [Fact]
    public void Receipt_from_an_earlier_turn_cannot_authorize_the_same_actor_after_new_readiness()
    {
        var first = Pending(); var old = first.PendingFacing;
        var state = Accepted(FacingTurnBattle.FinishFacing(first, A, old, Keep));
        for (int i = 0; i < 6 && state.Battle.TurnBattle.Turns.ActiveUnitId != A; i++)
        {
            state = Accepted(FacingTurnBattle.Advance(state));
            if (state.Battle.TurnBattle.Turns.ActiveUnitId == B)
                state = Accepted(FacingTurnBattle.WaitWithFacing(state, B, Keep));
        }
        Assert.Equal(A, state.Battle.TurnBattle.Turns.ActiveUnitId);
        state = Accepted(FacingTurnBattle.Move(state, new(A, new(3, 2, 0))));
        state = Accepted(FacingTurnBattle.Attack(state, new(A, B), new(Weapon, 0)));
        Assert.Equal(A, state.PendingFacing!.ActorId);
        Atomic(state, FacingTurnBattle.FinishFacing(state, A, old, Keep), FacingTurnRejection.StaleCompletion);
        Assert.True(FacingTurnBattle.FinishFacing(state, A, state.PendingFacing, Keep).Accepted);
    }

    [Fact]
    public void Prefix_suffix_replay_preserves_semantic_state_events_and_rejections_with_fresh_receipts()
    {
        Func<FacingTurnState, FacingTurnResult>[] journal = [FacingTurnBattle.Advance,
            s => FacingTurnBattle.Move(s, new(A, new(2, 2, 0))),
            s => FacingTurnBattle.Attack(s, new(A, B), new(Weapon, 5)), FacingTurnBattle.Advance,
            s => FacingTurnBattle.FinishFacing(s, B, s.PendingFacing, Keep),
            s => FacingTurnBattle.FinishFacing(s, A, s.PendingFacing, new FacingChoice.ChooseClosedEnemy(B)),
            s => FacingTurnBattle.FinishFacing(s, A, null, Keep), FacingTurnBattle.Advance];
        var state = Fresh(actor: new(2, 3, 0)); var receipts = new List<FacingTurnResult>();
        foreach (var apply in journal) { var r = apply(state); receipts.Add(r); state = r.State; }
        Assert.Equal(new[] { true, true, true, false, false, true, false, true }, receipts.Select(r => r.Accepted));
        for (int prefix = 0; prefix <= journal.Length; prefix++)
        {
            var replay = Fresh(actor: new(2, 3, 0)); foreach (var apply in journal.Take(prefix)) replay = apply(replay).State;
            for (int i = prefix; i < journal.Length; i++)
            {
                var actual = journal[i](replay); var expected = receipts[i];
                Assert.Equal(expected.Accepted, actual.Accepted); Assert.Equal(expected.FacingRejection, actual.FacingRejection);
                Assert.Equal(expected.AttackRejection, actual.AttackRejection); Assert.Equal(expected.TurnRejection, actual.TurnRejection);
                Assert.Equal(Snapshot(expected.State), Snapshot(actual.State)); Assert.Equal(expected.Events.ToArray(), actual.Events.ToArray());
                replay = actual.State;
            }
            Assert.Equal(Snapshot(state), Snapshot(replay));
        }
    }

    [Fact]
    public void Legacy_delegation_is_exact_and_probes_do_not_change_execution_draws()
    {
        var active = Active(); string before = Snapshot(active);
        for (int i = 0; i < 4; i++) Assert.Equal(BattleFacing.East, FacingTurnBattle.Probe(active, A, new FacingChoice.ChooseClosedEnemy(B)).Facing);
        var legacy = AttackBattleSimulator.Attack(active.Battle, new(A, B), new(Weapon, 5), Profile);
        var wrapped = FacingTurnBattle.Attack(active, new(A, B), new(Weapon, 5));
        Assert.Equal(BattleSnapshot(legacy.State), BattleSnapshot(wrapped.State.Battle));
        Assert.Equal(legacy.Events.ToArray(), wrapped.Events.ToArray()); Assert.Null(wrapped.State.PendingFacing);
        Assert.Equal(before, Snapshot(active));
    }

    [Theory] [InlineData("advance")] [InlineData("move")] [InlineData("attack")] [InlineData("weapon")]
    public void Delegated_rejections_publish_no_revision_or_receipt(string kind)
    {
        var state = Active();
        var result = kind switch
        {
            "advance" => FacingTurnBattle.Advance(state),
            "move" => FacingTurnBattle.Move(state, new(A, new(2, 2, 0))),
            "attack" => FacingTurnBattle.Attack(state, new(B, A), new(Weapon, 5)),
            _ => FacingTurnBattle.Attack(state, new(A, B), null)
        };
        Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events);
        Assert.Equal(FacingTurnRejection.None, result.FacingRejection); Assert.Null(result.State.PendingFacing);
        if (kind == "advance") Assert.Equal(TurnCycleRejection.TurnAlreadyActive, result.TurnRejection);
        else if (kind == "move") Assert.Equal(BattleTransitionRejection.DestinationNotReachable, result.MovementRejection);
        else Assert.Equal(kind == "attack" ? AttackCommandRejection.InactiveActor : AttackCommandRejection.UnsupportedSetup, result.AttackRejection);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void Completing_critical_attack_requires_its_full_draw_budget_before_minting_receipt(int remaining)
    {
        var moved = Accepted(FacingTurnBattle.Move(Active(actor: new(2, 3, 0)), new(A, new(2, 2, 0))));
        var state = Counters(moved, draws: ulong.MaxValue - (ulong)remaining);
        string before = Snapshot(state);
        var result = FacingTurnBattle.Attack(state, new(A, B), new(Weapon, 5));
        if (remaining < 7)
        {
            Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events);
            Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, result.AttackRejection); Assert.Null(result.State.PendingFacing);
        }
        else
        {
            var pending = Accepted(result); Assert.NotNull(pending.PendingFacing);
            Assert.Equal(ulong.MaxValue, pending.Battle.TurnBattle.Turns.RngState.DrawCount);
            var finish = FacingTurnBattle.FinishFacing(pending, A, pending.PendingFacing, Keep);
            Assert.True(finish.Accepted); Assert.Equal(ulong.MaxValue, finish.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        }
        Assert.Equal(before, Snapshot(state));
    }

    [Theory] [InlineData("advance")] [InlineData("move")] [InlineData("attack")] [InlineData("wait")]
    public void Delegated_success_can_use_last_revision_without_wrapping(string kind)
    {
        var state = Counters(kind == "advance" ? Fresh() : Active(), revision: ulong.MaxValue - 1);
        var result = kind switch
        {
            "advance" => FacingTurnBattle.Advance(state), "move" => FacingTurnBattle.Move(state, new(A, new(2, 3, 0))),
            "attack" => FacingTurnBattle.Attack(state, new(A, B), new(Weapon, 0)),
            _ => FacingTurnBattle.WaitWithFacing(state, A, Keep)
        };
        Assert.True(result.Accepted); Assert.Equal(ulong.MaxValue, result.State.Revision);
        Atomic(result.State, FacingTurnBattle.Advance(result.State), FacingTurnRejection.RevisionExhausted);
    }

    [Theory] [InlineData(105, 15)] [InlineData(189, 60)]
    public void Automatic_completion_caps_CT_once_before_facing(int ct, int expected)
    {
        var moved = Accepted(FacingTurnBattle.Move(Active(ct: ct, actor: new(2, 3, 0)), new(A, new(2, 2, 0))));
        var pending = Accepted(FacingTurnBattle.Attack(moved, new(A, B), new(Weapon, 5)));
        Assert.Equal(expected, Turn(pending, A).Ct);
        var settled = Accepted(FacingTurnBattle.FinishFacing(pending, A, pending.PendingFacing, Keep));
        Assert.Equal(expected, Turn(settled, A).Ct);
    }

    [Fact]
    public void Begin_preserves_preexisting_RNG_progress_instead_of_starting_a_new_stream()
    {
        var start = FacingTurnBattle.Begin(Setup(count: 500), Profile);
        Assert.Equal(new FftRandomState(601690112, 500), start.Battle.TurnBattle.Turns.RngState);
        var active = Accepted(FacingTurnBattle.Advance(start));
        Assert.Equal(new FftRandomState(57, 501), active.Battle.TurnBattle.Turns.RngState);
        var exhausted = FacingTurnBattle.Begin(Setup(count: ulong.MaxValue), Profile);
        var result = FacingTurnBattle.Advance(exhausted);
        Assert.False(result.Accepted); Assert.Same(exhausted, result.State);
        Assert.Equal(TurnCycleRejection.RandomDrawCountExhausted, result.TurnRejection);
    }

    [Fact]
    public void Even_a_matching_internal_receipt_cannot_finalize_an_active_clock_turn()
    {
        var active = Active();
        var receipt = new FacingCompletionReceipt(A, active.Revision, active.Battle, active.Lineage);
        var inconsistent = new FacingTurnState(active.Battle, Profile, active.Revision, receipt, active.Lineage);
        Atomic(inconsistent, FacingTurnBattle.FinishFacing(inconsistent, A, receipt, Keep), FacingTurnRejection.InconsistentCompletion);
    }

    [Theory]
    [InlineData("blocked")] [InlineData("untargetable")] [InlineData("stop")] [InlineData("water")]
    [InlineData("height")] [InlineData("half")] [InlineData("ceiling")] [InlineData("surface")]
    [InlineData("slope")] [InlineData("layer")] [InlineData("overlap")] [InlineData("third")]
    [InlineData("fly")] [InlineData("traits")] [InlineData("body")] [InlineData("blocked-upper")]
    public void Begin_requires_the_entire_certified_geometry(string geometry)
    {
        var error = Record.Exception(() => FacingTurnBattle.Begin(Setup(geometry: geometry), Profile));
        Assert.True(error is ArgumentException or NotSupportedException, error?.ToString());
    }

    [Theory] [InlineData("active")] [InlineData("ended")] [InlineData("ticks")]
    [InlineData("acted")] [InlineData("moved")] [InlineData("dead")] [InlineData("critical")]
    [InlineData("wide")] [InlineData("deep")]
    public void Begin_rejects_nonfresh_or_outside_coordinate_domain(string kind)
    {
        var setup = kind == "active" ? Active().Battle : kind == "ended" ? Accepted(FacingTurnBattle.WaitWithFacing(Active(), A, Keep)).Battle
            : Setup(ticks: kind == "ticks" ? 1ul : 0,
                acted: kind == "acted", moved: kind == "moved", dead: kind == "dead", hp: kind == "critical" ? 10 : 50,
                width: kind == "wide" ? 129 : 5, depth: kind == "deep" ? 129 : 5);
        Assert.Throws<NotSupportedException>(() => FacingTurnBattle.Begin(setup, Profile));
    }

    [Fact]
    public void Maximum_admitted_map_and_invalid_choice_actor_and_programmer_errors_are_explicit()
    {
        var large = Active(width: 128, depth: 128, actor: new(126, 127, 0), target: new(127, 127, 0));
        Assert.Equal(BattleFacing.East, FacingTurnBattle.Probe(large, A, new FacingChoice.ChooseClosedEnemy(B)).Facing);
        var active = Active();
        Atomic(active, FacingTurnBattle.WaitWithFacing(active, B, Keep), FacingTurnRejection.InactiveActor);
        Atomic(active, FacingTurnBattle.WaitWithFacing(active, new("sample:unit/missing"), Keep), FacingTurnRejection.UnknownActor);
        Atomic(active, FacingTurnBattle.WaitWithFacing(active, A, new FacingChoice.Fixed((BattleFacing)99)), FacingTurnRejection.UnsupportedFacing);
        foreach (var enemy in new[] { A, new BattleUnitId("sample:unit/missing") })
            Atomic(active, FacingTurnBattle.WaitWithFacing(active, A, new FacingChoice.ChooseClosedEnemy(enemy)), FacingTurnRejection.UnsupportedFacing);
        Assert.Throws<ArgumentNullException>(() => FacingTurnBattle.Begin(null!, Profile));
        Assert.Throws<ArgumentNullException>(() => FacingTurnBattle.Begin(Setup(), null!));
        Assert.Throws<ArgumentNullException>(() => FacingTurnBattle.Advance(null!));
        Assert.Throws<ArgumentNullException>(() => FacingTurnBattle.Probe(active, A, null!));
    }

    private static JsonDocument Contract() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "turn-facing-contract-v1.json")));
    private static TilePosition Point(JsonElement point) => new(point[0].GetInt32(), point[1].GetInt32(), 0);
    private static FacingTurnState Accepted(FacingTurnResult result)
    { Assert.True(result.Accepted, $"{result.FacingRejection}/{result.AttackRejection}/{result.TurnRejection}/{result.MovementRejection}"); return result.State; }
    private static AttackUnitState Unit(FacingTurnState state, BattleUnitId id) => state.Battle.Combatants.Single(u => u.UnitId == id);
    private static TurnUnitState Turn(FacingTurnState state, BattleUnitId id) => state.Battle.TurnBattle.Turns.Units.Single(u => u.Id == id);
    private static TilePosition Position(FacingTurnState state, BattleUnitId id) => state.Battle.TurnBattle.Battle.Units.Single(u => u.Id == id).Position;
    private static IEnumerable<TurnEndedEvent> Ended(IEnumerable<AttackResultEvent> events) => events.OfType<AttackTurnBattleEvent>()
        .Select(e => e.Event).OfType<TurnBattleClockEvent>().Select(e => e.Event).OfType<TurnEndedEvent>();
    private static string BattleSnapshot(AttackBattleSnapshot state) => JsonSerializer.Serialize(new
        { state.Combatants, state.TurnBattle.Turns, state.TurnBattle.Battle.Units, state.TurnBattle.Battle.RngState });
    private static string Snapshot(FacingTurnState state) => JsonSerializer.Serialize(new { battle = BattleSnapshot(state.Battle), state.Revision,
        pending = state.PendingFacing is { } p ? new { p.ActorId, p.Revision } : null });
    private static void Atomic(FacingTurnState state, FacingTurnResult result, FacingTurnRejection rejection)
    { Assert.False(result.Accepted); Assert.Equal(rejection, result.FacingRejection); Assert.Same(state, result.State); Assert.Empty(result.Events); }

    private static FacingTurnState Pending(int hp = 50)
    {
        var state = Accepted(FacingTurnBattle.Move(Active(hp: hp, actor: new(2, 3, 0)), new(A, new(2, 2, 0))));
        return Accepted(FacingTurnBattle.Attack(state, new(A, B), new(Weapon, 5)));
    }

    // Friend access is limited to counter-limit/invariant tests; no public state or receipt injection API.
    private static FacingTurnState Counters(FacingTurnState state, ulong? revision = null, ulong? draws = null)
    {
        var battle = state.Battle;
        if (draws is { } count)
        {
            var rng = new FftRandomState(battle.TurnBattle.Turns.RngState.Value, count);
            battle = new(new(new(battle.TurnBattle.Battle.Map, battle.TurnBattle.Battle.Units, rng),
                new(battle.TurnBattle.Turns.Units, rng, battle.TurnBattle.Turns.Ticks, battle.TurnBattle.Turns.Phase)), battle.Combatants);
        }
        ulong next = revision ?? state.Revision;
        var pending = state.PendingFacing is { } p ? new FacingCompletionReceipt(p.ActorId, next, battle, state.Lineage) : null;
        return new(battle, state.Profile, next, pending, state.Lineage);
    }

    private static FacingTurnState Fresh(bool second = false, bool reverse = false, int pa = 5, int hp = 50,
        int ct = 105, TilePosition? actor = null, TilePosition? target = null, BattleFacing actorFacing = BattleFacing.North,
        int width = 5, int depth = 5) => FacingTurnBattle.Begin(Setup(second, reverse, pa, hp, ct, actor, target, actorFacing, width, depth), Profile);
    private static FacingTurnState Active(bool second = false, bool reverse = false, int pa = 5, int hp = 50,
        int ct = 105, TilePosition? actor = null, TilePosition? target = null, BattleFacing actorFacing = BattleFacing.North,
        int width = 5, int depth = 5) => Accepted(FacingTurnBattle.Advance(Fresh(second, reverse, pa, hp, ct, actor, target, actorFacing, width, depth)));

    private static AttackBattleSnapshot Setup(bool second = false, bool reverse = false, int pa = 5, int hp = 50,
        int ct = 105, TilePosition? actor = null, TilePosition? target = null, BattleFacing actorFacing = BattleFacing.North,
        int width = 5, int depth = 5, uint seed = 601690112, ulong count = 0, ulong ticks = 0, int ko = 255, int evade = 0,
        BattleFacing targetFacing = BattleFacing.West, int actorBrave = 70, int targetBrave = 70,
        bool acted = false, bool moved = false, bool dead = false, bool unarmed = false,
        bool targetMoved = false, bool targetActed = false, string? geometry = null)
    {
        var actorId = second ? B : A; var targetId = second ? A : B;
        var rng = new FftRandomState(seed, count); int layers = geometry is "layer" or "blocked-upper" ? 2 : 1;
        var tiles = Enumerable.Repeat(new BattleTile(Ground, 10), width * depth * layers).ToArray();
        if (geometry == "blocked-upper")
            for (int i = width * depth; i < tiles.Length; i++)
                tiles[i] = new(Ground, 0, movementFlags: BattleTileMovementFlags.Blocked);
        tiles[^1] = geometry switch
        {
            "blocked" => new(Ground, 10, movementFlags: BattleTileMovementFlags.Blocked),
            "untargetable" => new(Ground, 10, movementFlags: BattleTileMovementFlags.Untargetable),
            "stop" => new(Ground, 10, movementFlags: BattleTileMovementFlags.CannotStop),
            "water" => new(Ground, 10, waterDepth: 1), "height" => new(Ground, 11),
            "half" => new(Ground, 10, halfHeight: 1), "ceiling" => new(Ground, 10, ceilingDepth: 1),
            "surface" => new(new TerrainSurfaceId("sample:terrain/uncertified"), 10),
            "slope" => new(Ground, 10, slope: TileSlope.FromPackedByte(1)), _ => tiles[^1]
        };
        var map = new BattleMapDefinition(width, depth, layers, tiles);
        var movement = new UnitMovementProfile(3, 2, geometry == "body" ? 7 : 6,
            geometry == "fly" ? UnitMovementMode.Fly : UnitMovementMode.Normal,
            geometry == "traits" ? UnitMovementTraits.AnyGround : UnitMovementTraits.None, []);
        var bodies = new List<BattleUnitState> { new(actorId, actor ?? new(2, 2, 0), movement, moved),
            new(targetId, geometry == "overlap" ? actor ?? new(2, 2, 0) : target ?? new(3, 2, 0), movement, targetMoved) };
        var turns = new List<TurnUnitState> { new(actorId, 10, ct, 0, false, moved, acted),
            new(targetId, 12, 0, 1, false, targetMoved, targetActed) };
        int maximum = Math.Max(50, hp);
        var combat = new List<AttackUnitState> { new(actorId, 50, 50, 0, 0, pa, 0, 0, 0, 0, actorBrave, 0, actorFacing,
                AttackUnitStatus.None, 0, unarmed ? null : Weapon),
            new(targetId, dead ? 0 : hp, maximum, 0, 0, 5, evade, 0, 0, 0, targetBrave, 0, targetFacing,
                dead ? AttackUnitStatus.Dead : hp <= maximum / 5 ? AttackUnitStatus.Critical : AttackUnitStatus.None, ko, Weapon) };
        if (geometry == "third")
        {
            var c = new BattleUnitId("sample:unit/c"); bodies.Add(new(c, new(0, 0, 0), movement)); turns.Add(new(c, 10, 0, 2));
            combat.Add(new(c, 50, 50, 0, 0, 5, 0, 0, 0, 0, 70, 0, BattleFacing.East, AttackUnitStatus.None, 0, Weapon));
        }
        return new(new(new(map, reverse ? bodies.AsEnumerable().Reverse() : bodies, rng),
            new(reverse ? turns.AsEnumerable().Reverse() : turns, rng, ticks, TurnCyclePhase.AddCt)),
            reverse ? combat.AsEnumerable().Reverse() : combat);
    }
}
