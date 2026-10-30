using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class FlatOrdinaryMovementTests
{
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ground");
    private static readonly AssetId Weapon = new("sample:item/sword");
    private static readonly FlatOrdinaryMovementProfile Profile = new(Ground, A, B);

    public static IEnumerable<object[]> SourceVectors()
    {
        using var contract = Contract();
        foreach (var row in contract.RootElement.GetProperty("vectors").EnumerateArray().Where(r => r.GetProperty("family").GetString() == "flat-route"))
            yield return [row.GetProperty("id").GetString()!];
    }

    [Theory] [MemberData(nameof(SourceVectors))]
    public void Source_routes_preserve_reachability_residuals_distance_rows_and_selected_paths(string id)
    {
        using var contract = Contract();
        var row = contract.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        var input = row.GetProperty("inputs"); var expected = row.GetProperty("expected");
        TilePosition Point(JsonElement p) => new(p[0].GetInt32(), p[1].GetInt32(), 0);
        var state = State(width: input.GetProperty("width").GetInt32(), depth: input.GetProperty("depth").GetInt32(),
            origin: Point(input.GetProperty("origin")), enemy: Point(input.GetProperty("enemy")),
            move: input.GetProperty("move").GetInt32(), jump: input.GetProperty("jump").GetInt32(),
            moved: input.GetProperty("moved").GetBoolean(), acted: input.TryGetProperty("acted", out var acted) && acted.GetBoolean());
        string before = Snapshot(state); var actor = A;
        var result = FlatOrdinaryMovement.Evaluate(state, actor, Profile);
        var outcome = Accepted(result);
        Assert.Equal(expected.GetProperty("effectiveMove").GetInt32(), outcome.EffectiveMove);
        Assert.Equal(expected.GetProperty("jumpHalf").GetInt32(), outcome.JumpHalf);
        Assert.Equal(expected.GetProperty("physicalDestinations").EnumerateArray().Select(Point), outcome.PhysicalDestinations);
        Assert.Equal(expected.GetProperty("aiOrigins").EnumerateArray().Select(Point), outcome.AiOrigins);
        Assert.Equal(expected.GetProperty("remainingRanges").EnumerateArray().Select(r =>
            new FlatMovementResidual(Point(r.GetProperty("tile")), r.GetProperty("remaining").GetByte())), outcome.RemainingRanges);
        var rows = expected.GetProperty("targetDistanceRows").EnumerateArray().ToArray();
        Assert.Equal(rows.Length, outcome.TargetDistanceRows.Length);
        for (int i = 0; i < rows.Length; i++)
        {
            Assert.Equal(rows[i].GetProperty("y").GetInt32(), outcome.TargetDistanceRows[i].Y);
            Assert.Equal(rows[i].GetProperty("values").EnumerateArray().Select(v => v.GetByte()), outcome.TargetDistanceRows[i].Distances);
        }
        var path = FlatOrdinaryMovement.Evaluate(state, actor, Profile, Point(input.GetProperty("destination")));
        if (expected.GetProperty("path").ValueKind == JsonValueKind.Null) Rejected(state, path, FlatMovementRejection.InvalidDestination);
        else
        {
            var selected = Accepted(path).Path!;
            Assert.Equal(expected.GetProperty("path").EnumerateArray().Select(Point), selected.Tiles);
            Assert.Equal(expected.GetProperty("packedSteps").EnumerateArray().Select(v => v.GetByte()), selected.PackedSteps);
            Assert.Equal(Enum.Parse<BattleFacing>(expected.GetProperty("executionFinalHeading").GetString()!), selected.FinalHeading);
        }
        var body = state.TurnBattle.Battle.Units.Single(u => u.Id == actor);
        var enemyTile = state.TurnBattle.Battle.Units.Single(u => u.Id != actor).Position;
        var prototype = FlatGridMovementRange.Calculate(state.TurnBattle.Battle.Map, body.Position, body.Move).Where(p => p != enemyTile).ToHashSet();
        prototype.ExceptWith(outcome.PhysicalDestinations);
        Assert.True(prototype.SetEquals(expected.GetProperty("defaultPrototypeExtraDestinations").EnumerateArray().Select(Point)));
        Assert.Equal(before, Snapshot(state)); Assert.Equal(state.TurnBattle.Turns.RngState, result.RngState);
    }

    [Theory] [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void Actor_identity_and_collection_order_preserve_moved_and_unmoved_queries(bool second, bool moved)
    {
        var state = State(second: second, moved: moved);
        var reversed = State(second: second, moved: moved, reverse: true);
        string before = Snapshot(state), reversedBefore = Snapshot(reversed);
        var actor = second ? B : A;
        var result = FlatOrdinaryMovement.Evaluate(state, actor, Profile);
        var outcome = Accepted(result);
        Assert.Contains(new TilePosition(2, 2, 0), outcome.AiOrigins);
        Assert.DoesNotContain(new TilePosition(4, 4, 0), outcome.AiOrigins);
        Assert.Contains(new TilePosition(3, 2, 0), outcome.PhysicalDestinations);
        if (moved) Assert.Equal(new TilePosition(2, 2, 0), Assert.Single(outcome.AiOrigins));
        else Assert.Contains(new TilePosition(3, 2, 0), outcome.AiOrigins);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(FlatOrdinaryMovement.Evaluate(reversed, actor, Profile)));
        var occupied = new TilePosition(4, 4, 0);
        Rejected(state, FlatOrdinaryMovement.Evaluate(state, actor, Profile, occupied), FlatMovementRejection.InvalidDestination);
        Rejected(reversed, FlatOrdinaryMovement.Evaluate(reversed, actor, Profile, occupied), FlatMovementRejection.InvalidDestination);
        Assert.Equal(before, Snapshot(state)); Assert.Equal(reversedBefore, Snapshot(reversed));
    }

    [Fact]
    public void Move_caps_and_maximum_map_have_literal_residuals_and_uncompressed_steps()
    {
        var max = Accepted(FlatOrdinaryMovement.Evaluate(State(width: 16, depth: 16, origin: new(0, 0, 0), enemy: new(15, 15, 0), move: 255, jump: 255), A, Profile, new(15, 14, 0)));
        Assert.Equal(124, max.EffectiveMove); Assert.Equal(3, max.JumpHalf); Assert.Equal(254, max.PhysicalDestinations.Length);
        Assert.Equal(125, max.RemainingRanges.Single(r => r.Tile == new TilePosition(0, 0, 0)).Remaining);
        Assert.Equal(96, max.RemainingRanges.Single(r => r.Tile == new TilePosition(15, 14, 0)).Remaining);
        Assert.Equal(Enumerable.Repeat((byte)192, 14).Concat(Enumerable.Repeat((byte)0, 15)), max.Path!.PackedSteps);
        Assert.Equal(BattleFacing.East, max.Path.FinalHeading);
        var zero = Accepted(FlatOrdinaryMovement.Evaluate(State(move: 0), A, Profile));
        Assert.Empty(zero.PhysicalDestinations); Assert.Equal(new[] { new TilePosition(2, 2, 0) }, zero.AiOrigins);
        Assert.Equal(new FlatMovementResidual(new(2, 2, 0), 1), Assert.Single(zero.RemainingRanges));
    }

    [Fact]
    public void Enemy_blocks_detours_and_both_existing_cost_prototypes_match_membership()
    {
        foreach (int budget in new[] { 0, 1, 2, 3, 4, 5, 124 })
        {
            var state = State(width: 3, depth: 3, origin: new(0, 1, 0), enemy: new(1, 1, 0), move: budget);
            var battle = state.TurnBattle.Battle; var actor = battle.Units.Single(u => u.Id == A);
            var range = Accepted(FlatOrdinaryMovement.Evaluate(state, A, Profile));
            var blocked = new HashSet<TilePosition> { new(1, 1, 0) };
            Assert.True(TerrainAwareMovementRange.Calculate(battle.Map, actor.Position, actor.MovementProfile, blocked).SetEquals(range.PhysicalDestinations));
            Assert.True(OrdinaryAdjacentStepRange.Calculate(battle.Map, actor.Position, actor.MovementProfile, blocked).SetEquals(range.PhysicalDestinations));
        }
        var shortState = State(width: 3, depth: 3, origin: new(0, 1, 0), enemy: new(1, 1, 0), move: 2);
        Rejected(shortState, FlatOrdinaryMovement.Evaluate(shortState, A, Profile, new(2, 1, 0)), FlatMovementRejection.InvalidDestination);
        Assert.True(BattleSimulator.Apply(shortState.TurnBattle.Battle, new MoveUnitCommand(A, new(2, 1, 0))).Accepted);
        var detour = Accepted(FlatOrdinaryMovement.Evaluate(State(width: 3, depth: 3, origin: new(0, 1, 0), enemy: new(1, 1, 0), move: 4), A, Profile, new(2, 1, 0)));
        Assert.Equal(new byte[] { 192, 0, 0, 128 }, detour.Path!.PackedSteps);
        Assert.Equal(BattleFacing.South, detour.Path.FinalHeading);
    }

    [Theory] [InlineData(5, 1)] [InlineData(1, 5)]
    public void One_panel_corridor_is_disconnected_even_with_maximum_jump(int width, int depth)
    {
        var state = State(width: width, depth: depth, origin: new(0, 0, 0),
            enemy: width == 1 ? new(0, 1, 0) : new(1, 0, 0), move: 255, jump: 255);
        Assert.Empty(Accepted(FlatOrdinaryMovement.Evaluate(state, A, Profile)).PhysicalDestinations);
        Rejected(state, FlatOrdinaryMovement.Evaluate(state, A, Profile, width == 1 ? new(0, 2, 0) : new(2, 0, 0)), FlatMovementRejection.InvalidDestination);
    }

    [Fact]
    public void Reverse_distance_ignores_occupants_and_returns_every_x_in_selected_rows()
    {
        var range = Accepted(FlatOrdinaryMovement.Evaluate(State(width: 3, depth: 3, origin: new(0, 1, 0), enemy: new(1, 1, 0), move: 2), A, Profile));
        Assert.Equal(new byte[] { 2, 1, 2 }, range.TargetDistanceRows[0].Distances);
        Assert.Equal(new byte[] { 1, 0, 1 }, range.TargetDistanceRows[1].Distances);
        Assert.Equal(new byte[] { 2, 1, 2 }, range.TargetDistanceRows[2].Distances);
        Assert.DoesNotContain(new TilePosition(1, 1, 0), range.AiOrigins);
        var moved = Accepted(FlatOrdinaryMovement.Evaluate(State(moved: true), A, Profile));
        Assert.Equal(2, Assert.Single(moved.TargetDistanceRows).Y);
        Assert.Equal(new byte[] { 6, 5, 4, 3, 2 }, moved.TargetDistanceRows[0].Distances);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(7)] [InlineData(8)] [InlineData(255)]
    public void Jump_changes_allowance_but_not_flat_enemy_traversal_or_adjacent_path(int jump)
    {
        var range = Accepted(FlatOrdinaryMovement.Evaluate(State(width: 3, depth: 3, origin: new(0, 1, 0), enemy: new(1, 1, 0), move: 4, jump: jump), A, Profile, new(2, 1, 0)));
        Assert.Equal(Math.Min(7, jump) / 2, range.JumpHalf);
        Assert.Equal(new byte[] { 192, 0, 0, 128 }, range.Path!.PackedSteps);
    }

    [Theory] [InlineData(3, 3)] [InlineData(4, 2)] [InlineData(1, 4)]
    public void Reach_and_paths_match_independent_all_pairs_distances_and_exhaustive_path_ranking(int width, int depth)
    {
        var tiles = (from y in Enumerable.Range(0, depth) from x in Enumerable.Range(0, width) select new TilePosition(x, y, 0)).ToArray();
        foreach (var enemy in tiles)
        {
            var nodes = tiles.Where(p => p != enemy).ToArray();
            var costs = (from p in nodes from q in nodes select (p, q)).ToDictionary(t => t,
                t => t.p == t.q ? 0 : Manhattan(t.p, t.q) == 1 ? 1 : 1000);
            foreach (var k in nodes) foreach (var p in nodes) foreach (var q in nodes)
                costs[(p, q)] = Math.Min(costs[(p, q)], costs[(p, k)] + costs[(k, q)]);
            foreach (var origin in nodes)
            {
                for (int budget = 0; budget <= 8; budget++)
                {
                    var range = Accepted(FlatOrdinaryMovement.Evaluate(State(width: width, depth: depth, origin: origin, enemy: enemy, move: budget), A, Profile));
                    Assert.Equal(nodes.Where(p => p != origin && costs[(origin, p)] <= budget), range.PhysicalDestinations);
                }
                foreach (var target in nodes.Where(p => p != origin && costs[(origin, p)] < 1000))
                {
                    var paths = new List<TilePosition[]>(); var path = new List<TilePosition> { origin };
                    void Enumerate(TilePosition at)
                    {
                        if (at == target) { paths.Add(path.ToArray()); return; }
                        foreach (var next in nodes.Where(p => Manhattan(at, p) == 1 && !path.Contains(p)))
                        {
                            if (path.Count + costs[(next, target)] != costs[(origin, target)]) continue;
                            path.Add(next); Enumerate(next); path.RemoveAt(path.Count - 1);
                        }
                    }
                    Enumerate(origin);
                    // Rank all shortest complete routes by their backward choice sequence, independently of BFS.
                    string Key(TilePosition[] route)
                    {
                        int previous = 0; var key = new List<char>();
                        for (int i = route.Length - 1; i > 0; i--)
                        {
                            int dx = route[i - 1].X - route[i].X, dy = route[i - 1].Y - route[i].Y;
                            int direction = dx == 1 ? 0 : dy == 1 ? 1 : dx == -1 ? 2 : 3;
                            key.Add((char)('0' + (direction == previous ? 0 : direction + 1))); previous = direction;
                        }
                        return new string(key.ToArray());
                    }
                    var expected = paths.OrderBy(Key, StringComparer.Ordinal).First();
                    var actual = Accepted(FlatOrdinaryMovement.Evaluate(State(width: width, depth: depth, origin: origin, enemy: enemy, move: 124), A, Profile, target)).Path!;
                    Assert.Equal(expected, actual.Tiles);
                    Assert.Equal(costs[(origin, target)], actual.PackedSteps.Length);
                }
            }
        }
    }

    [Theory] [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void Both_choice_orders_filter_only_AI_origins_and_do_not_authorize_or_consume_commands(bool moved, bool acted)
    {
        var state = State(moved: moved, acted: acted);
        var range = Accepted(FlatOrdinaryMovement.Evaluate(state, A, Profile, new(3, 2, 0)));
        Assert.Equal(moved ? 1 : range.PhysicalDestinations.Length + 1, range.AiOrigins.Length);
        Assert.Equal(new byte[] { 0 }, range.Path!.PackedSteps);
        Assert.Equal(moved, state.TurnBattle.Turns.Units.Single(u => u.Id == A).MovementTaken);
        Assert.Equal(acted, state.TurnBattle.Turns.Units.Single(u => u.Id == A).ActionTaken);
    }

    [Theory] [InlineData(TurnCyclePhase.AddCt)] [InlineData(TurnCyclePhase.PickUnit)] [InlineData(TurnCyclePhase.UnitTurn)]
    public void Queries_both_living_actors_in_any_phase_at_exhausted_RNG_without_facing_or_clock_mutation(TurnCyclePhase phase)
    {
        var state = State(phase: phase, count: ulong.MaxValue, hp: 5);
        string before = Snapshot(state);
        foreach (var actor in new[] { A, B })
        {
            var first = FlatOrdinaryMovement.Evaluate(state, actor, Profile);
            Assert.True(first.Accepted); Assert.Equal(state.TurnBattle.Turns.RngState, first.RngState);
            for (int i = 0; i < 3; i++) Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(FlatOrdinaryMovement.Evaluate(state, actor, Profile)));
        }
        Assert.Equal(before, Snapshot(state));
        var path = Accepted(FlatOrdinaryMovement.Evaluate(state, A, Profile, new(3, 2, 0))).Path!;
        Assert.Equal(BattleFacing.East, path.FinalHeading);
        Assert.Equal(BattleFacing.West, state.Combatants.Single(u => u.UnitId == A).Facing);
    }

    [Theory] [InlineData(-1, 0, 0)] [InlineData(5, 0, 0)] [InlineData(0, 5, 0)] [InlineData(0, 0, 1)]
    [InlineData(2, 2, 0)] [InlineData(4, 4, 0)]
    public void Invalid_path_requests_return_no_partial_range_and_preserve_RNG(int x, int y, int layer)
    {
        var state = State(count: ulong.MaxValue);
        Rejected(state, FlatOrdinaryMovement.Evaluate(state, A, Profile, new(x, y, layer)), FlatMovementRejection.InvalidDestination);
    }

    [Theory]
    [InlineData("blocked")] [InlineData("untargetable")] [InlineData("stop")] [InlineData("water")]
    [InlineData("height")] [InlineData("half")] [InlineData("ceiling")] [InlineData("slope")]
    [InlineData("surface")] [InlineData("upper")] [InlineData("overlap")] [InlineData("third")]
    [InlineData("move256")] [InlineData("jump256")] [InlineData("enemy-move256")]
    [InlineData("fly")] [InlineData("body")] [InlineData("traits")]
    [InlineData("cost-missing")] [InlineData("cost-two")] [InlineData("cost-blocked")] [InlineData("enemy-cost-missing")]
    [InlineData("dead-actor")] [InlineData("dead-enemy")]
    public void Unsupported_profiles_fail_atomically_for_the_entire_state(string geometry)
    {
        var state = State(geometry: geometry, count: ulong.MaxValue);
        Rejected(state, FlatOrdinaryMovement.Evaluate(state, A, Profile), FlatMovementRejection.UnsupportedProfile);
    }

    [Theory] [InlineData(17, 5)] [InlineData(5, 17)]
    public void Compatibility_dimensions_do_not_change_general_map_limits(int width, int depth)
    {
        var state = State(width: width, depth: depth);
        Rejected(state, FlatOrdinaryMovement.Evaluate(state, A, Profile), FlatMovementRejection.UnsupportedProfile);
        Assert.NotEmpty(FlatGridMovementRange.Calculate(state.TurnBattle.Battle.Map, new(2, 2, 0), 1));
    }

    [Fact]
    public void Explicit_certification_IDs_and_programmer_errors_are_validated()
    {
        var state = State();
        Rejected(state, FlatOrdinaryMovement.Evaluate(state, new("sample:unit/unknown"), Profile), FlatMovementRejection.UnknownActor);
        Rejected(state, FlatOrdinaryMovement.Evaluate(state, A, new(Ground, A, new("sample:unit/unknown"))), FlatMovementRejection.UnsupportedProfile);
        Rejected(state, FlatOrdinaryMovement.Evaluate(state, A, new(new("sample:terrain/other"), A, B)), FlatMovementRejection.UnsupportedProfile);
        Assert.Throws<ArgumentException>(() => new FlatOrdinaryMovementProfile(Ground, A, A));
        Assert.Throws<ArgumentException>(() => new FlatOrdinaryMovementProfile(default, A, B));
        Assert.Throws<ArgumentException>(() => new FlatOrdinaryMovementProfile(Ground, default, B));
        Assert.Throws<ArgumentNullException>(() => FlatOrdinaryMovement.Evaluate(null!, A, Profile));
        Assert.Throws<ArgumentNullException>(() => FlatOrdinaryMovement.Evaluate(state, A, null!));
        Assert.Equal(JsonSerializer.Serialize(FlatOrdinaryMovement.Evaluate(state, A, Profile)),
            JsonSerializer.Serialize(FlatOrdinaryMovement.Evaluate(state, A, new(Ground, B, A))));
    }

    [Fact]
    public void Real_push_rebuilds_from_settled_coordinates_without_stale_occupancy()
    {
        var start = State(origin: new(2, 2, 0), enemy: new(3, 2, 0), seed: 57, phase: TurnCyclePhase.UnitTurn);
        var before = Accepted(FlatOrdinaryMovement.Evaluate(start, A, Profile));
        Assert.DoesNotContain(new TilePosition(3, 2, 0), before.PhysicalDestinations);
        var attack = AttackBattleSimulator.Attack(start, new(A, B), new(Weapon, 5), new FlatSwordBattleProfile(Ground));
        Assert.True(attack.Accepted); Assert.Contains(attack.Events, e => e is SwordKnockbackEvent);
        Assert.Equal(new TilePosition(4, 2, 0), attack.State.TurnBattle.Battle.Units.Single(u => u.Id == B).Position);
        var after = Accepted(FlatOrdinaryMovement.Evaluate(attack.State, A, Profile, new(3, 2, 0)));
        Assert.Equal(new byte[] { 0 }, after.Path!.PackedSteps);
        Assert.DoesNotContain(new TilePosition(4, 2, 0), after.PhysicalDestinations);
        var forEnemy = Accepted(FlatOrdinaryMovement.Evaluate(attack.State, B, Profile, new(3, 2, 0)));
        Assert.Equal(new byte[] { 64 }, forEnemy.Path!.PackedSteps);
        var killed = AttackBattleSimulator.Attack(start, new(A, B), new(Weapon, 64), new FlatSwordBattleProfile(Ground));
        Assert.True(killed.Accepted);
        Rejected(killed.State, FlatOrdinaryMovement.Evaluate(killed.State, A, Profile), FlatMovementRejection.UnsupportedProfile);
    }

    [Fact]
    public void Legacy_facing_wrapper_does_not_activate_query_or_apply_its_heading()
    {
        var start = State(ticks: 0); var session = FacingTurnBattle.Begin(start, new FlatSwordBattleProfile(Ground));
        var active = FacingTurnBattle.Advance(session); Assert.True(active.Accepted);
        var query = Accepted(FlatOrdinaryMovement.Evaluate(active.State.Battle, A, Profile, new(3, 2, 0)));
        var moved = FacingTurnBattle.Move(active.State, new(A, new(3, 2, 0))); Assert.True(moved.Accepted);
        Assert.Equal(BattleFacing.East, query.Path!.FinalHeading);
        Assert.Equal(BattleFacing.West, moved.State.Battle.Combatants.Single(u => u.UnitId == A).Facing);
        Assert.Equal(active.State.Revision + 1, moved.State.Revision);
        Assert.Null(moved.State.PendingFacing);
    }

    private static JsonDocument Contract() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "flat-ai-movement-contract-v1.json")));
    private static int Manhattan(TilePosition a, TilePosition b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    private static FlatMovementOutcome Accepted(FlatMovementQueryResult result)
    { Assert.True(result.Accepted, result.Rejection.ToString()); Assert.Equal(FlatMovementRejection.None, result.Rejection); return Assert.IsType<FlatMovementOutcome>(result.Outcome); }
    private static void Rejected(AttackBattleSnapshot state, FlatMovementQueryResult result, FlatMovementRejection reason)
    { Assert.False(result.Accepted); Assert.Equal(reason, result.Rejection); Assert.Null(result.Outcome); Assert.Equal(state.TurnBattle.Turns.RngState, result.RngState); }
    private static string Snapshot(AttackBattleSnapshot state) => JsonSerializer.Serialize(new
    {
        Units = state.TurnBattle.Battle.Units.Select(u => new
        {
            u.Id, u.Position, u.HasMoved, u.MovementProfile.Move, u.MovementProfile.Jump,
            u.MovementProfile.BodyHeight, u.MovementProfile.Mode, u.MovementProfile.Traits,
            Costs = u.MovementProfile.TerrainCosts.OrderBy(p => p.Key.Value, StringComparer.Ordinal)
                .Select(p => new { Surface = p.Key.Value, p.Value })
        }),
        state.Combatants, state.TurnBattle.Turns
    });

    private static AttackBattleSnapshot State(int width = 5, int depth = 5, TilePosition? origin = null, TilePosition? enemy = null,
        int move = 4, int jump = 0, bool moved = false, bool acted = false, bool second = false, bool reverse = false,
        string? geometry = null, uint seed = 57, ulong count = 0, int hp = 50, TurnCyclePhase phase = TurnCyclePhase.AddCt, ulong ticks = 42)
    {
        var actorId = second ? B : A; var enemyId = second ? A : B;
        int layers = geometry == "upper" ? 2 : 1;
        var tiles = Enumerable.Repeat(new BattleTile(Ground, 10), width * depth * layers).ToArray();
        tiles[^1] = geometry switch
        {
            "blocked" => new(Ground, 10, movementFlags: BattleTileMovementFlags.Blocked),
            "untargetable" => new(Ground, 10, movementFlags: BattleTileMovementFlags.Untargetable),
            "stop" => new(Ground, 10, movementFlags: BattleTileMovementFlags.CannotStop),
            "water" => new(Ground, 10, waterDepth: 1), "height" => new(Ground, 11),
            "half" => new(Ground, 10, halfHeight: 1), "ceiling" => new(Ground, 10, ceilingDepth: 1),
            "slope" => new(Ground, 10, slope: TileSlope.FromPackedByte(1)),
            "surface" => new(new("sample:terrain/other"), 10), _ => tiles[^1]
        };
        UnitMovementProfile Movement(bool other) => new(geometry == "move256" || other && geometry == "enemy-move256" ? 256 : move,
            geometry == "jump256" ? 256 : jump, geometry == "body" ? 7 : 6,
            geometry == "fly" ? UnitMovementMode.Fly : UnitMovementMode.Normal,
            geometry == "traits" ? UnitMovementTraits.AnyGround : UnitMovementTraits.None,
            geometry == "cost-missing" || other && geometry == "enemy-cost-missing" ? [] :
            new[] { KeyValuePair.Create(Ground, geometry == "cost-blocked" ? TerrainMovementCost.Blocked : new TerrainMovementCost(geometry == "cost-two" ? 2 : 1)) });
        var bodies = new List<BattleUnitState> { new(actorId, origin ?? new(2, 2, 0), Movement(false), moved),
            new(enemyId, geometry == "overlap" ? origin ?? new(2, 2, 0) : enemy ?? new(4, 4, 0), Movement(true)) };
        var turns = new List<TurnUnitState> { new(actorId, 10, phase == TurnCyclePhase.UnitTurn ? 15 : 105, 0,
                phase == TurnCyclePhase.UnitTurn, moved, acted), new(enemyId, 12, 0, 1) };
        AttackUnitState Combat(BattleUnitId id, int health, BattleFacing facing) => new(id, health, 50, 0, 0, 5, 0, 0, 0, 0, 70, 0,
            facing, health == 0 ? AttackUnitStatus.Dead : health <= 10 ? AttackUnitStatus.Critical : AttackUnitStatus.None, 0, Weapon);
        var combat = new List<AttackUnitState> { Combat(actorId, geometry == "dead-actor" ? 0 : hp, BattleFacing.West),
            Combat(enemyId, geometry == "dead-enemy" ? 0 : 50, BattleFacing.North) };
        if (geometry == "third")
        {
            var c = new BattleUnitId("sample:unit/c"); bodies.Add(new(c, new(0, 0, 0), Movement(false)));
            turns.Add(new(c, 10, 0, 2)); combat.Add(Combat(c, 50, BattleFacing.East));
        }
        var rng = new FftRandomState(seed, count);
        return new(new(new(new(width, depth, layers, tiles), reverse ? bodies.AsEnumerable().Reverse() : bodies, rng),
            new(reverse ? turns.AsEnumerable().Reverse() : turns, rng, ticks, phase)), reverse ? combat.AsEnumerable().Reverse() : combat);
    }
}
