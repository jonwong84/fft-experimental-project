using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class FacingTurnPathMovementTests
{
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ground");
    private static readonly AssetId Weapon = new("sample:item/sword");
    private static readonly FlatSwordBattleProfile Sword = new(Ground);
    private static readonly FlatOrdinaryMovementProfile Movement = new(Ground, A, B);
    private static readonly FacingChoice Keep = new FacingChoice.KeepCurrent();

    public static IEnumerable<object[]> PositiveProposals()
    {
        using var document = Contract("voluntary-move-contract-v1.json");
        foreach (var row in document.RootElement.GetProperty("vectors").EnumerateArray()
            .Where(r => r.GetProperty("family").GetString() == "proposed-move" && r.GetProperty("expected").GetProperty("accepted").GetBoolean()))
            yield return [row.GetProperty("id").GetString()!];
    }

    [Theory] [MemberData(nameof(PositiveProposals))]
    public void Source_move_proposals_preserve_path_heading_choices_counters_and_receipts(string id)
    {
        using var document = Contract("voluntary-move-contract-v1.json");
        var row = document.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        var input = row.GetProperty("inputs"); var route = input.GetProperty("route"); var expected = row.GetProperty("expected");
        ulong count = input.GetProperty("rngCount").GetUInt64(); uint word = input.GetProperty("rngWord").GetUInt32();
        // Invert one BIOS transition so public Advance delivers the vector's literal word/count.
        var state = Active(false, false, route.GetProperty("width").GetInt32(), route.GetProperty("depth").GetInt32(),
            Point(route.GetProperty("origin")), Point(route.GetProperty("enemy")), route.GetProperty("move").GetInt32(),
            route.GetProperty("jump").GetInt32(), ct: input.GetProperty("ct").GetInt32(),
            seed: unchecked((word - 12345u) * 4005161829u), count: count - 1);
        var actor = A;
        // Normalized source vectors supply an Act flag, not an executable attack. Public combat sequences are tested below.
        if (input.GetProperty("acted").GetBoolean()) state = MarkActForNormalizedVector(state, actor);
        state = Counters(state, input.GetProperty("revision").GetUInt64());
        string before = Snapshot(state);
        var result = Move(state, actor, Point(route.GetProperty("destination")));
        var after = Accepted(result);
        Assert.Equal(expected.GetProperty("path").EnumerateArray().Select(Point), result.Path!.Tiles);
        Assert.Equal(expected.GetProperty("packedSteps").EnumerateArray().Select(v => v.GetByte()), result.Path.PackedSteps);
        var facing = Enum.Parse<BattleFacing>(expected.GetProperty("facing").GetString()!);
        Assert.Equal(facing, Combat(after, actor).Facing); Assert.Equal(facing, result.Path.FinalHeading);
        Assert.Equal(Point(expected.GetProperty("position")), Body(after, actor).Position);
        Assert.Equal(expected.GetProperty("ct").GetInt32(), Turn(after, actor).Ct);
        Assert.Equal(expected.GetProperty("hasTurn").GetBoolean(), Turn(after, actor).HasTurn);
        Assert.True(Body(after, actor).HasMoved); Assert.True(Turn(after, actor).MovementTaken);
        Assert.Equal(expected.GetProperty("acted").GetBoolean(), Turn(after, actor).ActionTaken);
        Assert.Equal(expected.GetProperty("revision").GetUInt64(), after.Revision);
        Assert.Equal(expected.GetProperty("pending").GetBoolean(), after.PendingFacing is not null);
        Assert.Equal(new FftRandomState(word, count), after.Battle.TurnBattle.Turns.RngState);
        AssertEvents(state, result, actor); AssertUnrelated(state, after, actor);
        Assert.Equal(before, Snapshot(state));
    }

    [Theory] [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void Actor_identity_and_collection_order_preserve_path_moves_before_and_after_Act(bool second, bool acted)
    {
        var state = Active(second: second, enemy: new(3, 2, 0));
        var reversed = Active(second: second, enemy: new(3, 2, 0), reverse: true);
        var actor = second ? B : A; var enemy = second ? A : B;
        if (acted)
        {
            state = Accepted(FacingTurnBattle.Attack(state, new(actor, enemy), new(Weapon, 0)));
            reversed = Accepted(FacingTurnBattle.Attack(reversed, new(actor, enemy), new(Weapon, 0)));
        }
        string before = Snapshot(state), reversedBefore = Snapshot(reversed);
        var result = Move(state, actor, new(2, 3, 0)); var other = Move(reversed, actor, new(2, 3, 0));
        var after = Accepted(result); var otherAfter = Accepted(other);
        Assert.Equal(new TilePosition(2, 3, 0), Body(after, actor).Position);
        Assert.True(Body(after, actor).HasMoved); Assert.Equal(acted, Turn(after, actor).ActionTaken);
        Assert.Equal(!acted, Turn(after, actor).HasTurn);
        Assert.Equal(acted, after.PendingFacing is not null);
        AssertSamePathAndEvents(result, other);
        Assert.Equal(after.Revision, otherAfter.Revision);
        Assert.Equal(after.PendingFacing?.ActorId, otherAfter.PendingFacing?.ActorId);
        foreach (var id in new[] { A, B })
        {
            Assert.Equal(Body(after, id).Position, Body(otherAfter, id).Position);
            Assert.Equal(Body(after, id).HasMoved, Body(otherAfter, id).HasMoved);
            Assert.Equal(Combat(after, id), Combat(otherAfter, id)); Assert.Equal(Turn(after, id), Turn(otherAfter, id));
        }
        Assert.Equal(after.Battle.TurnBattle.Turns.RngState, otherAfter.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(before, Snapshot(state)); Assert.Equal(reversedBefore, Snapshot(reversed));
    }

    public static IEnumerable<object[]> SelectedPaths()
    {
        using var document = Contract("flat-ai-movement-contract-v1.json");
        foreach (var row in document.RootElement.GetProperty("vectors").EnumerateArray().Where(r =>
            r.GetProperty("family").GetString() == "flat-route" && r.GetProperty("expected").GetProperty("path").ValueKind != JsonValueKind.Null))
            yield return [row.GetProperty("id").GetString()!];
    }

    [Theory] [MemberData(nameof(SelectedPaths))]
    public void Selected_source_routes_execute_as_confirmed_commands(string id)
    {
        using var document = Contract("flat-ai-movement-contract-v1.json");
        var row = document.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        var input = row.GetProperty("inputs"); var expected = row.GetProperty("expected");
        var state = Active(width: input.GetProperty("width").GetInt32(), depth: input.GetProperty("depth").GetInt32(),
            origin: Point(input.GetProperty("origin")), enemy: Point(input.GetProperty("enemy")),
            move: input.GetProperty("move").GetInt32(), jump: input.GetProperty("jump").GetInt32());
        var actor = A; string before = Snapshot(state);
        var result = Move(state, actor, Point(input.GetProperty("destination"))); var after = Accepted(result);
        Assert.Equal(expected.GetProperty("path").EnumerateArray().Select(Point), result.Path!.Tiles);
        Assert.Equal(expected.GetProperty("packedSteps").EnumerateArray().Select(v => v.GetByte()), result.Path.PackedSteps);
        Assert.Equal(Enum.Parse<BattleFacing>(expected.GetProperty("executionFinalHeading").GetString()!), Combat(after, actor).Facing);
        AssertEvents(state, result, actor); AssertUnrelated(state, after, actor); Assert.Equal(before, Snapshot(state));
    }

    [Theory] [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void Public_Move_then_Attack_and_Attack_then_Move_bind_fully_settled_receipts(bool moveFirst, bool second)
    {
        var actor = second ? B : A; var enemy = second ? A : B;
        var state = Active(second: second, origin: moveFirst ? new(2, 3, 0) : new(2, 2, 0), enemy: new(3, 2, 0), seed: 601690112);
        FacingTurnState pending;
        if (moveFirst)
        {
            var move = Move(state, actor, new(2, 2, 0)); state = Accepted(move);
            Assert.Null(state.PendingFacing); Assert.Equal(BattleFacing.South, Combat(state, actor).Facing);
            pending = Accepted(FacingTurnBattle.Attack(state, new(actor, enemy), new(Weapon, 5)));
        }
        else
        {
            state = Accepted(FacingTurnBattle.Attack(state, new(actor, enemy), new(Weapon, 5)));
            Assert.Equal(new TilePosition(4, 2, 0), Body(state, enemy).Position);
            var moved = Move(state, actor, new(2, 4, 0)); pending = Accepted(moved); AssertEvents(state, moved, actor);
            Assert.Equal(BattleFacing.North, Combat(pending, actor).Facing);
        }
        var receipt = Assert.IsType<FacingCompletionReceipt>(pending.PendingFacing);
        Assert.Same(pending.Battle, receipt.ExpectedBattle); Assert.Same(pending.Lineage, receipt.Lineage);
        Assert.Equal(pending.Revision, receipt.Revision); Assert.Equal(actor, receipt.ActorId);
        Assert.Equal(5, Combat(pending, enemy).Hp); Assert.Equal(new TilePosition(4, 2, 0), Body(pending, enemy).Position);
        Assert.Equal(new FftRandomState(3448561432, 8), pending.Battle.TurnBattle.Turns.RngState);
        var keep = FacingTurnBattle.FinishFacing(pending, actor, receipt, Keep); var finished = Accepted(keep);
        Assert.Equal(Combat(pending, actor).Facing, Combat(finished, actor).Facing);
        Assert.Same(pending.Battle.TurnBattle, finished.Battle.TurnBattle);
        Assert.IsType<FacingSettledEvent>(Assert.Single(keep.Events)); Assert.Null(finished.PendingFacing);
        Assert.Equal(15, Turn(finished, actor).Ct);
    }

    [Fact]
    public void Command_recomputes_current_path_after_a_real_push_and_rejects_the_new_enemy_tile()
    {
        var state = Active(origin: new(2, 2, 0), enemy: new(3, 2, 0), seed: 601690112);
        var stale = FlatOrdinaryMovement.Evaluate(state.Battle, A, Movement, new(4, 2, 0));
        Assert.True(stale.Accepted); // A detour before the push; its endpoint becomes occupied afterward.
        var acted = Accepted(FacingTurnBattle.Attack(state, new(A, B), new(Weapon, 5)));
        Atomic(acted, Move(acted, A, new(4, 2, 0)), path: FlatMovementRejection.InvalidDestination);
        var result = Move(acted, A, new(3, 2, 0)); var pending = Accepted(result);
        Assert.Equal(new byte[] { 0 }, result.Path!.PackedSteps);
        Assert.Equal(BattleFacing.East, Combat(pending, A).Facing); Assert.Equal(new TilePosition(4, 2, 0), Body(pending, B).Position);
        var finish = FacingTurnBattle.FinishFacing(pending, A, pending.PendingFacing, new FacingChoice.ChooseClosedEnemy(B));
        Assert.Equal(BattleFacing.East, Combat(Accepted(finish), A).Facing);
    }

    [Theory] [InlineData(-1, 0, 0)] [InlineData(5, 0, 0)] [InlineData(0, 5, 0)] [InlineData(0, 0, 1)]
    [InlineData(2, 2, 0)] [InlineData(4, 4, 0)]
    public void Invalid_destination_rejects_after_private_turn_calculation_without_any_publication(int x, int y, int layer)
    {
        var state = MarkActForNormalizedVector(Active(ct: 99), A);
        Atomic(state, Move(state, A, new(x, y, layer)), path: FlatMovementRejection.InvalidDestination);
        Assert.Equal(99, Turn(state, A).Ct); Assert.True(Turn(state, A).HasTurn); Assert.True(Turn(state, A).ActionTaken);
    }

    [Fact]
    public void Over_budget_detour_and_zero_Move_reject_without_default_prototype_fallback()
    {
        var state = Active(width: 3, depth: 3, origin: new(0, 1, 0), enemy: new(1, 1, 0), move: 2);
        Assert.True(FlatGridMovementRange.Calculate(state.Battle.TurnBattle.Battle.Map, new(0, 1, 0), 2).Contains(new(2, 1, 0)));
        Atomic(state, Move(state, A, new(2, 1, 0)), path: FlatMovementRejection.InvalidDestination);
        var zero = Active(move: 0); Atomic(zero, Move(zero, A, new(3, 2, 0)), path: FlatMovementRejection.InvalidDestination);
    }

    [Theory] [InlineData("missing-cost")] [InlineData("cost-two")] [InlineData("cost-blocked")]
    [InlineData("enemy-cost")] [InlineData("move256")] [InlineData("jump256")]
    [InlineData("blocked")] [InlineData("height")] [InlineData("water")] [InlineData("slope")]
    [InlineData("stop")] [InlineData("surface")] [InlineData("half")] [InlineData("ceiling")]
    [InlineData("untargetable")] [InlineData("body")] [InlineData("fly")] [InlineData("traits")]
    [InlineData("upper")] [InlineData("overlap")] [InlineData("third")]
    public void Unsupported_whole_profiles_are_typed_atomic_rejections(string kind)
    {
        var valid = Active(); var bad = Setup(geometry: kind);
        var ready = TurnCycle.Advance(bad.TurnBattle.Turns); Assert.True(ready.Accepted);
        var bodies = bad.TurnBattle.Battle.Units;
        var next = new AttackBattleSnapshot(new(new(bad.TurnBattle.Battle.Map, bodies, ready.State.RngState), ready.State), bad.Combatants);
        // Internal friend invariant fixture only: public Begin itself rejects some of these malformed geometries.
        var state = new FacingTurnState(next, valid.Profile, valid.Revision, null, valid.Lineage);
        Atomic(state, Move(state, A, new(3, 2, 0)), path: FlatMovementRejection.UnsupportedProfile);
    }

    [Theory] [InlineData(17, 5)] [InlineData(5, 17)]
    public void Large_legacy_maps_reject_only_the_explicit_path_entry(int width, int depth)
    {
        var state = Active(width: width, depth: depth);
        Atomic(state, Move(state, A, new(3, 2, 0)), path: FlatMovementRejection.UnsupportedProfile);
        Assert.True(FacingTurnBattle.Move(state, new(A, new(3, 2, 0))).Accepted);
    }

    [Fact]
    public void Authority_pending_and_revision_precedence_is_exact_and_nulls_are_programmer_errors()
    {
        var active = Active(); var unknown = new BattleUnitId("sample:unit/missing");
        Atomic(active, Move(active, unknown, new(3, 2, 0)), turn: TurnCycleRejection.UnknownUnit);
        Atomic(active, Move(active, B, new(3, 2, 0)), turn: TurnCycleRejection.InactiveUnit);
        var moved = Accepted(Move(active, A, new(3, 2, 0)));
        Atomic(moved, Move(moved, A, new(0, 0, 0)), turn: TurnCycleRejection.MovementAlreadyTaken);
        var query = FlatOrdinaryMovement.Evaluate(moved.Battle, A, Movement, new(2, 2, 0)); Assert.True(query.Accepted);
        var maximum = Counters(active, ulong.MaxValue);
        Atomic(maximum, Move(maximum, unknown, new(-1, -1, 0)), facing: FacingTurnRejection.RevisionExhausted);
        var pending = Pending(); var pendingMaximum = Counters(pending, ulong.MaxValue);
        Atomic(pendingMaximum, Move(pendingMaximum, unknown, new(-1, -1, 0)), facing: FacingTurnRejection.FacingPending);
        var fresh = FacingTurnBattle.Begin(Setup(), Sword);
        Atomic(fresh, Move(fresh, A, new(3, 2, 0)), turn: TurnCycleRejection.InactiveUnit);
        Atomic(active, FacingTurnPathMovement.MoveAlongPath(active, new(A, new(3, 2, 0)), new(Ground, A, unknown)), path: FlatMovementRejection.UnsupportedProfile);
        Assert.Throws<ArgumentNullException>(() => FacingTurnPathMovement.MoveAlongPath(null!, new(A, new(3, 2, 0)), Movement));
        Assert.Throws<ArgumentNullException>(() => FacingTurnPathMovement.MoveAlongPath(active, null!, Movement));
        Assert.Throws<ArgumentNullException>(() => FacingTurnPathMovement.MoveAlongPath(active, new(A, new(3, 2, 0)), null!));
    }

    [Fact]
    public void All_existing_commands_and_path_Move_are_blocked_while_facing_is_pending()
    {
        var pending = Pending(); string before = Snapshot(pending);
        foreach (var result in new[] { FacingTurnBattle.Advance(pending), FacingTurnBattle.Move(pending, new(A, new(0, 0, 0))),
            FacingTurnBattle.Attack(pending, new(A, B), null), FacingTurnBattle.WaitWithFacing(pending, A, Keep) })
        { Assert.False(result.Accepted); Assert.Same(pending, result.State); Assert.Empty(result.Events); Assert.Equal(FacingTurnRejection.FacingPending, result.FacingRejection); }
        Atomic(pending, Move(pending, A, new(0, 0, 0)), facing: FacingTurnRejection.FacingPending);
        var failed = FacingTurnBattle.FinishFacing(pending, A, pending.PendingFacing, new FacingChoice.Fixed((BattleFacing)99));
        Assert.False(failed.Accepted); Assert.Same(pending, failed.State); Assert.Equal(before, Snapshot(pending));
    }

    public static IEnumerable<object[]> FinishProposals()
    {
        using var document = Contract("voluntary-move-contract-v1.json");
        foreach (var row in document.RootElement.GetProperty("vectors").EnumerateArray().Where(r => r.GetProperty("family").GetString() == "proposed-finish"))
            yield return [row.GetProperty("id").GetString()!];
    }

    [Theory] [MemberData(nameof(FinishProposals))]
    public void Executes_12_ZOD122_completion_proposals_on_path_receipts(string id)
    {
        using var document = Contract("voluntary-move-contract-v1.json");
        var row = document.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        var input = row.GetProperty("inputs"); var expected = row.GetProperty("expected");
        var pending = id == "finish-after-KO-fixed" ? LethalPending() : Pending();
        var receipt = pending.PendingFacing; BattleUnitId actor = A;
        if (!input.GetProperty("pending").GetBoolean()) pending = Accepted(FacingTurnBattle.FinishFacing(pending, A, receipt, Keep));
        if (!input.GetProperty("identityMatches").GetBoolean()) receipt = null;
        if (!input.GetProperty("snapshotMatches").GetBoolean())
        { var wrong = new FacingCompletionReceipt(A, pending.Revision, Active().Battle, pending.Lineage); pending = new(pending.Battle, pending.Profile, pending.Revision, wrong, pending.Lineage); receipt = wrong; }
        if (!input.GetProperty("lineageMatches").GetBoolean()) pending = new(pending.Battle, pending.Profile, pending.Revision, receipt, new object());
        if (!input.GetProperty("actorMatches").GetBoolean()) actor = B;
        if (!input.GetProperty("revisionAvailable").GetBoolean()) { pending = Counters(pending, ulong.MaxValue); receipt = pending.PendingFacing; }
        if (input.GetProperty("active").GetBoolean())
        { var active = Active(); var wrong = new FacingCompletionReceipt(A, active.Revision, active.Battle, active.Lineage); pending = new(active.Battle, active.Profile, active.Revision, wrong, active.Lineage); receipt = wrong; }
        string before = Snapshot(pending); var choiceName = input.GetProperty("choice").GetString();
        FacingChoice choice = choiceName == "KeepCurrent" ? Keep : new FacingChoice.Fixed(Enum.TryParse<BattleFacing>(choiceName, out var f) ? f : (BattleFacing)99);
        var result = FacingTurnBattle.FinishFacing(pending, actor, receipt, choice);
        Assert.Equal(expected.GetProperty("accepted").GetBoolean(), result.Accepted);
        if (!result.Accepted)
        { Assert.Equal(expected.GetProperty("reason").GetString(), result.FacingRejection.ToString()); Assert.Same(pending, result.State); Assert.Empty(result.Events); }
        else
        {
            Assert.Equal(Enum.Parse<BattleFacing>(expected.GetProperty("facing").GetString()!), Combat(result.State, A).Facing);
            Assert.Null(result.State.PendingFacing); Assert.Same(pending.Battle.TurnBattle, result.State.Battle.TurnBattle);
            Assert.Equal(15, Turn(result.State, A).Ct); Assert.IsType<FacingSettledEvent>(Assert.Single(result.Events));
            Assert.Equal(FacingTurnRejection.MissingCompletion, FacingTurnBattle.FinishFacing(result.State, A, receipt, Keep).FacingRejection);
        }
        Assert.Equal(before, Snapshot(pending));
    }

    [Fact]
    public void Path_receipts_reject_cross_branch_cross_run_and_earlier_turn_authorization()
    {
        var active = MarkActForNormalizedVector(Active(origin: new(0, 0, 0), enemy: new(2, 2, 0), width: 3, depth: 3), A);
        var left = Accepted(Move(active, A, new(1, 1, 0))); var right = Accepted(Move(active, A, new(1, 1, 0)));
        Assert.Equal(Snapshot(left), Snapshot(right)); Assert.NotSame(left.PendingFacing, right.PendingFacing);
        Assert.Equal(FacingTurnRejection.StaleCompletion, FacingTurnBattle.FinishFacing(right, A, left.PendingFacing, Keep).FacingRejection);
        var separate = Pending();
        Assert.Equal(FacingTurnRejection.StaleCompletion, FacingTurnBattle.FinishFacing(separate, A, left.PendingFacing, Keep).FacingRejection);
        Assert.Equal(FacingTurnRejection.WrongActor, FacingTurnBattle.FinishFacing(left, B, left.PendingFacing, Keep).FacingRejection);
        var old = left.PendingFacing; var state = Accepted(FacingTurnBattle.FinishFacing(left, A, old, Keep));
        state = Accepted(FacingTurnBattle.Advance(state)); Assert.Equal(B, state.Battle.TurnBattle.Turns.ActiveUnitId);
        state = Accepted(FacingTurnBattle.WaitWithFacing(state, B, Keep));
        state = Accepted(FacingTurnBattle.Advance(state)); Assert.Equal(A, state.Battle.TurnBattle.Turns.ActiveUnitId);
        state = MarkActForNormalizedVector(state, A); state = Accepted(Move(state, A, new(0, 1, 0)));
        Assert.Equal(FacingTurnRejection.StaleCompletion, FacingTurnBattle.FinishFacing(state, A, old, Keep).FacingRejection);
        Assert.True(FacingTurnBattle.FinishFacing(state, A, state.PendingFacing, Keep).Accepted);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Last_revision_and_exhausted_RNG_preserve_existing_counter_semantics(bool acted)
    {
        var state = Active(count: ulong.MaxValue - 1);
        if (acted) state = MarkActForNormalizedVector(state, A);
        state = Counters(state, ulong.MaxValue - 1);
        var result = Move(state, A, new(3, 2, 0)); var after = Accepted(result);
        Assert.Equal(ulong.MaxValue, after.Revision); Assert.Equal(ulong.MaxValue, after.Battle.TurnBattle.Turns.RngState.DrawCount);
        Assert.Equal(acted, after.PendingFacing is not null); AssertUnrelated(state, after, A);
        Atomic(after, Move(after, A, new(2, 2, 0)), facing: acted ? FacingTurnRejection.FacingPending : FacingTurnRejection.RevisionExhausted);
        if (acted) Assert.Equal(FacingTurnRejection.RevisionExhausted, FacingTurnBattle.FinishFacing(after, A, after.PendingFacing, Keep).FacingRejection);
    }

    [Theory] [InlineData(BattleFacing.East)] [InlineData(BattleFacing.North)] [InlineData(BattleFacing.West)] [InlineData(BattleFacing.South)]
    public void Same_heading_walk_and_same_facing_completion_still_emit_events_and_consume_once(BattleFacing face)
    {
        var destination = face switch { BattleFacing.East => new TilePosition(3, 2, 0), BattleFacing.North => new(2, 3, 0),
            BattleFacing.West => new(1, 2, 0), _ => new(2, 1, 0) };
        var state = MarkActForNormalizedVector(Active(facing: face), A);
        var result = Move(state, A, destination); var pending = Accepted(result);
        Assert.Equal(face, result.Path!.FinalHeading); Assert.Equal(face, Assert.IsType<VoluntaryPathCompletedEvent>(result.Events[1]).PreviousFacing);
        var finish = FacingTurnBattle.FinishFacing(pending, A, pending.PendingFacing, Keep); Assert.True(finish.Accepted);
        Assert.Equal(face, Combat(finish.State, A).Facing); Assert.Single(finish.Events); Assert.Null(finish.State.PendingFacing);
        Assert.Equal(pending.Revision + 1, finish.State.Revision); Assert.Equal(15, Turn(finish.State, A).Ct);
    }

    [Fact]
    public void Prefix_suffix_replay_repeats_paths_events_and_semantic_state_with_fresh_receipts()
    {
        var prefix = MarkActForNormalizedVector(Active(), A); string before = Snapshot(prefix);
        var left = Move(prefix, A, new(3, 2, 0)); var right = Move(prefix, A, new(3, 2, 0));
        AssertEvents(prefix, left, A); AssertEvents(prefix, right, A);
        AssertSamePathAndEvents(left, right);
        Assert.Equal(Snapshot(left.State), Snapshot(right.State)); Assert.Equal(before, Snapshot(prefix));
        Assert.NotSame(left.State.PendingFacing, right.State.PendingFacing);
        var finishedLeft = Accepted(FacingTurnBattle.FinishFacing(left.State, A, left.State.PendingFacing, Keep));
        var finishedRight = Accepted(FacingTurnBattle.FinishFacing(right.State, A, right.State.PendingFacing, Keep));
        Assert.Equal(Snapshot(finishedLeft), Snapshot(finishedRight));
        Assert.Equal(Snapshot(Accepted(FacingTurnBattle.Advance(finishedLeft))), Snapshot(Accepted(FacingTurnBattle.Advance(finishedRight))));
    }

    [Fact]
    public void Post_KO_move_rejects_but_existing_facing_only_completion_remains_available()
    {
        var active = Active(origin: new(2, 2, 0), enemy: new(3, 2, 0), seed: 601690112);
        var killed = Accepted(FacingTurnBattle.Attack(active, new(A, B), new(Weapon, 64)));
        Assert.True(Turn(killed, A).HasTurn); Assert.Equal(0, Combat(killed, B).Hp);
        Atomic(killed, Move(killed, A, new(2, 3, 0)), path: FlatMovementRejection.UnsupportedProfile);
        var pending = LethalPending();
        Atomic(pending, Move(pending, A, new(0, 0, 0)), facing: FacingTurnRejection.FacingPending);
        Assert.Equal(FacingTurnRejection.NoLivingEnemy, FacingTurnBattle.FinishFacing(pending, A, pending.PendingFacing, new FacingChoice.ChooseClosedEnemy(B)).FacingRejection);
        Assert.True(FacingTurnBattle.FinishFacing(pending, A, pending.PendingFacing, Keep).Accepted);
        Assert.True(FacingTurnBattle.FinishFacing(pending, A, pending.PendingFacing, new FacingChoice.Fixed(BattleFacing.North)).Accepted);
    }

    [Fact]
    public void Legacy_Move_and_query_preserve_facing_while_path_commands_apply_heading()
    {
        var state = Active(); string before = Snapshot(state);
        var query = FlatOrdinaryMovement.Evaluate(state.Battle, A, Movement, new(3, 2, 0)); Assert.True(query.Accepted);
        Assert.Equal(BattleFacing.West, Combat(state, A).Facing); Assert.Equal(before, Snapshot(state));
        var legacy = Accepted(FacingTurnBattle.Move(state, new(A, new(3, 2, 0)))); Assert.Equal(BattleFacing.West, Combat(legacy, A).Facing);
        var current = Accepted(Move(state, A, new(3, 2, 0))); Assert.Equal(BattleFacing.East, Combat(current, A).Facing);
    }

    private static void AssertSamePathAndEvents(PathMoveResult expected, PathMoveResult actual)
    {
        Assert.Equal(expected.Path!.Tiles.ToArray(), actual.Path!.Tiles.ToArray());
        Assert.Equal(expected.Path.PackedSteps.ToArray(), actual.Path.PackedSteps.ToArray());
        Assert.Equal(expected.Path.FinalHeading, actual.Path.FinalHeading);
        // ImmutableArray record equality is storage identity; compare the path event by its payload instead.
        Assert.Equal(expected.Events.Select(e => e is VoluntaryPathCompletedEvent p ? JsonSerializer.Serialize(p) : e.ToString()),
            actual.Events.Select(e => e is VoluntaryPathCompletedEvent p ? JsonSerializer.Serialize(p) : e.ToString()));
    }

    private static JsonDocument Contract(string name) => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)));
    private static TilePosition Point(JsonElement p) => new(p[0].GetInt32(), p[1].GetInt32(), 0);
    private static BattleUnitState Body(FacingTurnState state, BattleUnitId id) => state.Battle.TurnBattle.Battle.Units.Single(u => u.Id == id);
    private static AttackUnitState Combat(FacingTurnState state, BattleUnitId id) => state.Battle.Combatants.Single(u => u.UnitId == id);
    private static TurnUnitState Turn(FacingTurnState state, BattleUnitId id) => state.Battle.TurnBattle.Turns.Units.Single(u => u.Id == id);
    private static PathMoveResult Move(FacingTurnState state, BattleUnitId actor, TilePosition destination) => FacingTurnPathMovement.MoveAlongPath(state, new(actor, destination), Movement);
    private static FacingTurnState Accepted(FacingTurnResult result) { Assert.True(result.Accepted, result.FacingRejection.ToString()); return result.State; }
    private static FacingTurnState Accepted(PathMoveResult result)
    { Assert.True(result.Accepted, $"{result.FacingRejection}/{result.TurnRejection}/{result.PathRejection}"); Assert.Equal(FacingTurnRejection.None, result.FacingRejection); Assert.Equal(TurnCycleRejection.None, result.TurnRejection); Assert.Equal(FlatMovementRejection.None, result.PathRejection); Assert.NotNull(result.Path); return result.State; }
    private static void Atomic(FacingTurnState state, PathMoveResult result, FacingTurnRejection facing = FacingTurnRejection.None,
        TurnCycleRejection turn = TurnCycleRejection.None, FlatMovementRejection path = FlatMovementRejection.None)
    { Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events); Assert.Null(result.Path); Assert.Equal(facing, result.FacingRejection); Assert.Equal(turn, result.TurnRejection); Assert.Equal(path, result.PathRejection); }
    private static void AssertEvents(FacingTurnState before, PathMoveResult result, BattleUnitId actor)
    {
        var movement = Assert.IsType<TurnBattleMovementEvent>(Assert.IsType<AttackTurnBattleEvent>(result.Events[0]).Event);
        Assert.Equal(new BattleMovementEvent(actor, Body(before, actor).Position, Body(result.State, actor).Position), movement.Movement);
        var path = Assert.IsType<VoluntaryPathCompletedEvent>(result.Events[1]); Assert.Equal(actor, path.ActorId);
        Assert.Same(result.Path, path.Path); Assert.Equal(Combat(before, actor).Facing, path.PreviousFacing);
        Assert.Equal(new TurnChoiceConsumedEvent(actor, TurnCommandKind.ConfirmMove), Assert.IsType<TurnBattleClockEvent>(Assert.IsType<AttackTurnBattleEvent>(result.Events[2]).Event).Event);
        Assert.Equal(Turn(before, actor).ActionTaken ? 4 : 3, result.Events.Length);
        if (Turn(before, actor).ActionTaken)
        {
            Assert.Equal(new TurnEndedEvent(actor, Turn(before, actor).Ct, Math.Min(60, Turn(before, actor).Ct)), Assert.IsType<TurnBattleClockEvent>(Assert.IsType<AttackTurnBattleEvent>(result.Events[3]).Event).Event);
            var receipt = Assert.IsType<FacingCompletionReceipt>(result.State.PendingFacing);
            Assert.Same(result.State.Battle, receipt.ExpectedBattle); Assert.Same(before.Lineage, receipt.Lineage); Assert.Equal(result.State.Revision, receipt.Revision);
        }
        else Assert.Null(result.State.PendingFacing);
    }
    private static void AssertUnrelated(FacingTurnState before, FacingTurnState after, BattleUnitId actor)
    {
        var other = actor == A ? B : A;
        Assert.Same(before.Profile, after.Profile); Assert.Same(before.Lineage, after.Lineage);
        Assert.Same(before.Battle.TurnBattle.Battle.Map, after.Battle.TurnBattle.Battle.Map);
        Assert.Same(Body(before, actor).MovementProfile, Body(after, actor).MovementProfile);
        Assert.Same(Body(before, other), Body(after, other)); Assert.Same(Combat(before, other), Combat(after, other));
        Assert.Equal(Combat(before, actor).WithResult(Combat(before, actor).Hp, Combat(after, actor).Facing, Combat(before, actor).KoCount), Combat(after, actor));
        Assert.Equal(Turn(before, other), Turn(after, other)); Assert.Equal(before.Battle.TurnBattle.Turns.Ticks, after.Battle.TurnBattle.Turns.Ticks);
        Assert.Equal(before.Battle.TurnBattle.Turns.RngState, after.Battle.TurnBattle.Turns.RngState);
    }
    private static string Snapshot(FacingTurnState s) => JsonSerializer.Serialize(new
    {
        s.Revision, Pending = s.PendingFacing is { } p ? new { p.ActorId, p.Revision } : null,
        Units = s.Battle.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved,
            u.MovementProfile.Move, u.MovementProfile.Jump, u.MovementProfile.BodyHeight, u.MovementProfile.Mode, u.MovementProfile.Traits,
            Costs = u.MovementProfile.TerrainCosts.Select(c => new { Id = c.Key.Value, c.Value }) }),
        s.Battle.Combatants, s.Battle.TurnBattle.Turns
    });
    private static FacingTurnState Pending()
    {
        var state = MarkActForNormalizedVector(Active(width: 3, depth: 3, origin: new(0, 0, 0), enemy: new(2, 2, 0)), A);
        return Accepted(Move(state, A, new(1, 1, 0)));
    }
    private static FacingTurnState LethalPending()
    {
        var state = Accepted(Move(Active(origin: new(1, 2, 0), enemy: new(3, 2, 0)), A, new(2, 2, 0)));
        return Accepted(FacingTurnBattle.Attack(state, new(A, B), new(Weapon, 64)));
    }
    private static FacingTurnState MarkActForNormalizedVector(FacingTurnState state, BattleUnitId actor)
    {
        var act = AttackBattleSimulator.ConfirmAct(state.Battle, actor); Assert.True(act.Accepted);
        return new(act.State, state.Profile, state.Revision + 1, null, state.Lineage);
    }
    private static FacingTurnState Counters(FacingTurnState state, ulong revision) => new(state.Battle, state.Profile, revision,
        state.PendingFacing is { } p ? new FacingCompletionReceipt(p.ActorId, revision, state.Battle, state.Lineage) : null, state.Lineage);
    private static FacingTurnState Active(bool second = false, bool reverse = false, int width = 5, int depth = 5,
        TilePosition? origin = null, TilePosition? enemy = null, int move = 4, int jump = 0, int ct = 15,
        uint seed = 57, ulong count = 0, BattleFacing facing = BattleFacing.West)
        => Accepted(FacingTurnBattle.Advance(FacingTurnBattle.Begin(Setup(second, reverse, width, depth, origin, enemy, move, jump, ct, seed, count, facing), Sword)));

    private static AttackBattleSnapshot Setup(bool second = false, bool reverse = false, int width = 5, int depth = 5,
        TilePosition? origin = null, TilePosition? enemy = null, int move = 4, int jump = 0, int ct = 15,
        uint seed = 57, ulong count = 0, BattleFacing facing = BattleFacing.West, string? geometry = null)
    {
        var actor = second ? B : A; var other = second ? A : B; int layers = geometry == "upper" ? 2 : 1;
        var tiles = Enumerable.Repeat(new BattleTile(Ground, 10), width * depth * layers).ToArray();
        tiles[^1] = geometry switch
        {
            "blocked" => new(Ground, 10, movementFlags: BattleTileMovementFlags.Blocked),
            "untargetable" => new(Ground, 10, movementFlags: BattleTileMovementFlags.Untargetable),
            "stop" => new(Ground, 10, movementFlags: BattleTileMovementFlags.CannotStop),
            "height" => new(Ground, 11), "half" => new(Ground, 10, halfHeight: 1), "water" => new(Ground, 10, waterDepth: 1),
            "ceiling" => new(Ground, 10, ceilingDepth: 1), "slope" => new(Ground, 10, slope: TileSlope.FromPackedByte(1)),
            "surface" => new(new("sample:terrain/other"), 10), _ => tiles[^1]
        };
        UnitMovementProfile Profile(bool foe) => new(geometry == "move256" ? 256 : move, geometry == "jump256" ? 256 : jump,
            geometry == "body" ? 7 : 6, geometry == "fly" ? UnitMovementMode.Fly : UnitMovementMode.Normal,
            geometry == "traits" ? UnitMovementTraits.AnyGround : UnitMovementTraits.None,
            geometry == "missing-cost" || foe && geometry == "enemy-cost" ? [] :
            new[] { KeyValuePair.Create(Ground, geometry == "cost-blocked" ? TerrainMovementCost.Blocked : new TerrainMovementCost(geometry == "cost-two" ? 2 : 1)) });
        var bodies = new List<BattleUnitState> { new(actor, origin ?? new(2, 2, 0), Profile(false)),
            new(other, geometry == "overlap" ? origin ?? new(2, 2, 0) : enemy ?? new(4, 4, 0), Profile(true)) };
        var turns = new List<TurnUnitState> { new(actor, 10, ct + 90, 0), new(other, 12, 0, 1) };
        AttackUnitState Combatant(BattleUnitId id, BattleFacing face) => new(id, 50, 50, 0, 0, 5, 0, 0, 0, 0, 70, 0, face, AttackUnitStatus.None, 255, Weapon);
        var combat = new List<AttackUnitState> { Combatant(actor, facing), Combatant(other, BattleFacing.North) };
        if (geometry == "third")
        {
            var c = new BattleUnitId("sample:unit/c"); bodies.Add(new(c, new(0, 0, 0), Profile(false)));
            turns.Add(new(c, 10, 0, 2)); combat.Add(Combatant(c, BattleFacing.East));
        }
        var rng = new FftRandomState(seed, count);
        return new(new(new(new(width, depth, layers, tiles), reverse ? bodies.AsEnumerable().Reverse() : bodies, rng),
            new(reverse ? turns.AsEnumerable().Reverse() : turns, rng)), reverse ? combat.AsEnumerable().Reverse() : combat);
    }
}
