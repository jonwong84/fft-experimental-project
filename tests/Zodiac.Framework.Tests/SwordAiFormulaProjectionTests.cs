using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

/// <summary>Formula-only source-derived cases; no whole-AI or executable-reference equivalence claim.</summary>
[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiFormulaProjectionTests
{
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly FlatSwordBattleProfile Profile = new(Ground);

    public static IEnumerable<object[]> ContractVectors()
    {
        using var document = Contract();
        foreach (var row in document.RootElement.GetProperty("vectors").EnumerateArray()
            .Where(r => r.GetProperty("family").GetString() == "sword-formula-projection"))
            yield return [row.GetProperty("id").GetString()!];
    }

    [Theory] [MemberData(nameof(ContractVectors))]
    public void Executes_seven_unchanged_ZOD116_formula_vectors(string id)
    {
        using var document = Contract();
        var row = document.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        var input = row.GetProperty("inputs"); var expected = row.GetProperty("expected");
        var state = State(seed: input.GetProperty("seed").GetUInt32(), pa: input.GetProperty("pa").GetInt32(),
            hp: input.GetProperty("hp").GetInt32(), evade: input.GetProperty("evade").GetInt32(),
            targetFacing: input.GetProperty("front").GetBoolean() ? BattleFacing.West : BattleFacing.North);
        var result = Project(state, input.GetProperty("power").GetInt32());
        Assert.True(result.Accepted); Assert.Equal(AttackCommandRejection.None, result.Rejection);
        Assert.Equal(new(expected.GetProperty("hitPercent").GetInt32(), expected.GetProperty("damage").GetInt32(),
            expected.GetProperty("hp").GetInt32()), result.Outcome);
        Assert.Equal(new FftRandomState(expected.GetProperty("rngWord").GetUInt32(),
            expected.GetProperty("resolutionDraws").GetUInt64()), result.RngState);
        // Literal raw streams independently constrain the four BIOS transitions used by the query.
        var rng = state.TurnBattle.Turns.RngState;
        foreach (var raw in expected.GetProperty("raw").EnumerateArray())
        {
            var draw = new FftBiosRandom().Next(rng); Assert.Equal(raw.GetUInt32(), draw.Value); rng = draw.State;
        }
        Assert.Equal(rng, result.RngState);
    }

    public static IEnumerable<object[]> Facings()
    {
        foreach (var target in new[] { new TilePosition(3, 2, 0), new(1, 2, 0), new(2, 3, 0), new(2, 1, 0) })
        foreach (var actorFace in Enum.GetValues<BattleFacing>())
        foreach (var targetFace in Enum.GetValues<BattleFacing>())
        foreach (bool secondActor in new[] { false, true }) yield return [target, actorFace, targetFace, secondActor];
    }

    [Theory] [MemberData(nameof(Facings))]
    public void All_cardinal_facings_and_actor_IDs_use_target_front_only(TilePosition target, BattleFacing actorFace,
        BattleFacing targetFace, bool secondActor)
    {
        // Explicit table avoids reproducing the production directional expression.
        var fronts = new Dictionary<TilePosition, BattleFacing>
        { [new(3, 2, 0)] = BattleFacing.West, [new(1, 2, 0)] = BattleFacing.East,
          [new(2, 3, 0)] = BattleFacing.South, [new(2, 1, 0)] = BattleFacing.North };
        var state = State(target: target, actorFacing: actorFace, targetFacing: targetFace,
            secondActor: secondActor, reverse: secondActor, evade: 100);
        string original = Snapshot(state);
        var result = Project(state, secondActor: secondActor);
        Assert.Equal(targetFace == fronts[target] ? new(0, 0, 50) : new SwordAiFormulaOutcome(100, 25, 25), result.Outcome);
        Assert.Equal(new FftRandomState(3587663605, 4), result.RngState);
        Assert.Equal(original, Snapshot(state));
    }

    [Theory]
    [InlineData(0, 5, 50, 5, 45)] [InlineData(5, 0, 50, 0, 50)]
    [InlineData(5, 5, 24, 25, 0)] [InlineData(5, 5, 25, 25, 0)] [InlineData(5, 5, 26, 25, 1)]
    [InlineData(255, 64, 65535, 999, 64536)] [InlineData(1, 255, 1, 255, 0)]
    [InlineData(3, 255, 2000, 765, 1235)] [InlineData(4, 250, 2000, 999, 1001)]
    [InlineData(9, 111, 2000, 999, 1001)] [InlineData(0, 255, 50, 255, 0)]
    public void Damage_is_normal_capped_then_HP_clamped(int pa, int power, int hp, int damage, int finalHp)
    {
        var result = Project(State(pa: pa, hp: hp), power);
        Assert.Equal(new(90, damage, finalHp), result.Outcome);
        Assert.True(result.Outcome!.HasAccuracy);
    }

    [Theory] [InlineData(0, 100)] [InlineData(1, 99)] [InlineData(99, 1)] [InlineData(100, 0)]
    public void Partial_accuracy_never_weights_damage_or_selects_seeded_miss(int evade, int chance)
    {
        foreach (uint seed in new[] { 0u, 1u, 57u, 233u, uint.MaxValue })
        {
            var result = Project(State(seed: seed, evade: evade));
            Assert.Equal(new(chance, chance == 0 ? 0 : 25, chance == 0 ? 50 : 25), result.Outcome);
            Assert.Equal(chance > 0, result.Outcome!.HasAccuracy); Assert.Equal(4ul, result.RngState.DrawCount);
        }
    }

    public static IEnumerable<object[]> Budgets()
    {
        foreach (uint seed in new[] { 1u, 57u })
        foreach (int evade in new[] { 0, 10, 100 })
        for (int remaining = 0; remaining <= 4; remaining++) yield return [seed, evade, remaining];
    }

    [Theory] [MemberData(nameof(Budgets))]
    public void Every_draw_budget_is_atomic_even_for_certain_evasion(uint seed, int evade, int remaining)
    {
        var state = State(seed: seed, evade: evade, count: ulong.MaxValue - (ulong)remaining);
        string original = Snapshot(state); var result = Project(state);
        if (remaining < 4) Atomic(state, result, AttackCommandRejection.RandomDrawCountExhausted);
        else
        {
            Assert.True(result.Accepted); Assert.NotNull(result.Outcome);
            Assert.Equal(new FftRandomState(seed == 1 ? 3295386429u : 3587663605u, ulong.MaxValue), result.RngState);
        }
        Assert.Equal(original, Snapshot(state));
    }

    [Theory] [InlineData(255, 64, true)] [InlineData(255, 65, false)]
    [InlineData(65, 254, true)] [InlineData(65, 255, false)]
    [Trait("Coverage", "CurrentAdmission")]
    public void Signed_envelope_gate_precedes_draws_even_for_certain_evasion(int pa, int power, bool supported)
    {
        var state = State(pa: pa, evade: 100, count: ulong.MaxValue - 4); var result = Project(state, power);
        if (supported) { Assert.True(result.Accepted); Assert.Equal(new(0, 0, 50), result.Outcome); }
        else Atomic(state, result, AttackCommandRejection.UnsupportedSetup);
    }

    [Theory] [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void Repeated_probes_use_current_stats_and_preserve_both_collection_orders(bool secondActor, bool reverse)
    {
        var state = State(pa: 6, hp: 10, secondActor: secondActor, reverse: reverse);
        string original = Snapshot(state); var result = Project(state, secondActor: secondActor);
        Assert.Equal(new(90, 30, 0), result.Outcome);
        for (int i = 0; i < 4; i++) Assert.Equal(result, Project(state, secondActor: secondActor));
        Assert.Equal(original, Snapshot(state));
    }

    [Theory] [InlineData(3648949908u, 0u, 0u)] [InlineData(126717588u, 2147418112u, 32767u)]
    public void Fourth_evade_raw_endpoints_do_not_create_random_AI_misses(uint seed, uint finalWord, uint raw)
    {
        // Seeds obtained by four inverse BIOS LCG steps from literal final words 0 and 0x7fff0000.
        var rng = new FftRandomState(seed, 0);
        uint fourth = 0;
        for (int i = 0; i < 4; i++) { var draw = new FftBiosRandom().Next(rng); rng = draw.State; fourth = draw.Value; }
        Assert.Equal(raw, fourth);
        var result = Project(State(seed: seed));
        Assert.Equal(new FftRandomState(finalWord, 4), result.RngState);
        Assert.Equal(new(90, 25, 25), result.Outcome);
    }

    [Theory] [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    [Trait("Contract", "QueryIsolation")]
    public void Target_choice_flags_and_full_input_remain_unchanged(bool moved, bool acted)
    {
        var state = State(targetMoved: moved, targetActed: acted);
        string original = Snapshot(state);
        Assert.Equal(new(90, 25, 25), Project(state).Outcome);
        Assert.Equal(original, Snapshot(state));
    }

    [Fact]
    public void Next_turn_query_reads_PA_raised_by_Accumulate_in_this_battle()
    {
        var start = State(); var ability = new AssetId("sample:ability/accumulate");
        var profile = new ImmediateAbilityBattleProfile([new(ability)], [new(A, [ability], 0), new(B, [], 0)]);
        var applied = ImmediateAbilityBattleSimulator.Apply(start, new(A, ability), profile);
        Assert.True(applied.Accepted);
        Atomic(applied.State, Project(applied.State), AttackCommandRejection.ActionAlreadyTaken);
        var wait = AttackBattleSimulator.Wait(applied.State, A); Assert.True(wait.Accepted);
        var state = wait.State;
        for (int i = 0; i < 6 && state.TurnBattle.Turns.ActiveUnitId != A; i++)
        {
            var ready = AttackBattleSimulator.Advance(state); Assert.True(ready.Accepted); state = ready.State;
            if (state.TurnBattle.Turns.ActiveUnitId == B)
            { var skip = AttackBattleSimulator.Wait(state, B); Assert.True(skip.Accepted); state = skip.State; }
        }
        Assert.Equal(A, state.TurnBattle.Turns.ActiveUnitId);
        Assert.Equal(new(90, 25, 25), Project(start).Outcome);
        Assert.Equal(new(90, 30, 20), Project(state).Outcome);
        Assert.Equal(5, start.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
    }

    [Fact]
    public void Composing_returned_rng_preserves_progress_even_when_zero_accuracy_candidate_is_discarded()
    {
        var state = State(evade: 100); var first = Project(state);
        Assert.True(first.Accepted); Assert.False(first.Outcome!.HasAccuracy);
        // Test-only composition. A future authoritative decision runner must validate/commit both copies.
        var next = state.WithTurnBattle(new(new(state.TurnBattle.Battle.Map, state.TurnBattle.Battle.Units, first.RngState),
            new(state.TurnBattle.Turns.Units, first.RngState, state.TurnBattle.Turns.Ticks, state.TurnBattle.Turns.Phase)));
        var second = Project(next);
        Assert.Equal(new FftRandomState(361004401, 8), second.RngState);
        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(new FftRandomState(57, 0), state.TurnBattle.Turns.RngState);
    }

    [Fact]
    public void Move_then_query_does_not_end_turn_and_query_then_execution_keeps_original_critical()
    {
        var start = State(actor: new(1, 2, 0));
        var move = AttackBattleSimulator.Move(start, new(A, new(2, 2, 0)));
        Assert.True(move.Accepted); var state = move.State; string original = Snapshot(state);
        Assert.Equal(new(90, 25, 25), Project(state).Outcome);
        Assert.Equal(original, Snapshot(state)); Assert.Equal(A, state.TurnBattle.Turns.ActiveUnitId);
        var attack = AttackBattleSimulator.Attack(state, new(A, B), new(Weapon, 5), Profile);
        Assert.True(attack.Accepted); Assert.Null(attack.State.TurnBattle.Turns.ActiveUnitId);
        Assert.Equal(new AttackResolvedEvent(A, B, true, true, 45), Assert.Single(attack.Events.OfType<AttackResolvedEvent>()));
        Assert.Single(attack.Events.OfType<SwordKnockbackEvent>());
        Assert.Equal(new FftRandomState(3448561432, 7), attack.State.TurnBattle.Turns.RngState);
    }

    [Fact]
    public void Query_before_Act_then_Move_preserves_choices_and_nonzero_rng_count()
    {
        var state = State(seed: 1, count: 15);
        Assert.Equal(new FftRandomState(3295386429, 19), Project(state).RngState);
        var attack = AttackBattleSimulator.Attack(state, new(A, B), new(Weapon, 5), Profile);
        Assert.True(attack.Accepted); Assert.Equal(A, attack.State.TurnBattle.Turns.ActiveUnitId);
        var move = AttackBattleSimulator.Move(attack.State, new(A, new(2, 3, 0)));
        Assert.True(move.Accepted); Assert.Null(move.State.TurnBattle.Turns.ActiveUnitId);
        Assert.Equal(new FftRandomState(2516284547, 21), move.State.TurnBattle.Turns.RngState);
    }

    [Fact]
    public void Edge_and_interior_use_same_formula_without_any_push_destination_requirement()
    {
        Assert.Equal(Project(State()), Project(State(actor: new(3, 2, 0), target: new(4, 2, 0))));
    }

    [Theory]
    [InlineData("actor", AttackCommandRejection.UnknownActor)] [InlineData("target", AttackCommandRejection.UnknownTarget)]
    [InlineData("inactive", AttackCommandRejection.InactiveActor)] [InlineData("acted", AttackCommandRejection.ActionAlreadyTaken)]
    [InlineData("self", AttackCommandRejection.SelfTarget)] [InlineData("dead", AttackCommandRejection.DeadTarget)]
    [InlineData("weapon", AttackCommandRejection.UnsupportedSetup)] [InlineData("unarmed", AttackCommandRejection.UnsupportedSetup)]
    [InlineData("null-weapon", AttackCommandRejection.UnsupportedSetup)]
    [InlineData("diagonal", AttackCommandRejection.OutOfRange)] [InlineData("distance", AttackCommandRejection.OutOfRange)]
    public void Invalid_actions_reject_before_exhausted_rng(string kind, AttackCommandRejection reason)
    {
        var state = State(acted: kind == "acted", dead: kind == "dead", unarmed: kind == "unarmed",
            target: kind == "diagonal" ? new(3, 3, 0) : kind == "distance" ? new(4, 2, 0) : null, count: ulong.MaxValue);
        var missing = new BattleUnitId("sample:unit/missing");
        var command = new AttackUnitCommand(kind == "actor" ? missing : kind == "inactive" ? B : A,
            kind == "target" ? missing : kind == "self" ? A : kind == "inactive" ? A : B);
        BoundedSwordWeapon? weapon = kind == "null-weapon" ? null : new(kind == "weapon" ? new("sample:item/other") : Weapon, 5);
        Atomic(state, SwordAiFormulaProjection.Evaluate(state, command, weapon, Profile), reason);
    }

    [Theory]
    [InlineData("blocked")] [InlineData("untargetable")] [InlineData("stop")] [InlineData("water")]
    [InlineData("height")] [InlineData("half")] [InlineData("ceiling")] [InlineData("surface")]
    [InlineData("slope")] [InlineData("layer")] [InlineData("overlap")] [InlineData("third")]
    [InlineData("fly")] [InlineData("traits")] [InlineData("body")] [InlineData("blocked-upper")]
    [Trait("Coverage", "CurrentAdmission")]
    public void Entire_certified_geometry_is_checked_before_rng(string geometry)
    {
        var state = State(geometry: geometry, count: ulong.MaxValue);
        Atomic(state, Project(state), AttackCommandRejection.UnsupportedSetup);
    }

    [Fact]
    public void Profile_mismatch_and_programmer_null_errors_are_explicit()
    {
        var state = State(count: ulong.MaxValue);
        Atomic(state, SwordAiFormulaProjection.Evaluate(state, new(A, B), new(Weapon, 5),
            new(new TerrainSurfaceId("sample:terrain/other"))), AttackCommandRejection.UnsupportedSetup);
        Assert.Throws<ArgumentNullException>(() => SwordAiFormulaProjection.Evaluate(null!, new(A, B), new(Weapon, 5), Profile));
        Assert.Throws<ArgumentNullException>(() => SwordAiFormulaProjection.Evaluate(state, null!, new(Weapon, 5), Profile));
        Assert.Throws<ArgumentNullException>(() => SwordAiFormulaProjection.Evaluate(state, new(A, B), new(Weapon, 5), null!));
    }

    private static JsonDocument Contract() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "ai-decision-contract-v1.json")));
    private static SwordAiFormulaResult Project(AttackBattleSnapshot state, int power = 5, bool secondActor = false) =>
        SwordAiFormulaProjection.Evaluate(state, new(secondActor ? B : A, secondActor ? A : B), new(Weapon, power), Profile);
    private static string Snapshot(AttackBattleSnapshot state) => JsonSerializer.Serialize(new
        { state.Combatants, state.TurnBattle.Turns, state.TurnBattle.Battle.Units, state.TurnBattle.Battle.RngState });
    private static void Atomic(AttackBattleSnapshot state, SwordAiFormulaResult result, AttackCommandRejection reason)
    {
        Assert.False(result.Accepted); Assert.Equal(reason, result.Rejection); Assert.Null(result.Outcome);
        Assert.Equal(state.TurnBattle.Turns.RngState, result.RngState);
    }

    private static AttackBattleSnapshot State(uint seed = 57, ulong count = 0, int pa = 5, int hp = 50, int ko = 0, int evade = 10,
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
