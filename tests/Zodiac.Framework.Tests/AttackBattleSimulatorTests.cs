using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class AttackBattleSimulatorTests
{
    private static readonly BattleUnitId A = new("sample:unit/attacker"), B = new("sample:unit/defender");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");

    // Source-derived vectors at decomp 11464943bbdbd2a0af022d90cb7f10455cf911df.
    // Effective stats and the event/rejection API are synthetic inputs/contracts.
    [Theory]
    [InlineData(1u, BattleFacing.West, true, false, 25, 25, 6ul, 2516284547u, AttackUnitStatus.None)]
    [InlineData(19u, BattleFacing.West, false, false, 0, 50, 4ul, 2162199151u, AttackUnitStatus.None)]
    [InlineData(19u, BattleFacing.North, true, false, 25, 25, 6ul, 2071569413u, AttackUnitStatus.None)]
    [InlineData(19u, BattleFacing.East, true, false, 25, 25, 6ul, 2071569413u, AttackUnitStatus.None)]
    [InlineData(57u, BattleFacing.West, true, true, 45, 5, 7ul, 3448561432u, AttackUnitStatus.Critical)]
    public void Seeded_hit_miss_facing_and_critical_vectors(uint seed, BattleFacing facing, bool hit, bool critical,
        int damage, int hp, ulong draws, uint word, AttackUnitStatus status)
    {
        var before = State(seed: seed, facing: facing);
        var actual = Attack(before);
        Assert.True(actual.Accepted);
        Assert.Equal(new AttackResolvedEvent(A, B, hit, critical, damage), actual.Events.OfType<AttackResolvedEvent>().Single());
        var target = actual.State.Combatants[1];
        Assert.Equal(hp, target.Hp); Assert.Equal(status, target.Status);
        Assert.Equal(new FftRandomState(word, draws), actual.State.TurnBattle.Turns.RngState);
        Assert.Equal(actual.State.TurnBattle.Turns.RngState, actual.State.TurnBattle.Battle.RngState);
        Assert.True(actual.State.TurnBattle.Turns.Units[0].ActionTaken);
        Assert.True(actual.State.TurnBattle.Turns.Units[0].HasTurn);
        Assert.Equal(BattleFacing.South, actual.State.Combatants[0].Facing);
        Assert.Equal(BattleFacing.South, before.Combatants[0].Facing);
        Assert.Equal(50, before.Combatants[1].Hp);
        Assert.Equal(new FftRandomState(seed, 0), before.TurnBattle.Turns.RngState);
        Assert.Equal(actual.Events.ToArray(), Attack(before).Events.ToArray());
    }

    [Theory]
    [InlineData(24, 0, 0, AttackUnitStatus.Dead, 1)]
    [InlineData(25, 0, 255, AttackUnitStatus.Dead, 0)]
    [InlineData(26, 1, 0, AttackUnitStatus.Critical, 0)]
    [InlineData(31, 6, 0, AttackUnitStatus.Critical, 0)]
    [InlineData(32, 7, 0, AttackUnitStatus.None, 0)]
    public void Hp_clamp_Critical_integer_boundary_and_KO_byte_wrap(int maximum, int hp, int oldKo, AttackUnitStatus status, int ko)
    {
        var actual = Attack(State(maxHp: maximum, koCount: oldKo));
        Assert.Equal(hp, actual.State.Combatants[1].Hp);
        Assert.Equal(status, actual.State.Combatants[1].Status);
        Assert.Equal(ko, actual.State.Combatants[1].KoCount);
        Assert.Equal(status == AttackUnitStatus.Dead ? 1 : 0, actual.Events.OfType<AttackKnockedOutEvent>().Count());
        if (status == AttackUnitStatus.Dead)
        {
            Assert.IsType<AttackTurnBattleEvent>(actual.Events[0]);
            Assert.IsType<AttackResolvedEvent>(actual.Events[1]);
            Assert.IsType<AttackHpChangedEvent>(actual.Events[2]);
            Assert.IsType<AttackStatusChangedEvent>(actual.Events[3]);
            Assert.IsType<AttackKnockedOutEvent>(actual.Events[4]);
        }
    }

    [Theory]
    [InlineData(0, 0, 1u, 0, false, 6ul)]
    [InlineData(0, 255, 1u, 255, false, 6ul)]
    [InlineData(1, 5, 57u, 5, true, 7ul)]
    [InlineData(255, 64, 1u, 999, false, 6ul)]
    [InlineData(128, 128, 57u, 999, true, 7ul)]
    public void Zero_PA_zodiac_floor_zero_power_critical_zero_bonus_and_positive_damage_cap(
        int pa, int power, uint seed, int damage, bool critical, ulong draws)
    {
        var actual = Attack(State(seed: seed, pa: pa, maxHp: 65535), power);
        Assert.True(actual.Accepted);
        var resolved = actual.Events.OfType<AttackResolvedEvent>().Single();
        Assert.Equal(damage, resolved.Damage); Assert.Equal(critical, resolved.Critical);
        Assert.Equal(65535 - damage, actual.State.Combatants[1].Hp);
        Assert.Equal(draws, actual.State.TurnBattle.Turns.RngState.DrawCount);
        Assert.Equal(damage == 0 ? 0 : 1, actual.Events.OfType<AttackHpChangedEvent>().Count());
    }

    [Fact]
    public void A_capped_formula_damage_can_exceed_the_actual_HP_loss()
    {
        var actual = Attack(State(pa: 255), power: 64);
        Assert.Equal(999, actual.Events.OfType<AttackResolvedEvent>().Single().Damage);
        Assert.Equal(new AttackHpChangedEvent(B, 50, 0), actual.Events.OfType<AttackHpChangedEvent>().Single());
    }

    [Theory]
    [InlineData(2, 1, BattleFacing.West)]
    [InlineData(0, 1, BattleFacing.East)]
    [InlineData(1, 2, BattleFacing.South)]
    [InlineData(1, 0, BattleFacing.North)]
    public void Each_cardinal_direction_preserves_settled_actor_and_checks_targets_front(int x, int y, BattleFacing front)
    {
        var actual = Attack(State(seed: 19, facing: front, targetPosition: new(x, y, 0)));
        Assert.True(actual.Accepted);
        Assert.False(actual.Events.OfType<AttackResolvedEvent>().Single().Hit);
        Assert.Equal(BattleFacing.South, actual.State.Combatants[0].Facing);
        Assert.Equal(new FftRandomState(2162199151, 4), actual.State.TurnBattle.Turns.RngState);
    }

    [Fact]
    public void Certain_front_class_evade_still_draws_four_times_and_side_clears_it()
    {
        var miss = Attack(State(classEvade: 100));
        Assert.False(miss.Events.OfType<AttackResolvedEvent>().Single().Hit);
        Assert.Equal(4ul, miss.State.TurnBattle.Turns.RngState.DrawCount);
        var hit = Attack(State(classEvade: 100, facing: BattleFacing.North));
        Assert.True(hit.Events.OfType<AttackResolvedEvent>().Single().Hit);
        Assert.Equal(6ul, hit.State.TurnBattle.Turns.RngState.DrawCount);
    }

    [Theory]
    [InlineData(410u, false, 4ul)] // Fourth raw3276 scales to9: evade10 succeeds.
    [InlineData(3878u, true, 6ul)] // Fourth raw3277 scales to10: threshold equality fails evade.
    public void Evasion_threshold_equality_uses_source_multiply_divide_scaling(uint seed, bool hit, ulong draws)
    {
        var result = Attack(State(seed: seed));
        Assert.Equal(hit, result.Events.OfType<AttackResolvedEvent>().Single().Hit);
        Assert.Equal(draws, result.State.TurnBattle.Turns.RngState.DrawCount);
    }

    [Theory]
    [InlineData(22402u, true, 7ul)] // Fifth raw1310 scales to3: critical.
    [InlineData(27894u, false, 6ul)] // Fifth raw1311 scales to4: not critical.
    public void Critical_threshold_equality_does_not_crit(uint seed, bool critical, ulong draws)
    {
        var result = Attack(State(seed: seed, classEvade: 0));
        Assert.Equal(critical, result.Events.OfType<AttackResolvedEvent>().Single().Critical);
        Assert.Equal(draws, result.State.TurnBattle.Turns.RngState.DrawCount);
    }

    [Fact]
    public void Attack_then_Wait_refunds_unused_move_and_post_move_attack_ends_before_damage_events()
    {
        var hit = Attack(State(ct: 50));
        var wait = AttackBattleSimulator.Wait(hit.State, A);
        Assert.True(wait.Accepted);
        Assert.Equal(60, wait.State.TurnBattle.Turns.Units[0].Ct);
        Assert.False(wait.State.TurnBattle.Turns.Units[0].HasTurn);
        Assert.Equal(hit.State.TurnBattle.Turns.RngState, wait.State.TurnBattle.Turns.RngState);
        var both = Attack(State(moved: true));
        Assert.False(both.State.TurnBattle.Turns.Units[0].HasTurn);
        Assert.Equal(0, both.State.TurnBattle.Turns.Units[0].Ct);
        Assert.IsType<TurnEndedEvent>(Assert.IsType<TurnBattleClockEvent>(Assert.IsType<AttackTurnBattleEvent>(both.Events[1]).Event).Event);
        Assert.IsType<AttackResolvedEvent>(both.Events[2]);
    }

    [Fact]
    public void Critical_projection_can_Wait_and_advance_but_dead_scheduling_is_rejected()
    {
        var critical = Attack(State(seed: 57));
        var wait = AttackBattleSimulator.Wait(critical.State, A);
        var ready = AttackBattleSimulator.Advance(wait.State);
        Assert.True(ready.Accepted);
        Assert.Equal(B, ready.State.TurnBattle.Turns.ActiveUnitId);
        Assert.Equal(AttackUnitStatus.Critical, ready.State.Combatants[1].Status);
        var ko = Attack(State(maxHp: 25));
        var ended = AttackBattleSimulator.Wait(ko.State, A);
        Assert.True(ended.Accepted);
        AtomicReject(ended.State, AttackBattleSimulator.Advance(ended.State), AttackCommandRejection.UnsupportedSetup);
        AtomicReject(ko.State, AttackBattleSimulator.Move(ko.State, new(A, new(0, 1, 0))), AttackCommandRejection.UnsupportedSetup);
        AtomicReject(ko.State, AttackBattleSimulator.Attack(ko.State, new(A, B), new(Weapon, 5)), AttackCommandRejection.ActionAlreadyTaken);
    }

    [Theory]
    [InlineData("actor", AttackCommandRejection.UnknownActor)]
    [InlineData("target", AttackCommandRejection.UnknownTarget)]
    [InlineData("inactive", AttackCommandRejection.InactiveActor)]
    [InlineData("acted", AttackCommandRejection.ActionAlreadyTaken)]
    [InlineData("self", AttackCommandRejection.SelfTarget)]
    [InlineData("dead", AttackCommandRejection.DeadTarget)]
    [InlineData("diagonal", AttackCommandRejection.OutOfRange)]
    [InlineData("distance", AttackCommandRejection.OutOfRange)]
    [InlineData("knockback", AttackCommandRejection.UnsupportedSetup)]
    [InlineData("weapon", AttackCommandRejection.UnsupportedSetup)]
    [InlineData("unarmed", AttackCommandRejection.UnsupportedSetup)]
    [InlineData("overflow", AttackCommandRejection.UnsupportedSetup)]
    [InlineData("water", AttackCommandRejection.UnsupportedSetup)]
    public void Invalid_or_unsupported_commands_preserve_exact_input_and_emit_nothing(string kind, AttackCommandRejection expected)
    {
        var before = State(acted: kind == "acted", dead: kind == "dead", pa: kind == "overflow" ? 255 : 5,
            targetPosition: kind == "diagonal" ? new(2, 2, 0) : kind == "distance" ? new(1, 3, 0) : null,
            width: kind == "knockback" ? 4 : 3, depth: kind == "distance" ? 4 : 3,
            unarmed: kind == "unarmed", water: kind == "water");
        var command = new AttackUnitCommand(kind == "actor" ? new("sample:unit/missing") : kind == "inactive" ? B : A,
            kind == "target" ? new("sample:unit/missing") : kind == "self" ? A : kind == "inactive" ? A : B);
        BoundedSwordWeapon? weapon = kind == "weapon" ? new(new("sample:item/other"), 5) : new(Weapon, kind == "overflow" ? 65 : 5);
        AtomicReject(before, AttackBattleSimulator.Attack(before, command, weapon), expected);
    }

    [Fact]
    public void Draw_counter_exhaustion_is_atomic_and_uses_actual_branch_draw_count()
    {
        var before = State(count: ulong.MaxValue - 5);
        AtomicReject(before, Attack(before), AttackCommandRejection.RandomDrawCountExhausted);
        var miss = Attack(State(seed: 19, count: ulong.MaxValue - 4));
        Assert.True(miss.Accepted); Assert.Equal(ulong.MaxValue, miss.State.TurnBattle.Turns.RngState.DrawCount);
        var critical = State(seed: 57, count: ulong.MaxValue - 6);
        AtomicReject(critical, Attack(critical), AttackCommandRejection.RandomDrawCountExhausted);
    }

    private static void AtomicReject(AttackBattleSnapshot before, AttackBattleResult actual, AttackCommandRejection reason)
    {
        Assert.False(actual.Accepted); Assert.Equal(reason, actual.AttackRejection);
        Assert.Same(before, actual.State);
        Assert.Empty(actual.Events);
    }

    private static AttackBattleResult Attack(AttackBattleSnapshot state, int power = 5) => AttackBattleSimulator.Attack(state, new(A, B), new(Weapon, power));

    private static AttackBattleSnapshot State(uint seed = 1, BattleFacing facing = BattleFacing.West, int maxHp = 50,
        int koCount = 0, int pa = 5, int classEvade = 10, bool moved = false, int ct = 0, bool acted = false,
        bool dead = false, TilePosition? targetPosition = null, int width = 3, int depth = 3, bool unarmed = false, bool water = false, ulong count = 0)
    {
        var rng = new FftRandomState(seed, count);
        var tiles = Enumerable.Repeat(new BattleTile(10), width * depth).ToArray();
        if (water) tiles[0] = new BattleTile(new TerrainSurfaceId("sample:terrain/ground"), 10, 0, 1, default);
        var map = new BattleMapDefinition(width, depth, 1, tiles);
        var turns = new TurnCycleState([new(A, 10, ct, 0, true, moved, acted), new(B, 10, 100, 1)], rng, phase: TurnCyclePhase.UnitTurn);
        return new(new(new(map, [new(A, new(1, 1, 0), 1, moved), new(B, targetPosition ?? new(2, 1, 0), 1)], rng), turns),
            [new(A, 50, 50, 0, 0, pa, 10, 0, 0, 0, 70, 0, BattleFacing.South, AttackUnitStatus.None, 0, unarmed ? null : Weapon),
             new(B, dead ? 0 : maxHp, maxHp, 0, 0, 5, classEvade, 0, 0, 0, 70, 0, facing, dead ? AttackUnitStatus.Dead : AttackUnitStatus.None, koCount, null)]);
    }
}
