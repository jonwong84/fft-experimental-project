using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class ThrowStoneBattleTests
{
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly AssetId Stone = new("sample:ability/throw-stone");
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");

    // Source-derived arithmetic at decomp 11464943bbdbd2a0af022d90cb7f10455cf911df.
    [Theory]
    [InlineData(1, 0u, 1)] [InlineData(1, 16383u, 1)] [InlineData(1, 16384u, 2)] [InlineData(1, 32767u, 2)]
    [InlineData(5, 0u, 5)] [InlineData(5, 16383u, 5)] [InlineData(5, 16384u, 10)] [InlineData(5, 32767u, 10)]
    [InlineData(6, 0u, 6)] [InlineData(6, 16383u, 6)] [InlineData(6, 16384u, 12)] [InlineData(6, 32767u, 12)]
    [InlineData(99, 0u, 99)] [InlineData(99, 16383u, 99)] [InlineData(99, 16384u, 198)] [InlineData(99, 32767u, 198)]
    public void Damage_boundary_vectors(int pa, uint raw, int expected) => Assert.Equal(expected, ThrowStoneFormula.Damage(pa, raw));

    [Theory]
    [InlineData(70, 70, 0u, 0u, false)] [InlineData(70, 70, 32767u, 32767u, false)]
    [InlineData(70, 70, 32767u, 0u, true)] [InlineData(70, 70, 0u, 32767u, false)]
    [InlineData(100, 10, 32767u, 32767u, true)] [InlineData(10, 100, 32767u, 32767u, false)]
    public void Independent_scaled_Brave_rolls_use_strict_comparison(int a, int b, uint ra, uint rb, bool expected) =>
        Assert.Equal(expected, ThrowStoneFormula.AttemptsKnockback(a, b, ra, rb));

    [Theory]
    [InlineData(0, 0u)] [InlineData(100, 0u)] [InlineData(5, 32768u)] [InlineData(5, uint.MaxValue)]
    public void Arithmetic_rejects_outside_closed_domain(int pa, uint raw) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ThrowStoneFormula.Damage(pa, raw));

    [Theory] [InlineData(9, 70, 0u, 0u)] [InlineData(70, 101, 0u, 0u)]
    [InlineData(70, 70, 32768u, 0u)] [InlineData(70, 70, 0u, uint.MaxValue)]
    public void Brave_arithmetic_checks_both_stats_and_raw_values(int a, int b, uint ra, uint rb) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ThrowStoneFormula.AttemptsKnockback(a, b, ra, rb));

    [Theory]
    [InlineData(1u, 0, BattleFacing.West, true, 10, false, 3655513600u, 7ul)]
    [InlineData(3u, 0, BattleFacing.West, true, 10, true, 273505290u, 7ul)]
    [InlineData(19u, 10, BattleFacing.West, false, 0, false, 2162199151u, 4ul)]
    [InlineData(19u, 10, BattleFacing.North, true, 5, true, 3282209882u, 7ul)]
    [InlineData(57u, 0, BattleFacing.West, true, 5, true, 3448561432u, 7ul)]
    [InlineData(410u, 10, BattleFacing.West, false, 0, false, 214736230u, 4ul)]
    [InlineData(3878u, 10, BattleFacing.West, true, 10, false, 1752498361u, 7ul)]
    public void Seeded_source_vectors_include_exact_damage_push_and_rng(uint seed, int evade, BattleFacing facing,
        bool hit, int damage, bool push, uint word, ulong draws)
    {
        var before = State(seed: seed, evade: evade, facing: facing);
        string original = Snapshot(before);
        var result = Apply(before);
        Assert.True(result.Accepted);
        Assert.Equal(new ThrowStoneResolvedEvent(A, Stone, B, hit, damage, push), result.Events[1]);
        Assert.Equal(new FftRandomState(word, draws), result.State.TurnBattle.Turns.RngState);
        Assert.Equal(result.State.TurnBattle.Turns.RngState, result.State.TurnBattle.Battle.RngState);
        Assert.Equal(50 - damage, result.State.Combatants[1].Hp);
        Assert.Equal(push ? new(4, 2, 0) : new TilePosition(3, 2, 0), Position(result.State, B));
        Assert.Equal(push ? 1 : 0, result.Events.OfType<ThrowStoneKnockbackEvent>().Count());
        Assert.Same(before.Combatants[0], result.State.Combatants[0]);
        Assert.Equal(before.Combatants[1].Facing, result.State.Combatants[1].Facing);
        Assert.Equal(original, Snapshot(before));
        Assert.True(result.State.TurnBattle.Turns.Units[0].ActionTaken);
        Assert.False(result.State.TurnBattle.Turns.Units[0].MovementTaken);
        Assert.Same(before.TurnBattle.Battle.Map, result.State.TurnBattle.Battle.Map);
        Assert.Equal(before.TurnBattle.Turns.Units[1], result.State.TurnBattle.Turns.Units[1]);
        Assert.Equal(before.TurnBattle.Turns.Ticks, result.State.TurnBattle.Turns.Ticks);
    }

    [Theory]
    [InlineData(1, 0, true)] [InlineData(4, 0, true)] [InlineData(2, 2, true)] [InlineData(3, 1, true)]
    [InlineData(5, 0, false)] [InlineData(3, 2, false)] [InlineData(0, 5, false)]
    public void Range_uses_Manhattan_including_diagonals(int dx, int dy, bool admitted)
    {
        var state = State(actor: new(1, 1, 0), target: new(1 + dx, 1 + dy, 0));
        var result = Apply(state);
        Assert.Equal(admitted, result.Accepted);
        if (!admitted) Atomic(state, result, AttackCommandRejection.OutOfRange);
    }

    public static IEnumerable<object[]> FacingCases()
    {
        (int x, int y, BattleFacing[] front)[] offsets =
        [
            (1,0,[BattleFacing.West]), (-1,0,[BattleFacing.East]),
            (0,1,[BattleFacing.South]), (0,-1,[BattleFacing.North]),
            (1,1,[BattleFacing.West,BattleFacing.South]), (-1,1,[BattleFacing.East,BattleFacing.South]),
            (1,-1,[BattleFacing.West,BattleFacing.North]), (-1,-1,[BattleFacing.East,BattleFacing.North]),
            (2,1,[BattleFacing.West]), (1,2,[BattleFacing.South]),
        ];
        foreach (var (x,y,front) in offsets)
        foreach (var facing in Enum.GetValues<BattleFacing>())
            yield return [x,y,facing,front.Contains(facing)];
    }

    [Theory] [MemberData(nameof(FacingCases))]
    public void Front_class_evade_100_and_all_diagonal_facing_cases(int dx, int dy, BattleFacing facing, bool front)
    {
        var before = State(actor: new(3,3,0), target: new(3+dx,3+dy,0), evade: 100, facing: facing);
        var result = Apply(before);
        Assert.True(result.Accepted);
        Assert.Equal(!front, result.Events.OfType<ThrowStoneResolvedEvent>().Single().Hit);
        Assert.Equal(front ? 4ul : 7ul, result.State.TurnBattle.Turns.RngState.DrawCount);
        Assert.Equal(before.Combatants[0].Facing, result.State.Combatants[0].Facing);
        Assert.Equal(facing, result.State.Combatants[1].Facing);
    }

    [Theory]
    [InlineData(4,3,5,3)] [InlineData(2,3,1,3)] [InlineData(3,4,3,5)] [InlineData(3,2,3,1)]
    [InlineData(4,4,4,5)] [InlineData(2,4,2,5)] [InlineData(4,2,4,1)] [InlineData(2,2,2,1)]
    [InlineData(5,4,6,4)] [InlineData(4,5,4,6)]
    public void Knockback_dominant_axis_and_Y_ties_preserve_target_choices(int x, int y, int endX, int endY)
    {
        var before = State(seed: 3, actor: new(3,3,0), target: new(x,y,0), targetMoved: true, targetActed: true);
        var result = Apply(before);
        Assert.Equal(new ThrowStoneKnockbackEvent(B, new(x,y,0), new(endX,endY,0)), result.Events[^1]);
        Assert.Equal(new(endX,endY,0), Position(result.State,B));
        var body = result.State.TurnBattle.Battle.Units[1];
        Assert.True(body.HasMoved); Assert.Same(before.TurnBattle.Battle.Units[1].MovementProfile, body.MovementProfile);
        Assert.Equal(before.TurnBattle.Turns.Units[1], result.State.TurnBattle.Turns.Units[1]);
        Assert.Equal(before.Combatants[1].Facing, result.State.Combatants[1].Facing);
        Assert.Equal(3, result.Events.Length - 1); // choice, result, HP, then displacement.
    }

    [Theory] [InlineData(6,3,5,3)] [InlineData(0,3,1,3)] [InlineData(3,6,3,5)]
    [InlineData(3,0,3,1)] [InlineData(6,6,5,5)]
    public void Off_map_push_suppression_keeps_hit_and_all_seven_draws(int tx, int ty, int ax, int ay)
    {
        var result = Apply(State(seed:3, actor:new(ax,ay,0), target:new(tx,ty,0)));
        Assert.True(result.Events.OfType<ThrowStoneResolvedEvent>().Single().KnockbackAttempted);
        Assert.Equal(40, result.State.Combatants[1].Hp);
        Assert.Equal(new(tx,ty,0), Position(result.State,B));
        Assert.Empty(result.Events.OfType<ThrowStoneKnockbackEvent>());
        Assert.Equal(new FftRandomState(273505290,7), result.State.TurnBattle.Turns.RngState);
    }

    [Theory]
    [InlineData(5,0,AttackUnitStatus.Dead)] [InlineData(10,0,AttackUnitStatus.Dead)] [InlineData(11,1,AttackUnitStatus.Critical)]
    [InlineData(12,2,AttackUnitStatus.Critical)] [InlineData(13,3,AttackUnitStatus.None)]
    public void Lethal_push_cancellation_and_integer_Critical_boundary(int maxHp, int hp, AttackUnitStatus status)
    {
        var result = Apply(State(seed:3, maximum:maxHp, ko:255));
        Assert.Equal(hp,result.State.Combatants[1].Hp); Assert.Equal(status,result.State.Combatants[1].Status);
        Assert.Equal(new FftRandomState(273505290,7),result.State.TurnBattle.Turns.RngState);
        if (hp == 0)
        {
            Assert.Equal(0,result.State.Combatants[1].KoCount);
            Assert.Equal(new ThrowStoneResolvedEvent(A,Stone,B,true,10,true),result.Events[1]);
            Assert.IsType<AttackHpChangedEvent>(result.Events[2]);
            Assert.IsType<AttackStatusChangedEvent>(result.Events[3]);
            Assert.Equal(new AttackKnockedOutEvent(B,0),result.Events[4]);
            Assert.Empty(result.Events.OfType<ThrowStoneKnockbackEvent>());
            Atomic(result.State,Apply(result.State),AttackCommandRejection.ActionAlreadyTaken);
            var ended = AttackBattleSimulator.Wait(result.State,A); Assert.True(ended.Accepted);
            Assert.False(AttackBattleSimulator.Advance(ended.State).Accepted); // Existing post-KO scheduler boundary.
        }
        else Assert.IsType<ThrowStoneKnockbackEvent>(result.Events[^1]);
    }

    [Theory] [InlineData(5,10)] [InlineData(6,12)] [InlineData(99,198)]
    public void Current_PA_feeds_damage_without_weapon_power_or_critical(int pa,int damage)
    {
        var result = Apply(State(seed:3,pa:pa,maximum:65535));
        Assert.Equal(damage,result.Events.OfType<ThrowStoneResolvedEvent>().Single().Damage);
        Assert.Equal(65535-damage,result.State.Combatants[1].Hp);
        Assert.Empty(result.Events.OfType<AttackResolvedEvent>());
    }

    [Theory]
    [InlineData("actor",AttackCommandRejection.UnknownActor)] [InlineData("target",AttackCommandRejection.UnknownTarget)]
    [InlineData("inactive",AttackCommandRejection.InactiveActor)] [InlineData("spent",AttackCommandRejection.ActionAlreadyTaken)]
    [InlineData("self",AttackCommandRejection.SelfTarget)] [InlineData("dead",AttackCommandRejection.DeadTarget)]
    [InlineData("effect",AttackCommandRejection.UnknownAbility)] [InlineData("grant",AttackCommandRejection.AbilityNotGranted)]
    [InlineData("pa0",AttackCommandRejection.UnsupportedSetup)] [InlineData("pa100",AttackCommandRejection.UnsupportedSetup)]
    public void Invalid_actions_reject_atomically_before_drawing(string kind,AttackCommandRejection expected)
    {
        var state = State(pa:kind=="pa0"?0:kind=="pa100"?100:5,acted:kind=="spent",dead:kind=="dead");
        var command = new UseTargetedAbilityCommand(kind=="actor"?new("sample:unit/missing"):kind=="inactive"?B:A,
            kind=="effect"?new("sample:ability/unknown"):Stone,kind=="target"?new("sample:unit/missing"):kind=="self"?A:B);
        Atomic(state,TargetedAbilityBattleSimulator.Apply(state,command,Profile(kind!="grant")),expected);
    }

    [Theory] [InlineData("blocked")] [InlineData("untargetable")] [InlineData("stop")] [InlineData("water")]
    [InlineData("height")] [InlineData("half")] [InlineData("ceiling")] [InlineData("surface")]
    [InlineData("slope")] [InlineData("layer")] [InlineData("overlap")] [InlineData("third")]
    public void Unsupported_geometry_even_away_from_path_rejects_before_Act_or_rng(string kind)
    {
        var state = State(geometry:kind);
        Atomic(state,Apply(state),AttackCommandRejection.UnsupportedSetup);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void Hit_rng_exhaustion_is_atomic_at_every_draw(int remaining)
    {
        var state=State(seed:3,count:ulong.MaxValue-(ulong)remaining);
        Atomic(state,Apply(state),AttackCommandRejection.RandomDrawCountExhausted);
    }

    [Theory] [InlineData(0,false)] [InlineData(1,false)] [InlineData(2,false)] [InlineData(3,false)] [InlineData(4,true)]
    public void Miss_needs_only_its_four_draws(int remaining,bool accepted)
    {
        var state=State(seed:19,evade:100,count:ulong.MaxValue-(ulong)remaining);
        var result=Apply(state); Assert.Equal(accepted,result.Accepted);
        if (accepted) Assert.Equal(ulong.MaxValue,result.State.TurnBattle.Turns.RngState.DrawCount);
        else Atomic(state,result,AttackCommandRejection.RandomDrawCountExhausted);
    }

    [Fact]
    public void Exact_hit_budget_and_repeated_live_probes_are_pure()
    {
        var input=State(seed:3,count:ulong.MaxValue-7);
        string before=Snapshot(input);
        var expected=Apply(input);
        Assert.True(expected.Accepted); Assert.Equal(ulong.MaxValue,expected.State.TurnBattle.Turns.RngState.DrawCount);
        for(int i=0;i<10;i++)
        {
            var probe=Apply(input);
            Assert.Equal(Snapshot(expected.State),Snapshot(probe.State));
            Assert.Equal(expected.Events.ToArray(),probe.Events.ToArray());
        }
        Assert.Equal(before,Snapshot(input));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Act_Move_Wait_order_preserves_existing_CT_refunds(bool moveFirst)
    {
        var initial=State(seed:3);
        var state=initial;
        if(moveFirst) { var move=AttackBattleSimulator.Move(state,new(A,new(2,3,0))); Assert.True(move.Accepted); state=move.State; }
        var act=Apply(state); Assert.True(act.Accepted);
        if(moveFirst)
        {
            Assert.Null(act.State.TurnBattle.Turns.ActiveUnitId);
            Assert.Equal(15,act.State.TurnBattle.Turns.Units[0].Ct);
            Assert.IsType<AttackTurnBattleEvent>(act.Events[1]);
            Assert.IsType<ThrowStoneResolvedEvent>(act.Events[2]);
        }
        else
        {
            var move=AttackBattleSimulator.Move(act.State,new(A,new(2,3,0))); Assert.True(move.Accepted);
            Assert.Null(move.State.TurnBattle.Turns.ActiveUnitId);
            var wait=AttackBattleSimulator.Wait(act.State,A); Assert.True(wait.Accepted);
            Assert.Equal(35,wait.State.TurnBattle.Turns.Units[0].Ct);
        }
    }

    [Fact]
    public void Command_journal_replays_accepts_rejects_pushes_moves_and_later_readiness()
    {
        var start=State(seed:3);
        var journal=new List<Func<AttackBattleSnapshot,AttackBattleResult>>
        {
            Apply, Apply, s=>AttackBattleSimulator.Move(s,new(A,new(2,3,0))), s=>AttackBattleSimulator.Advance(s),
            s=>TargetedAbilityBattleSimulator.Apply(s,new(B,Stone,A),Profile()), s=>AttackBattleSimulator.Wait(s,B),
            s=>AttackBattleSimulator.Advance(s)
        };
        var current=start;
        var expected=new List<(bool accepted,string state,AttackResultEvent[] events)>();
        foreach(var command in journal)
        {
            var result=command(current); current=result.State;
            expected.Add((result.Accepted,Snapshot(current),result.Events.ToArray()));
        }
        Assert.True(expected[0].accepted); Assert.False(expected[1].accepted);
        Assert.True(expected[2].accepted); Assert.True(expected[3].accepted);
        Assert.False(expected[4].accepted); Assert.True(expected[5].accepted); Assert.True(expected[6].accepted);
        for(int repeat=0;repeat<2;repeat++)
        {
            var replay=start;
            for(int i=0;i<journal.Count;i++)
            {
                var result=journal[i](replay); replay=result.State;
                Assert.Equal(expected[i].accepted,result.Accepted); Assert.Equal(expected[i].state,Snapshot(replay));
                Assert.Equal(expected[i].events,result.Events.ToArray());
            }
        }
        Assert.Equal(new(3,2,0),Position(start,B));
    }

    [Fact]
    public void A_push_to_distance_five_is_rechecked_after_legal_turn_continuation()
    {
        var state = Apply(State(seed: 3, actor: new(1,1,0), target: new(5,1,0))).State;
        Assert.Equal(new(6,1,0), Position(state,B));
        state = AttackBattleSimulator.Wait(state,A).State;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var advance = AttackBattleSimulator.Advance(state);
            Assert.True(advance.Accepted); state = advance.State;
            if (state.TurnBattle.Turns.ActiveUnitId == A)
            {
                Atomic(state, Apply(state), AttackCommandRejection.OutOfRange);
                return;
            }
            var wait = AttackBattleSimulator.Wait(state,B); Assert.True(wait.Accepted); state = wait.State;
        }
        Assert.Fail("The authored clock failed to return A's turn.");
    }

    [Fact]
    public void Either_explicitly_granted_unit_can_act_without_mutating_the_other_units_choices()
    {
        var original = State(seed: 3, target: new(3,2,0));
        var turns = new TurnCycleState([new(A,10,15,0),new(B,12,0,1,true)],
            original.TurnBattle.Turns.RngState,12,TurnCyclePhase.UnitTurn);
        var state = new AttackBattleSnapshot(new(original.TurnBattle.Battle,turns),original.Combatants);
        var profile = new TargetedAbilityBattleProfile(Ground,[new(Stone)],[new(A,[],0),new(B,[Stone],0)]);
        var result = TargetedAbilityBattleSimulator.Apply(state,new(B,Stone,A),profile);
        Assert.True(result.Accepted);
        Assert.Equal(new ThrowStoneResolvedEvent(B,Stone,A,true,10,true),result.Events[1]);
        Assert.Equal(new ThrowStoneKnockbackEvent(A,new(2,2,0),new(1,2,0)),result.Events[^1]);
        Assert.Equal(state.TurnBattle.Turns.Units[0],result.State.TurnBattle.Turns.Units[0]);
        Assert.Equal(BattleFacing.South,result.State.Combatants[0].Facing);
        Assert.Equal(BattleFacing.West,result.State.Combatants[1].Facing);
    }

    [Fact]
    public void Profiles_snapshot_collections_and_reject_ambiguous_or_unsupported_bindings()
    {
        var ids=new List<AssetId>{Stone};
        var grant=new TargetedAbilityUnitGrant(A,ids,0); ids.Clear(); Assert.Contains(Stone,grant.Abilities);
        var effects=new List<BoundedThrowStoneAbility>{new(Stone)};
        var units=new List<TargetedAbilityUnitGrant>{grant,new(B,[],0)};
        var profile=new TargetedAbilityBattleProfile(Ground,effects,units); effects.Clear(); units.Clear();
        Assert.True(TargetedAbilityBattleSimulator.Apply(State(),new(A,Stone,B),profile).Accepted);
        Assert.Throws<ArgumentException>(()=>new BoundedThrowStoneAbility(default));
        Assert.Throws<ArgumentException>(()=>new TargetedAbilityUnitGrant(default,[],0));
        Assert.Throws<NotSupportedException>(()=>new TargetedAbilityUnitGrant(A,[Stone],1));
        Assert.Throws<ArgumentException>(()=>new TargetedAbilityUnitGrant(A,[Stone,Stone],0));
        Assert.Throws<ArgumentException>(()=>new TargetedAbilityUnitGrant(A,[default],0));
        Assert.Throws<ArgumentException>(()=>new TargetedAbilityBattleProfile(default,[],[]));
        Assert.Throws<ArgumentException>(()=>new TargetedAbilityBattleProfile(Ground,[],[grant]));
        Assert.Throws<ArgumentException>(()=>new TargetedAbilityBattleProfile(Ground,[new(Stone),new(Stone)],[]));
        Assert.Throws<ArgumentException>(()=>new TargetedAbilityBattleProfile(Ground,[new(Stone)],[grant,grant]));
        var state = State();
        Atomic(state,TargetedAbilityBattleSimulator.Apply(state,new(A,Stone,B),
            new(Ground,[new(Stone)],[grant])),AttackCommandRejection.UnsupportedSetup);
    }

    [Fact]
    public void Programmer_null_arguments_remain_explicit()
    {
        Assert.Throws<ArgumentNullException>(()=>TargetedAbilityBattleSimulator.Apply(null!,new(A,Stone,B),Profile()));
        Assert.Throws<ArgumentNullException>(()=>TargetedAbilityBattleSimulator.Apply(State(),null!,Profile()));
        Assert.Throws<ArgumentNullException>(()=>TargetedAbilityBattleSimulator.Apply(State(),new(A,Stone,B),null!));
    }

    private static TargetedAbilityBattleProfile Profile(bool grant=true) =>
        new(Ground,[new(Stone)],[new(A,grant?[Stone]:[],0),new(B,[],0)]);
    private static AttackBattleResult Apply(AttackBattleSnapshot state) => TargetedAbilityBattleSimulator.Apply(state,new(A,Stone,B),Profile());
    private static TilePosition Position(AttackBattleSnapshot state,BattleUnitId id) => state.TurnBattle.Battle.Units.Single(u=>u.Id==id).Position;
    private static void Atomic(AttackBattleSnapshot before,AttackBattleResult result,AttackCommandRejection rejection)
    {
        Assert.False(result.Accepted); Assert.Equal(rejection,result.AttackRejection); Assert.Empty(result.Events);
        Assert.Same(before,result.State);
    }
    private static string Snapshot(AttackBattleSnapshot state) => JsonSerializer.Serialize(new
    {state.Combatants,state.TurnBattle.Turns,state.TurnBattle.Battle.Units,state.TurnBattle.Battle.RngState});

    private static AttackBattleSnapshot State(uint seed=1,int pa=5,int evade=0,BattleFacing facing=BattleFacing.West,
        TilePosition? actor=null,TilePosition? target=null,int maximum=50,int ko=0,ulong count=0,
        bool moved=false,bool acted=false,bool targetMoved=false,bool targetActed=false,bool dead=false,string? geometry=null)
    {
        var rng=new FftRandomState(seed,count);
        var tiles=Enumerable.Repeat(new BattleTile(Ground,10),49*(geometry=="layer"?2:1)).ToArray();
        tiles[^1]=geometry switch
        {
            "blocked"=>new(Ground,10,movementFlags:BattleTileMovementFlags.Blocked),
            "untargetable"=>new(Ground,10,movementFlags:BattleTileMovementFlags.Untargetable),
            "stop"=>new(Ground,10,movementFlags:BattleTileMovementFlags.CannotStop),
            "water"=>new(Ground,10,waterDepth:1), "height"=>new(Ground,11),
            "half"=>new(Ground,10,halfHeight:1), "ceiling"=>new(Ground,10,ceilingDepth:1),
            "surface"=>new(new TerrainSurfaceId("sample:terrain/uncertified"),10),
            "slope"=>new(Ground,10,slope:TileSlope.FromPackedByte(1)), _=>tiles[^1]
        };
        var map=new BattleMapDefinition(7,7,geometry=="layer"?2:1,tiles);
        var bodies=new List<BattleUnitState>{new(A,actor??new(2,2,0),3,moved),new(B,geometry=="overlap"?actor??new(2,2,0):target??new(3,2,0),3,targetMoved)};
        var turns=new List<TurnUnitState>{new(A,10,15,0,true,moved,acted),new(B,12,83,1,false,targetMoved,targetActed)};
        var combat=new List<AttackUnitState>{Unit(A,50,pa,BattleFacing.South,0,0,false),Unit(B,maximum,5,facing,evade,ko,dead)};
        if(geometry=="third")
        {
            var c=new BattleUnitId("sample:unit/c"); bodies.Add(new(c,new(0,0,0),3)); turns.Add(new(c,10,0,2)); combat.Add(Unit(c,50,5,BattleFacing.East,0,0,false));
        }
        return new(new(new(map,bodies,rng),new(turns,rng,12,TurnCyclePhase.UnitTurn)),combat);
    }
    private static AttackUnitState Unit(BattleUnitId id,int maximum,int pa,BattleFacing facing,int evade,int ko,bool dead) =>
        new(id,dead?0:maximum,maximum,0,0,pa,evade,0,0,0,70,0,facing,dead?AttackUnitStatus.Dead:AttackUnitStatus.None,ko,new("sample:item/sword"));
}
