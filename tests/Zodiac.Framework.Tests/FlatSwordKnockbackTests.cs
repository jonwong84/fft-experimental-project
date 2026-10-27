using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

/// <summary>SKB-001..020; source-derived arithmetic at decomp 11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
public sealed class FlatSwordKnockbackTests
{
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly FlatSwordBattleProfile Profile = new(Ground);

    private static JsonDocument Contract() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "sword-flat-knockback-contract-v1.json")));

    public static IEnumerable<object[]> ContractVectors()
    {
        using var contract = Contract();
        foreach (var row in contract.RootElement.GetProperty("vectors").EnumerateArray())
            yield return [row.GetProperty("id").GetString()!];
    }

    [Theory] [MemberData(nameof(ContractVectors))]
    public void Executes_all_35_source_contract_vectors_without_rewriting_historical_expectations(string id)
    {
        using var contract = Contract();
        var row = contract.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        var input = row.GetProperty("inputs"); var expected = row.GetProperty("expected");
        var before = State(seed: input.GetProperty("seed").GetUInt32(), count: input.GetProperty("drawCount").GetUInt64(),
            pa: input.GetProperty("pa").GetInt32(), hp: input.GetProperty("hp").GetInt32(),
            actor: Point(input.GetProperty("actor")), target: Point(input.GetProperty("target")),
            width: input.GetProperty("mapSize")[0].GetInt32(), depth: input.GetProperty("mapSize")[1].GetInt32(),
            actorFacing: Face(input.GetProperty("actorFacing")), targetFacing: Face(input.GetProperty("targetFacing")),
            evade: input.GetProperty("classEvade").GetInt32(), actorBrave: input.GetProperty("actorBrave").GetInt32(),
            targetBrave: input.GetProperty("targetBrave").GetInt32());
        string original = Snapshot(before);
        var result = Attack(before, input.GetProperty("power").GetInt32());
        Assert.True(result.Accepted);
        Assert.Equal(new AttackResolvedEvent(A, B, expected.GetProperty("hit").GetBoolean(),
            expected.GetProperty("critical").GetBoolean(), expected.GetProperty("damage").GetInt32()),
            Assert.Single(result.Events.OfType<AttackResolvedEvent>()));
        Assert.Equal(expected.GetProperty("hp").GetInt32(), Unit(result.State, B).Hp);
        Assert.Equal(Point(expected.GetProperty("target")), Position(result.State, B));
        Assert.Equal(Position(before, A), Position(result.State, A));
        Assert.Equal(Face(expected.GetProperty("actorFacing")), Unit(result.State, A).Facing);
        Assert.Equal(Face(expected.GetProperty("targetFacing")), Unit(result.State, B).Facing);
        Assert.Equal(new FftRandomState(expected.GetProperty("rngWord").GetUInt32(), expected.GetProperty("drawCount").GetUInt64()),
            result.State.TurnBattle.Turns.RngState);
        Assert.Equal(result.State.TurnBattle.Turns.RngState, result.State.TurnBattle.Battle.RngState);
        Assert.Equal(expected.GetProperty("moved").GetBoolean() ? 1 : 0, result.Events.OfType<SwordKnockbackEvent>().Count());
        Assert.Equal(before.TurnBattle.Turns.Units.Single(u => u.Id == B), result.State.TurnBattle.Turns.Units.Single(u => u.Id == B));
        Assert.Equal(original, Snapshot(before));
    }

    public static IEnumerable<object[]> CardinalFacings()
    {
        foreach (var (dx, dy, front) in new[] { (1, 0, BattleFacing.West), (0, 1, BattleFacing.South),
                     (-1, 0, BattleFacing.East), (0, -1, BattleFacing.North) })
        foreach (var targetFacing in Enum.GetValues<BattleFacing>())
        foreach (uint seed in new[] { 1u, 57u })
            yield return [dx, dy, front, targetFacing, seed];
    }

    [Theory] [MemberData(nameof(CardinalFacings))]
    public void Cardinal_target_facing_controls_hits_misses_and_critical_pushes(int dx, int dy,
        BattleFacing front, BattleFacing targetFacing, uint seed)
    {
        var target = new TilePosition(2 + dx, 2 + dy, 0);
        var before = State(seed: seed, actor: new(2, 2, 0), target: target,
            targetFacing: targetFacing, evade: 100);
        var result = Attack(before);
        bool hit = targetFacing != front, critical = hit && seed == 57;
        Assert.True(result.Accepted);
        Assert.Equal(new AttackResolvedEvent(A, B, hit, critical, hit ? critical ? 45 : 25 : 0), result.Events[1]);
        Assert.Equal(critical ? new(target.X + dx, target.Y + dy, 0) : target, Position(result.State, B));
        Assert.Equal(Unit(before, A).Facing, Unit(result.State, A).Facing); Assert.Equal(targetFacing, Unit(result.State, B).Facing);
        Assert.Equal(hit ? critical ? 7ul : 6ul : 4ul, result.State.TurnBattle.Turns.RngState.DrawCount);
        Assert.Equal(hit ? seed == 57 ? 3448561432u : 2516284547u : seed == 57 ? 3587663605u : 3295386429u,
            result.State.TurnBattle.Turns.RngState.Value);
    }

    [Theory] [InlineData(BattleFacing.East)] [InlineData(BattleFacing.North)]
    [InlineData(BattleFacing.West)] [InlineData(BattleFacing.South)]
    public void Critical_push_preserves_the_actor_facing(BattleFacing facing)
    {
        var before = State(actorFacing: facing); string original = Snapshot(before);
        var result = Attack(before);
        Assert.True(result.Accepted); Assert.Equal(facing, Unit(result.State, A).Facing);
        Assert.Equal(new AttackResolvedEvent(A, B, true, true, 45), result.Events[1]);
        Assert.Equal(new TilePosition(4, 2, 0), Position(result.State, B));
        Assert.Equal(original, Snapshot(before));
    }

    [Fact]
    public void Legacy_entry_point_still_rejects_interior_even_when_the_selected_seed_is_noncritical()
    {
        var before = State(seed: 1);
        Atomic(before, AttackBattleSimulator.Attack(before, new(A, B), new(Weapon, 5)), AttackCommandRejection.UnsupportedSetup);
        Assert.True(Attack(before).Accepted);
        Assert.Equal(FlatSwordBattleProfile.RulesId, "fft:rules/sword-flat-knockback-v1");
        Assert.Equal(Ground, Profile.OrdinaryGroundSurface);
    }

    [Theory] [InlineData(22402u, true, 7ul)] [InlineData(27894u, false, 6ul)]
    public void Critical_threshold_1310_1311_retains_original_draw_branch(uint seed, bool critical, ulong count)
    {
        var result = Attack(State(seed: seed));
        Assert.True(result.Accepted); Assert.Equal(critical, Assert.Single(result.Events.OfType<AttackResolvedEvent>()).Critical);
        Assert.Equal(critical ? 1 : 0, result.Events.OfType<SwordKnockbackEvent>().Count());
        Assert.Equal(count, result.State.TurnBattle.Turns.RngState.DrawCount);
    }

    [Theory]
    [InlineData(1772728485u, 25, 3429651764u)] // Sixth raw 0.
    [InlineData(1713073460u, 25, 962748671u)]  // Sixth raw 6553: last zero bonus at PA5.
    [InlineData(475580580u, 30, 1802140207u)] // Sixth raw 6554: first +1 bonus.
    [InlineData(862737717u, 45, 442776580u)]  // Sixth raw 32767.
    public void Critical_bonus_threshold_and_endpoints_keep_the_seventh_proc_draw(uint seed, int damage, uint word)
    {
        var result = Attack(State(seed: seed, hp: 100));
        Assert.Equal(new AttackResolvedEvent(A, B, true, true, damage), result.Events[1]);
        Assert.Equal(new FftRandomState(word, 7), result.State.TurnBattle.Turns.RngState);
        Assert.Single(result.Events.OfType<SwordKnockbackEvent>());
    }

    [Theory] [InlineData(44, 0, false)] [InlineData(45, 0, false)] [InlineData(46, 1, true)]
    public void Lethal_equality_cancels_position_after_all_draws_and_wraps_KO_byte(int hp, int finalHp, bool moved)
    {
        var result = Attack(State(hp: hp, ko: 255));
        Assert.Equal(finalHp, Unit(result.State, B).Hp);
        Assert.Equal(finalHp == 0 ? 0 : 255, Unit(result.State, B).KoCount);
        Assert.Equal(moved ? new(4, 2, 0) : new TilePosition(3, 2, 0), Position(result.State, B));
        Assert.Equal(new FftRandomState(3448561432, 7), result.State.TurnBattle.Turns.RngState);
        Assert.Equal(moved ? 1 : 0, result.Events.OfType<SwordKnockbackEvent>().Count());
        Assert.Equal(finalHp == 0 ? AttackUnitStatus.Dead : AttackUnitStatus.Critical, Unit(result.State, B).Status);
    }

    [Theory] [InlineData(0, 5, 5)] [InlineData(5, 0, 0)] [InlineData(255, 64, 999)]
    public void Zero_PA_zero_power_and_cap_preserve_push_and_no_damage_means_no_HP_event(int pa, int power, int damage)
    {
        var result = Attack(State(pa: pa, hp: 2000), power);
        Assert.Equal(new AttackResolvedEvent(A, B, true, true, damage), result.Events[1]);
        Assert.Equal(2000 - damage, Unit(result.State, B).Hp);
        Assert.Single(result.Events.OfType<SwordKnockbackEvent>());
        Assert.Equal(damage == 0 ? 0 : 1, result.Events.OfType<AttackHpChangedEvent>().Count());
    }

    [Fact]
    public void Signed16_gate_rejects_before_draw_even_for_normal_hit_seed()
    {
        var before = State(seed: 1, pa: 255, count: ulong.MaxValue);
        Atomic(before, Attack(before, 65), AttackCommandRejection.UnsupportedSetup);
    }

    public static IEnumerable<object[]> DrawBudgets()
    {
        foreach (var (seed, evade, required) in new[] { (57u, 100, 4), (1u, 0, 6), (57u, 0, 7) })
            for (int remaining = 0; remaining <= required; remaining++) yield return [seed, evade, required, remaining];
    }
    [Theory] [MemberData(nameof(DrawBudgets))]
    public void Every_draw_budget_rejects_atomically_or_finishes_at_ulong_max(uint seed, int evade, int required, int remaining)
    {
        var before = State(seed: seed, evade: evade, count: ulong.MaxValue - (ulong)remaining);
        string original = Snapshot(before); var result = Attack(before);
        if (remaining < required) Atomic(before, result, AttackCommandRejection.RandomDrawCountExhausted);
        else
        {
            Assert.True(result.Accepted); Assert.Equal(ulong.MaxValue, result.State.TurnBattle.Turns.RngState.DrawCount);
            Assert.Equal(result.State.TurnBattle.Turns.RngState, result.State.TurnBattle.Battle.RngState);
        }
        Assert.Equal(original, Snapshot(before));
    }

    [Theory] [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void Forced_position_preserves_targets_full_turn_and_movement_profile(bool moved, bool acted)
    {
        var before = State(targetMoved: moved, targetActed: acted);
        var result = Attack(before);
        Assert.Equal(before.TurnBattle.Turns.Units.Single(u => u.Id == B), result.State.TurnBattle.Turns.Units.Single(u => u.Id == B));
        var body = result.State.TurnBattle.Battle.Units.Single(u => u.Id == B);
        Assert.Equal(moved, body.HasMoved);
        Assert.Same(before.TurnBattle.Battle.Units.Single(u => u.Id == B).MovementProfile, body.MovementProfile);
        Assert.Equal(12ul, result.State.TurnBattle.Turns.Ticks);
        Assert.Equal(before.Combatants[0], result.State.Combatants[0]);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Act_Move_and_Move_Act_keep_choice_and_event_order(bool moveFirst)
    {
        var before = State(actor: moveFirst ? new(1, 2, 0) : new(2, 2, 0));
        var state = before;
        if (moveFirst)
        {
            var move = AttackBattleSimulator.Move(state, new(A, new(2, 2, 0)));
            Assert.True(move.Accepted); state = move.State;
        }
        var result = Attack(state);
        Assert.True(result.Accepted);
        Assert.IsType<AttackTurnBattleEvent>(result.Events[0]);
        if (moveFirst)
        {
            Assert.Null(result.State.TurnBattle.Turns.ActiveUnitId);
            Assert.Equal(15, result.State.TurnBattle.Turns.Units[0].Ct);
            Assert.IsType<TurnEndedEvent>(Assert.IsType<TurnBattleClockEvent>(Assert.IsType<AttackTurnBattleEvent>(result.Events[1]).Event).Event);
            Assert.IsType<AttackResolvedEvent>(result.Events[2]);
        }
        else
        {
            Assert.Equal(A, result.State.TurnBattle.Turns.ActiveUnitId);
            var move = AttackBattleSimulator.Move(result.State, new(A, new(2, 3, 0)));
            Assert.True(move.Accepted); Assert.Null(move.State.TurnBattle.Turns.ActiveUnitId);
            var wait = AttackBattleSimulator.Wait(result.State, A);
            Assert.True(wait.Accepted); Assert.Equal(35, wait.State.TurnBattle.Turns.Units[0].Ct);
        }
        Assert.Equal(new SwordKnockbackEvent(B, new(3, 2, 0), new(4, 2, 0)), result.Events[^1]);
        Assert.Empty(result.Events.OfType<AttackTurnBattleEvent>().Select(e => e.Event).OfType<TurnBattleMovementEvent>());
    }

    [Fact]
    public void Subsequent_target_turn_rechecks_attack_and_move_range_from_pushed_position()
    {
        var state = Attack(State()).State;
        state = AttackBattleSimulator.Wait(state, A).State;
        var ready = AttackBattleSimulator.Advance(state);
        Assert.True(ready.Accepted); state = ready.State;
        Assert.Equal(B, state.TurnBattle.Turns.ActiveUnitId);
        Atomic(state, AttackBattleSimulator.Attack(state, new(B, A), new(Weapon, 5), Profile), AttackCommandRejection.OutOfRange);
        // From pushed x4, x0 is distance4 and beyond Move3; from old x3 it would be legal.
        var tooFar = AttackBattleSimulator.Move(state, new(B, new(0, 2, 0)));
        Assert.False(tooFar.Accepted); Assert.Same(state, tooFar.State); Assert.Empty(tooFar.Events);
        var move = AttackBattleSimulator.Move(state, new(B, new(3, 2, 0)));
        Assert.True(move.Accepted);
        var attack = AttackBattleSimulator.Attack(move.State, new(B, A), new(Weapon, 0), Profile);
        Assert.True(attack.Accepted);
    }

    [Theory] [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void Both_actor_IDs_and_collection_orders_have_identical_pure_results(bool secondActor, bool reverse)
    {
        var state = State(secondActor: secondActor, reverse: reverse);
        var actor = secondActor ? B : A; var target = secondActor ? A : B;
        string before = Snapshot(state);
        var result = AttackBattleSimulator.Attack(state, new(actor, target), new(Weapon, 5), Profile);
        Assert.True(result.Accepted); Assert.Equal(new(4, 2, 0), Position(result.State, target));
        for (int i = 0; i < 4; i++)
        {
            var probe = AttackBattleSimulator.Attack(state, new(actor, target), new(Weapon, 5), new(Ground));
            Assert.Equal(Snapshot(result.State), Snapshot(probe.State)); Assert.Equal(result.Events.ToArray(), probe.Events.ToArray());
        }
        Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Command_replay_compares_every_accepted_and_rejected_prefix_and_suffix()
    {
        var start = State();
        Func<AttackBattleSnapshot, AttackBattleResult>[] journal =
        [s => Attack(s), s => Attack(s), s => AttackBattleSimulator.Wait(s, A), s => AttackBattleSimulator.Advance(s),
         s => AttackBattleSimulator.Attack(s, new(B, A), new(Weapon, 5), new(Ground)),
         s => AttackBattleSimulator.Move(s, new(B, new(3, 2, 0))),
         s => AttackBattleSimulator.Attack(s, new(B, A), new(Weapon, 0), new(Ground))];
        var state = start; var receipts = new List<AttackBattleResult>();
        foreach (var command in journal) { var result = command(state); receipts.Add(result); state = result.State; }
        Assert.Equal(new[] { true, false, true, true, false, true, true }, receipts.Select(r => r.Accepted));
        Assert.Equal(5, Unit(state, B).Hp); Assert.Equal(50, Unit(state, A).Hp);
        for (int prefix = 0; prefix <= journal.Length; prefix++)
        {
            var replay = State();
            foreach (var command in journal.Take(prefix)) replay = command(replay).State;
            for (int i = prefix; i < journal.Length; i++)
            {
                var result = journal[i](replay); var expected = receipts[i];
                Assert.Equal(expected.Accepted, result.Accepted); Assert.Equal(expected.AttackRejection, result.AttackRejection);
                Assert.Equal(expected.TurnRejection, result.TurnRejection); Assert.Equal(expected.MovementRejection, result.MovementRejection);
                Assert.Equal(Snapshot(expected.State), Snapshot(result.State)); Assert.Equal(expected.Events.ToArray(), result.Events.ToArray());
                replay = result.State;
            }
            Assert.Equal(Snapshot(state), Snapshot(replay));
        }
    }

    [Theory]
    [InlineData("blocked")] [InlineData("untargetable")] [InlineData("stop")] [InlineData("water")]
    [InlineData("height")] [InlineData("half")] [InlineData("ceiling")] [InlineData("surface")]
    [InlineData("slope")] [InlineData("layer")] [InlineData("overlap")] [InlineData("third")]
    [InlineData("fly")] [InlineData("traits")] [InlineData("body")] [InlineData("blocked-upper")]
    public void Unsupported_geometry_anywhere_rejects_before_rng_or_Act(string geometry)
    {
        var state = State(geometry: geometry, count: ulong.MaxValue);
        Atomic(state, Attack(state), AttackCommandRejection.UnsupportedSetup);
    }

    [Theory]
    [InlineData("actor", AttackCommandRejection.UnknownActor)] [InlineData("target", AttackCommandRejection.UnknownTarget)]
    [InlineData("inactive", AttackCommandRejection.InactiveActor)] [InlineData("acted", AttackCommandRejection.ActionAlreadyTaken)]
    [InlineData("self", AttackCommandRejection.SelfTarget)] [InlineData("dead", AttackCommandRejection.DeadTarget)]
    [InlineData("weapon", AttackCommandRejection.UnsupportedSetup)] [InlineData("missing-weapon", AttackCommandRejection.UnsupportedSetup)]
    [InlineData("null-weapon", AttackCommandRejection.UnsupportedSetup)] [InlineData("diagonal", AttackCommandRejection.OutOfRange)]
    [InlineData("distance", AttackCommandRejection.OutOfRange)]
    public void Invalid_commands_remain_atomic_and_do_not_reach_an_exhausted_rng(string kind, AttackCommandRejection expected)
    {
        var state = State(acted: kind == "acted", dead: kind == "dead", unarmed: kind == "missing-weapon",
            target: kind == "diagonal" ? new(3, 3, 0) : kind == "distance" ? new(4, 2, 0) : null, count: ulong.MaxValue);
        var command = new AttackUnitCommand(kind == "actor" ? new("sample:unit/missing") : kind == "inactive" ? B : A,
            kind == "target" ? new("sample:unit/missing") : kind == "self" ? A : kind == "inactive" ? A : B);
        BoundedSwordWeapon? weapon = kind == "null-weapon" ? null : new(kind == "weapon" ? new("sample:item/other") : Weapon, 5);
        Atomic(state, AttackBattleSimulator.Attack(state, command, weapon, Profile), expected);
    }

    [Fact]
    public void Event_sequences_distinguish_normal_critical_zero_damage_and_lethal_results()
    {
        var critical = Attack(State());
        Assert.Equal(new[] { typeof(AttackTurnBattleEvent), typeof(AttackResolvedEvent), typeof(AttackHpChangedEvent),
            typeof(AttackStatusChangedEvent), typeof(SwordKnockbackEvent) }, critical.Events.Select(e => e.GetType()));
        var zero = Attack(State(), 0);
        Assert.Equal(new[] { typeof(AttackTurnBattleEvent), typeof(AttackResolvedEvent), typeof(SwordKnockbackEvent) }, zero.Events.Select(e => e.GetType()));
        var lethal = Attack(State(hp: 45));
        Assert.Equal(new[] { typeof(AttackTurnBattleEvent), typeof(AttackResolvedEvent), typeof(AttackHpChangedEvent),
            typeof(AttackStatusChangedEvent), typeof(AttackKnockedOutEvent) }, lethal.Events.Select(e => e.GetType()));
        var normal = Attack(State(seed: 1));
        Assert.Equal(new[] { typeof(AttackTurnBattleEvent), typeof(AttackResolvedEvent), typeof(AttackHpChangedEvent) }, normal.Events.Select(e => e.GetType()));
    }

    [Fact]
    public void Offmap_profile_result_is_exactly_the_legacy_result_with_same_state_and_events()
    {
        foreach (uint seed in new[] { 1u, 57u, 233u })
        {
            var state = State(seed: seed, actor: new(3, 2, 0), target: new(4, 2, 0));
            var legacy = AttackBattleSimulator.Attack(state, new(A, B), new(Weapon, 5)); var flat = Attack(state);
            Assert.Equal(Snapshot(legacy.State), Snapshot(flat.State)); Assert.Equal(legacy.Events.ToArray(), flat.Events.ToArray());
        }
    }

    [Fact]
    public void Strict_profile_rejects_blocked_upper_layer_that_legacy_edge_geometry_admits()
    {
        var state = State(actor: new(3, 2, 0), target: new(4, 2, 0), geometry: "blocked-upper");
        Assert.True(AttackBattleSimulator.Attack(state, new(A, B), new(Weapon, 5)).Accepted);
        Atomic(state, Attack(state), AttackCommandRejection.UnsupportedSetup);
        var ordinary = State(count: ulong.MaxValue);
        Atomic(ordinary, AttackBattleSimulator.Attack(ordinary, new(A, B), new(Weapon, 5),
            new(new TerrainSurfaceId("sample:terrain/other-ground"))), AttackCommandRejection.UnsupportedSetup);
    }

    [Fact]
    public void Profile_and_programmer_null_errors_are_explicit()
    {
        Assert.Throws<ArgumentException>(() => new FlatSwordBattleProfile(default));
        Assert.Throws<ArgumentNullException>(() => AttackBattleSimulator.Attack(null!, new(A, B), new(Weapon, 5), Profile));
        Assert.Throws<ArgumentNullException>(() => AttackBattleSimulator.Attack(State(), null!, new(Weapon, 5), Profile));
        Assert.Throws<ArgumentNullException>(() => AttackBattleSimulator.Attack(State(), new(A, B), new(Weapon, 5), null!));
    }

    private static AttackBattleResult Attack(AttackBattleSnapshot state, int power = 5) =>
        AttackBattleSimulator.Attack(state, new(A, B), new(Weapon, power), Profile);
    private static AttackUnitState Unit(AttackBattleSnapshot state, BattleUnitId id) => state.Combatants.Single(u => u.UnitId == id);
    private static TilePosition Position(AttackBattleSnapshot state, BattleUnitId id) => state.TurnBattle.Battle.Units.Single(u => u.Id == id).Position;
    private static TilePosition Point(JsonElement point) => new(point[0].GetInt32(), point[1].GetInt32(), 0);
    private static BattleFacing Face(JsonElement value) => Enum.Parse<BattleFacing>(value.GetString()!);
    private static string Snapshot(AttackBattleSnapshot state) => JsonSerializer.Serialize(new
        { state.Combatants, state.TurnBattle.Turns, state.TurnBattle.Battle.Units, state.TurnBattle.Battle.RngState });
    private static void Atomic(AttackBattleSnapshot before, AttackBattleResult result, AttackCommandRejection expected)
    {
        Assert.False(result.Accepted); Assert.Equal(expected, result.AttackRejection); Assert.Same(before, result.State);
        Assert.Empty(result.Events); Assert.Equal(before.TurnBattle.Turns.RngState, before.TurnBattle.Battle.RngState);
    }

    private static AttackBattleSnapshot State(uint seed = 57, ulong count = 0, int pa = 5, int hp = 50, int ko = 0, int evade = 0,
        TilePosition? actor = null, TilePosition? target = null, int width = 5, int depth = 5,
        BattleFacing actorFacing = BattleFacing.North, BattleFacing targetFacing = BattleFacing.West,
        int actorBrave = 70, int targetBrave = 70, bool acted = false, bool dead = false, bool unarmed = false,
        bool targetMoved = false, bool targetActed = false, bool secondActor = false, bool reverse = false, string? geometry = null)
    {
        var actorId = secondActor ? B : A; var targetId = secondActor ? A : B;
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
        var bodies = new List<BattleUnitState> { new(actorId, actor ?? new(2, 2, 0), movement),
            new(targetId, geometry == "overlap" ? actor ?? new(2, 2, 0) : target ?? new(3, 2, 0), movement, targetMoved) };
        var turns = new List<TurnUnitState> { new(actorId, 10, 15, 0, true, false, acted),
            new(targetId, 12, 100, 1, false, targetMoved, targetActed) };
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
            new(reverse ? turns.AsEnumerable().Reverse() : turns, rng, 12, TurnCyclePhase.UnitTurn)),
            reverse ? combat.AsEnumerable().Reverse() : combat);
    }
}
