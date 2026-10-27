using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

/// <summary>Closed source-derived projection tests; no original-game or full-AI equivalence claim.</summary>
[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiCandidateProjectionTests
{
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly FlatSwordBattleProfile Sword = new(Ground);
    private static readonly FlatOrdinaryMovementProfile Movement = new(Ground, A, B);
    private static readonly SwordAiCandidateProfile Profile = new(Sword, Movement, [new(A, 255), new(B, 19)]);
    private static readonly FftRandomState Decision = new(1, 7);

    public static IEnumerable<object[]> Proposals()
    {
        using var document = Contract();
        foreach (var row in document.RootElement.GetProperty("vectors").EnumerateArray()
            .Where(r => r.GetProperty("family").GetString() == "proposed-candidate"))
            yield return [row.GetProperty("id").GetString()!];
    }

    [Theory] [MemberData(nameof(Proposals))]
    public void Executes_18_normalized_ZOD124_candidate_proposals(string id)
    {
        using var document = Contract();
        var row = document.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        var d = row.GetProperty("inputs"); var e = row.GetProperty("expected");
        int I(string key) => d.GetProperty(key).GetInt32();
        bool Flag(string key) => d.GetProperty(key).GetBoolean();
        TilePosition Tile(string key) => new(d.GetProperty(key)[0].GetInt32()+2,d.GetProperty(key)[1].GetInt32()+2,0);
        var order = Enum.Parse<SwordAiCandidateOrder>(d.GetProperty("order").GetString()!);
        var origin = Tile("origin"); var destination = Tile("destination");
        // The historical descriptions supply scalar formula/placement certificates, not full maps.
        // Materialize legal action origins, and explicit failed certificates as real bad geometry/destinations.
        var enemy = order == SwordAiCandidateOrder.MoveThenAct && destination != origin
            ? new TilePosition(destination.X+1,destination.Y,0) : new TilePosition(origin.X-1,origin.Y,0);
        var state = State(origin: origin, enemy: enemy, ct: I("actorCt"), targetCt: I("targetCt"),
            speed: I("actorSpeed"), targetSpeed: I("targetSpeed"), slot: I("actorSlot"), moved: Flag("moved"),
            acted: Flag("acted"), hp: Flag("terminal") ? 0 : I("targetHp"), maxHp: I("targetMaxHp"),
            evade: 100-I("hitPercent"), targetFacing: Enum.Parse<BattleFacing>(d.GetProperty("targetFacing").GetString()!),
            targetMoved: Flag("targetMoved"), targetActed: Flag("targetActed"), ticks: d.GetProperty("elapsedTicks").GetUInt64(),
            geometry: Flag("profileSupported") ? null : "cost-two");
        var candidate = new SwordAiCandidate(A,B,Flag("placementAllowed") ? destination : new(99,99,0),order);
        var rng = new FftRandomState(d.GetProperty("rngWord").GetUInt32(),d.GetProperty("rngCount").GetUInt64());
        var profile = new SwordAiCandidateProfile(Sword,Movement,[new(A,255),new(B,(byte)I("deathCounter"))]);
        var result = SwordAiCandidateProjection.Evaluate(state,candidate,new(Weapon,I("damage")/5),profile,rng);
        Assert.Equal(e.GetProperty("disposition").GetString(),result.Disposition.ToString());
        Assert.Equal(new FftRandomState(e.GetProperty("rngWord").GetUInt32(),e.GetProperty("rngCount").GetUInt64()),result.RngState);
        if (result.Disposition == SwordAiCandidateDisposition.Rejected)
        { Assert.Null(result.Outcome); Assert.Equal(e.GetProperty("reason").GetString(),result.Rejection.ToString()); return; }
        Assert.Equal(SwordAiCandidateRejection.None,result.Rejection);
        if (result.Disposition == SwordAiCandidateDisposition.NotValuable) { Assert.Null(result.Outcome); return; }
        var expected = e.GetProperty("outcome"); var outcome = Assert.IsType<SwordAiCandidateOutcome>(result.Outcome);
        Assert.Equal(destination,Unit(outcome,A).Position);
        Assert.Equal(Enum.Parse<BattleFacing>(d.GetProperty("facing").GetString()!),Unit(outcome,A).Facing);
        Assert.Equal(expected.GetProperty("targetFacing").GetString(),Unit(outcome,B).Facing.ToString());
        Assert.Equal(expected.GetProperty("turnEndCt").GetInt32(),outcome.TurnEndCt);
        Assert.Equal(expected.GetProperty("target").GetProperty("hp").GetInt32(),Unit(outcome,B).Hp);
        Assert.Equal(expected.GetProperty("target").GetProperty("status").GetString(),Unit(outcome,B).Status.ToString());
        Assert.Equal(expected.GetProperty("target").GetProperty("deathCounter").GetInt32(),Unit(outcome,B).DeathCounter);
        var clock = expected.GetProperty("clock");
        Assert.Equal(clock.GetProperty("elapsedTicks").GetUInt64(),outcome.ElapsedTicks);
        Assert.Equal(clock.GetProperty("upkeepDraws").GetInt32(),outcome.UpkeepPicks.Length);
        foreach (var u in clock.GetProperty("units").EnumerateArray())
        {
            var actual = Unit(outcome,u.GetProperty("slot").GetInt32() == I("actorSlot") ? A : B);
            Assert.Equal(u.GetProperty("ct").GetInt32(),actual.Ct);
            Assert.Equal(u.GetProperty("moved").GetBoolean(),actual.MovementTaken);
            Assert.Equal(u.GetProperty("acted").GetBoolean(),actual.ActionTaken);
        }
        Assert.Equal(clock.GetProperty("readySlot").GetInt32() == I("actorSlot") ? A : B,outcome.MoveOpportunityUnitId);
    }

    [Theory]
    [InlineData(false, SwordAiCandidateOrder.MoveThenAct)] [InlineData(false, SwordAiCandidateOrder.ActThenMove)]
    [InlineData(true, SwordAiCandidateOrder.MoveThenAct)] [InlineData(true, SwordAiCandidateOrder.ActThenMove)]
    [Trait("Contract", "QueryIsolation")]
    public void Actor_identity_and_collection_order_preserve_both_action_orders(bool second, SwordAiCandidateOrder order)
    {
        var enemy = order == SwordAiCandidateOrder.MoveThenAct ? new TilePosition(0, 2, 0) : new(3, 2, 0);
        var state = State(second: second, enemy: enemy);
        var reversed = State(second: second, enemy: enemy, reverse: true);
        string before = Snapshot(state), reversedBefore = Snapshot(reversed);
        var candidate = new SwordAiCandidate(second ? B : A, second ? A : B, new(1, 2, 0), order);
        var result = Project(state, candidate); Assert.Equal(candidate.ActorId, Projected(result).ActorId);
        Assert.Equal(25, Unit(result.Outcome!, candidate.TargetId).Hp);
        Assert.Equal(candidate.Destination, Unit(result.Outcome!, candidate.ActorId).Position);
        Assert.Equal(Decision.DrawCount + 5ul, result.RngState.DrawCount);
        Assert.Equal(Serialize(result), Serialize(Project(reversed, candidate)));
        Assert.Equal(before, Snapshot(state)); Assert.Equal(reversedBefore, Snapshot(reversed));
    }

    public static IEnumerable<object[]> OrdersAndFaces()
    {
        foreach (var order in Enum.GetValues<SwordAiCandidateOrder>())
        foreach (var facing in Enum.GetValues<BattleFacing>())
            yield return [order,facing];
    }

    [Theory] [MemberData(nameof(OrdersAndFaces))]
    public void Both_action_orders_preserve_each_facing_and_use_the_correct_formula_origin(
        SwordAiCandidateOrder order, BattleFacing facing)
    {
        var actor = A; var target = B;
        var state = State(facing: facing,enemy: new(4,4,0));
        string before = Snapshot(state);
        var candidate = new SwordAiCandidate(actor,target,new(1,2,0),order);
        // Neither action origin is adjacent to this enemy; both orders must reject before draws.
        var invalid = Project(state,candidate);
        Atomic(invalid,SwordAiCandidateRejection.OutOfRange);
        var enemy = order == SwordAiCandidateOrder.MoveThenAct ? new TilePosition(0,2,0) : new(3,2,0);
        state = State(facing: facing,enemy: enemy);
        before = Snapshot(state);
        var result = Project(state,candidate); var outcome = Projected(result);
        Assert.Equal(new(1,2,0),Unit(outcome,actor).Position); Assert.Equal(facing,Unit(outcome,actor).Facing);
        Assert.Equal(BattleFacing.East,Unit(outcome,target).Facing);
        Assert.Equal(25,Unit(outcome,target).Hp); Assert.Equal(15,outcome.TurnEndCt);
        Assert.Equal(before,Snapshot(state)); Assert.Equal(new FftRandomState(57,100),state.TurnBattle.Turns.RngState);
    }

    [Theory] [InlineData(false,35)] [InlineData(true,15)]
    public void Stay_respects_incoming_Move_and_real_path_heading_is_not_used(bool moved,int expectedCt)
    {
        var state = State(moved: moved);
        var outcome = Projected(Project(state)); Assert.Equal(expectedCt,outcome.TurnEndCt);
        Assert.Equal(BattleFacing.West,Unit(outcome,A).Facing);
        var movement = FlatOrdinaryMovement.Evaluate(state,A,Movement,new(2,3,0));
        Assert.Equal(BattleFacing.North,movement.Outcome!.Path!.FinalHeading);
        Atomic(Project(state,new(A,B,new(2,3,0),SwordAiCandidateOrder.ActThenMove)),
            moved ? SwordAiCandidateRejection.InvalidCandidate : SwordAiCandidateRejection.None, accepted: !moved);
    }

    [Theory] [InlineData(0,5,50,50,45,AttackUnitStatus.None)] [InlineData(5,0,50,50,50,AttackUnitStatus.None)]
    [InlineData(5,8,50,50,10,AttackUnitStatus.Critical)] [InlineData(1,40,51,51,11,AttackUnitStatus.None)]
    [InlineData(1,41,51,51,10,AttackUnitStatus.Critical)] [InlineData(5,10,50,50,0,AttackUnitStatus.Dead)]
    [InlineData(255,64,65535,65535,64536,AttackUnitStatus.None)] [InlineData(5,5,10,50,0,AttackUnitStatus.Dead)]
    public void HP_boundaries_and_projected_Critical_or_Dead_are_applied_without_a_live_state_guard(
        int pa,int power,int hp,int maxHp,int expected,AttackUnitStatus status)
    {
        var state = State(pa: pa,hp: hp,maxHp: maxHp);
        var result = Project(state,power: power); var target = Unit(Projected(result),B);
        Assert.Equal(expected,target.Hp); Assert.Equal(status,target.Status);
        Assert.Equal(status == AttackUnitStatus.Dead ? 3 : 19,target.DeathCounter);
        Assert.Equal(status == AttackUnitStatus.Dead,result.Outcome!.TargetKnockedOut);
        Assert.Equal(hp,state.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.Equal(255,state.Combatants.Single(u => u.UnitId == B).KoCount);
    }

    [Theory] [InlineData(0,100)] [InlineData(50,50)] [InlineData(99,1)] [InlineData(100,0)]
    public void Partial_accuracy_projects_full_damage_and_certain_evade_retains_only_four_draws(int evade,int hit)
    {
        var state = State(evade: evade);
        var result = Project(state);
        if (hit == 0)
        {
            Assert.Equal(SwordAiCandidateDisposition.NotValuable,result.Disposition); Assert.Null(result.Outcome);
            Assert.Equal(SwordAiCandidateRejection.None,result.Rejection); Assert.Equal(new FftRandomState(3295386429,11),result.RngState);
        }
        else
        {
            Assert.Equal(hit,Projected(result).HitPercent); Assert.Equal(25,Unit(result.Outcome!,B).Hp);
            Assert.Equal(new FftRandomState(4182499122,12),result.RngState);
        }
    }

    [Theory] [InlineData(100)] [InlineData(101)] [InlineData(199)] [InlineData(200)] [InlineData(254)]
    public void Already_ready_enemy_is_picked_before_CT_add_and_uses_modulo(int ct)
    {
        var outcome = Projected(Project(State(targetCt: ct,ticks: ulong.MaxValue)));
        Assert.Equal(0ul,outcome.ElapsedTicks); Assert.Equal(ulong.MaxValue,outcome.Ticks);
        Assert.Equal(B,outcome.MoveOpportunityUnitId); Assert.Equal(ct%100,Unit(outcome,B).Ct);
        Assert.Equal(35,Unit(outcome,A).Ct); Assert.False(Unit(outcome,B).MovementTaken); Assert.False(Unit(outcome,B).ActionTaken);
        Assert.Equal(new SwordAiUpkeepPick(B,ct,ct%100,false,31051),Assert.Single(outcome.UpkeepPicks));
    }

    [Theory] [InlineData(0,false)] [InlineData(1,false)] [InlineData(0,true)] [InlineData(1,true)]
    public void Equal_CT_uses_original_tie_keys_and_highest_CT_wins_before_tie(int slot,bool reverse)
    {
        var tied = Projected(Project(State(ct: 40,targetCt: 95,speed: 40,targetSpeed: 5,slot: slot,reverse: reverse)));
        Assert.Equal(slot == 0 ? A : B,tied.MoveOpportunityUnitId);
        Assert.Equal(1ul,tied.ElapsedTicks);
        var highest = Projected(Project(State(ct: 40,targetCt: 96,speed: 40,targetSpeed: 5,slot: slot,reverse: reverse)));
        Assert.Equal(B,highest.MoveOpportunityUnitId); Assert.Equal(1,Unit(highest,B).Ct);
    }

    [Fact]
    public void Byte_CT_cap_is_254_and_projected_dead_enemy_can_consume_101_upkeep_draws()
    {
        var cap = Projected(Project(State(ct: 0,targetCt: 90,speed: 1,targetSpeed: 255)));
        Assert.Equal(254,Assert.Single(cap.UpkeepPicks).CtBefore); Assert.Equal(54,Unit(cap,B).Ct);
        var state = State(ct: 0,targetCt: 0,speed: 1,targetSpeed: 255,enemy: new(4,2,0));
        var result = Project(state,new(A,B,new(3,2,0),SwordAiCandidateOrder.MoveThenAct),power: 10);
        var outcome = Projected(result);
        Assert.Equal(100ul,outcome.ElapsedTicks); Assert.Equal(101,outcome.UpkeepPicks.Length);
        Assert.All(outcome.UpkeepPicks.Take(100),p => { Assert.Equal(B,p.UnitId); Assert.True(p.Dead); Assert.Equal(254,p.CtBefore); Assert.Equal(54,p.CtAfter); });
        Assert.Equal(A,outcome.UpkeepPicks[^1].UnitId); Assert.False(outcome.UpkeepPicks[^1].Dead);
        Assert.Equal(3,Unit(outcome,B).DeathCounter); Assert.Equal(54,Unit(outcome,B).Ct);
        Assert.Equal(112ul,result.RngState.DrawCount); Assert.Equal(Advance(Decision,105),result.RngState);
        Assert.Equal(255,state.Combatants.Single(u => u.UnitId == B).KoCount);
    }

    [Fact]
    public void Dead_pick_can_precede_tick_overflow_and_all_staged_RNG_is_rolled_back()
    {
        var state = State(targetCt: 100,ticks: ulong.MaxValue);
        Atomic(Project(state,power: 10),SwordAiCandidateRejection.TickCountExhausted);
        Assert.Equal(100,state.TurnBattle.Turns.Units.Single(u => u.Id == B).Ct);
    }

    public static IEnumerable<object[]> Budgets()
    {
        // Exhaust every prefix of each distinct draw schedule; zero accuracy never reaches lethal work.
        foreach (var (evade, lethal, draws) in new[] { (0, false, 5), (0, true, 6), (100, false, 4) })
        for (int remaining=0; remaining<=draws; remaining++) yield return [evade,lethal,remaining];
    }

    [Theory] [MemberData(nameof(Budgets))]
    public void Every_formula_and_upkeep_draw_budget_rejects_atomically_or_fits_exactly(int evade,bool lethal,int remaining)
    {
        var state = State(evade: evade); var decision = new FftRandomState(1,ulong.MaxValue-(ulong)remaining);
        var result = Project(state,power: lethal ? 10 : 5,rng: decision);
        int needed = evade == 100 ? 4 : lethal ? 6 : 5;
        if (remaining < needed) Atomic(result,SwordAiCandidateRejection.RandomDrawCountExhausted,decision);
        else
        {
            Assert.Equal(evade == 100 ? SwordAiCandidateDisposition.NotValuable : SwordAiCandidateDisposition.Projected,result.Disposition);
            Assert.Equal(Advance(decision,needed),result.RngState);
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Tick_boundary_and_zero_accuracy_do_not_reserve_unneeded_clock_work(bool noAccuracy)
    {
        var state = State(ticks: ulong.MaxValue,evade: noAccuracy ? 100 : 0);
        var result = Project(state);
        if (noAccuracy) { Assert.Equal(SwordAiCandidateDisposition.NotValuable,result.Disposition); Assert.Equal(Advance(Decision,4),result.RngState); }
        else Atomic(result,SwordAiCandidateRejection.TickCountExhausted);
        var fits = Project(State(ticks: ulong.MaxValue-1));
        Assert.Equal(ulong.MaxValue,Projected(fits).Ticks);
    }

    [Fact]
    public void Authority_terminal_identity_order_and_null_errors_are_explicit_before_draws()
    {
        var state = State(); var unknown = new BattleUnitId("sample:unit/unknown");
        Atomic(Project(state,new(unknown,B,new(2,2,0),SwordAiCandidateOrder.MoveThenAct)),SwordAiCandidateRejection.UnknownActor);
        Atomic(Project(state,new(A,unknown,new(2,2,0),SwordAiCandidateOrder.MoveThenAct)),SwordAiCandidateRejection.UnknownTarget);
        Atomic(Project(state,new(A,A,new(2,2,0),SwordAiCandidateOrder.MoveThenAct)),SwordAiCandidateRejection.InvalidCandidate);
        Atomic(Project(state,new(A,B,new(2,2,0),(SwordAiCandidateOrder)99)),SwordAiCandidateRejection.InvalidCandidate);
        Atomic(Project(State(hp: 0)),SwordAiCandidateRejection.TerminalInput);
        Atomic(Project(State(acted: true)),SwordAiCandidateRejection.UnavailableAction);
        Atomic(Project(State(active: false)),SwordAiCandidateRejection.UnavailableAction);
        Atomic(Project(State(second: true)),SwordAiCandidateRejection.UnavailableAction);
        Assert.Throws<ArgumentNullException>(() => SwordAiCandidateProjection.Evaluate(null!,Stay(),new(Weapon,5),Profile,Decision));
        Assert.Throws<ArgumentNullException>(() => SwordAiCandidateProjection.Evaluate(state,null!,new(Weapon,5),Profile,Decision));
        Assert.Throws<ArgumentNullException>(() => SwordAiCandidateProjection.Evaluate(state,Stay(),new(Weapon,5),null!,Decision));
    }

    [Theory] [InlineData("blocked")] [InlineData("water")] [InlineData("height")] [InlineData("cost-two")]
    [InlineData("enemy-cost")] [InlineData("move256")] [InlineData("jump256")] [InlineData("fly")]
    [InlineData("traits")] [InlineData("body")] [InlineData("overlap")] [InlineData("large")]
    [Trait("Coverage", "CurrentAdmission")]
    public void Unsupported_whole_profiles_reject_without_rng_or_fallback(string geometry)
    { Atomic(Project(State(geometry: geometry),rng: new(1,ulong.MaxValue)),SwordAiCandidateRejection.UnsupportedProfile,new(1,ulong.MaxValue)); }

    [Theory] [InlineData(-1,2,0)] [InlineData(5,2,0)] [InlineData(2,2,1)] [InlineData(1,2,0)] [InlineData(4,4,0)]
    public void Invalid_blocked_occupied_layer_and_over_budget_destinations_reject_before_formula(int x,int y,int layer)
    { Atomic(Project(State(move: 1),new(A,B,new(x,y,layer),SwordAiCandidateOrder.MoveThenAct)),SwordAiCandidateRejection.InvalidCandidate); }

    [Fact]
    [Trait("Coverage", "CurrentAdmission")]
    public void Weapon_and_signed_envelope_guards_precede_random_work_even_at_certain_evade()
    {
        var state = State(evade: 100,pa: 255);
        Atomic(SwordAiCandidateProjection.Evaluate(state,Stay(),null,Profile,Decision),SwordAiCandidateRejection.UnsupportedProfile);
        Atomic(SwordAiCandidateProjection.Evaluate(state,Stay(),new(new("sample:item/wrong"),5),Profile,Decision),SwordAiCandidateRejection.UnsupportedProfile);
        Atomic(Project(state,power: 65),SwordAiCandidateRejection.UnsupportedProfile);
        Assert.Equal(SwordAiCandidateDisposition.NotValuable,Project(state,power: 64).Disposition);
    }

    [Fact]
    [Trait("Coverage", "CurrentAdmission")]
    public void Profile_requires_complete_matching_ground_opponents_and_baselines()
    {
        Assert.Throws<ArgumentNullException>(() => new SwordAiCandidateProfile(null!,Movement,[]));
        Assert.Throws<ArgumentNullException>(() => new SwordAiCandidateProfile(Sword,null!,[]));
        Assert.Throws<ArgumentNullException>(() => new SwordAiCandidateProfile(Sword,Movement,null!));
        foreach (var units in new SwordAiUnitBaseline[][] { [],[new(A,255)],[new(A,255),new(A,255)],[new(A,255),null!],
            [new(A,255),new(new("sample:unit/unknown"),255)] })
            Assert.Throws<ArgumentException>(() => new SwordAiCandidateProfile(Sword,Movement,units));
        Assert.Throws<ArgumentException>(() => new SwordAiCandidateProfile(new(new("sample:terrain/other")),Movement,[new(A,255),new(B,255)]));
        var unrelated = new FlatOrdinaryMovementProfile(Ground,A,new("sample:unit/unknown"));
        var profile = new SwordAiCandidateProfile(Sword,unrelated,[new(A,255),new(new("sample:unit/unknown"),255)]);
        Atomic(SwordAiCandidateProjection.Evaluate(State(),Stay(),new(Weapon,5),profile,Decision),SwordAiCandidateRejection.UnsupportedProfile);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    [Trait("Contract", "QueryIsolation")]
    public void Discard_carry_replay_and_independent_branches_have_no_hidden_random_or_live_mutation(bool noAccuracy)
    {
        var state = State(evade: noAccuracy ? 100 : 0); string before = Snapshot(state);
        var first = Project(state); var second = Project(state,rng: first.RngState);
        Assert.Equal(Serialize(first),Serialize(Project(state))); Assert.NotEqual(first.RngState,second.RngState);
        Assert.Equal(first.RngState.DrawCount+(noAccuracy ? 4ul : 5ul),second.RngState.DrawCount);
        for (int i=0;i<4;i++) Assert.Equal(Serialize(first),Serialize(Project(state)));
        Assert.Equal(before,Snapshot(state)); Assert.Equal(new FftRandomState(57,100),state.TurnBattle.Turns.RngState);
        Assert.Equal(20ul,state.TurnBattle.Turns.Ticks);
    }

    [Fact]
    public void Initial_Critical_actor_is_supported_and_baseline_metadata_is_immutable()
    {
        var state = State(actorHp: 10);
        var outcome = Projected(Project(state));
        Assert.Equal((10,AttackUnitStatus.Critical,255),(Unit(outcome,A).Hp,Unit(outcome,A).Status,(int)Unit(outcome,A).DeathCounter));
        Assert.Equal(255,Profile.Units.Single(u => u.UnitId == A).DeathCounter);
        Assert.Equal(19,Profile.Units.Single(u => u.UnitId == B).DeathCounter);
        Assert.Equal(AttackUnitStatus.Critical,state.Combatants.Single(u => u.UnitId == A).Status);
    }

    private static JsonDocument Contract() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","ai-candidate-contract-v1.json")));
    private static SwordAiCandidate Stay() => new(A,B,new(2,2,0),SwordAiCandidateOrder.MoveThenAct);
    private static SwordAiCandidateResult Project(AttackBattleSnapshot state,SwordAiCandidate? candidate=null,int power=5,FftRandomState? rng=null)
        => SwordAiCandidateProjection.Evaluate(state,candidate ?? Stay(),new(Weapon,power),Profile,rng ?? Decision);
    private static SwordAiProjectedUnit Unit(SwordAiCandidateOutcome outcome,BattleUnitId id) => outcome.Units.Single(u => u.UnitId == id);
    private static SwordAiCandidateOutcome Projected(SwordAiCandidateResult result)
    { Assert.Equal(SwordAiCandidateDisposition.Projected,result.Disposition); Assert.Equal(SwordAiCandidateRejection.None,result.Rejection); return Assert.IsType<SwordAiCandidateOutcome>(result.Outcome); }
    private static void Atomic(SwordAiCandidateResult result,SwordAiCandidateRejection rejection,FftRandomState? rng=null,bool accepted=false)
    {
        if (accepted) { Projected(result); return; }
        Assert.Equal(SwordAiCandidateDisposition.Rejected,result.Disposition); Assert.Equal(rejection,result.Rejection);
        Assert.Null(result.Outcome); Assert.Equal(rng ?? Decision,result.RngState);
    }
    private static FftRandomState Advance(FftRandomState rng,int count)
    { for (int i=0;i<count;i++) rng=new FftBiosRandom().Next(rng).State; return rng; }
    private static string Serialize(object value) => JsonSerializer.Serialize(value);
    private static string Snapshot(AttackBattleSnapshot state) => Serialize(new { state.Combatants,state.TurnBattle.Turns,
        state.TurnBattle.Battle.Map,state.TurnBattle.Battle.RngState,
        Units = state.TurnBattle.Battle.Units.Select(u => new { u.Id,u.Position,u.HasMoved,
            u.MovementProfile.Move,u.MovementProfile.Jump,u.MovementProfile.BodyHeight,u.MovementProfile.Mode,
            u.MovementProfile.Traits,Costs=u.MovementProfile.TerrainCosts.Select(c => new { Id=c.Key.Value,c.Value }) }) });

    private static AttackBattleSnapshot State(bool second=false,bool reverse=false,TilePosition? origin=null,TilePosition? enemy=null,
        int ct=15,int targetCt=95,int speed=5,int targetSpeed=5,int slot=0,bool moved=false,bool acted=false,bool active=true,
        int hp=50,int maxHp=50,int actorHp=50,int pa=5,int evade=0,int move=4,
        BattleFacing facing=BattleFacing.West,BattleFacing targetFacing=BattleFacing.East,
        bool targetMoved=true,bool targetActed=true,ulong ticks=20,string? geometry=null)
    {
        var actor=second ? B : A; var target=second ? A : B;
        var rng=new FftRandomState(57,100); int width=geometry == "large" ? 17 : 5;
        var tiles=Enumerable.Repeat(new BattleTile(Ground,10),width*5).ToArray();
        tiles[^1]=geometry switch { "blocked" => new(Ground,10,movementFlags: BattleTileMovementFlags.Blocked),
            "water" => new(Ground,10,waterDepth: 1), "height" => new(Ground,11), _ => tiles[^1] };
        UnitMovementProfile Move(bool foe) => new(geometry == "move256" ? 256 : move,geometry == "jump256" ? 256 : 0,
            geometry == "body" ? 7 : 6,geometry == "fly" ? UnitMovementMode.Fly : UnitMovementMode.Normal,
            geometry == "traits" ? UnitMovementTraits.AnyGround : UnitMovementTraits.None,
            [KeyValuePair.Create(Ground,new TerrainMovementCost(geometry == "cost-two" || foe && geometry == "enemy-cost" ? 2 : 1))]);
        var bodies=new BattleUnitState[] { new(actor,origin ?? new(2,2,0),Move(false),moved),
            new(target,geometry == "overlap" ? origin ?? new(2,2,0) : enemy ?? new(1,2,0),Move(true),targetMoved) };
        var turns=new TurnUnitState[] { new(actor,speed,ct,slot,active,moved,acted),
            new(target,targetSpeed,targetCt,1-slot,false,targetMoved,targetActed) };
        AttackUnitState Combat(BattleUnitId id,int current,int max,BattleFacing face,int evadeValue) =>
            new(id,current,max,0,0,pa,evadeValue,0,0,0,70,0,face,current == 0 ? AttackUnitStatus.Dead :
                current <= max/5 ? AttackUnitStatus.Critical : AttackUnitStatus.None,255,Weapon);
        var combat=new[] { Combat(actor,actorHp,50,facing,0), Combat(target,hp,maxHp,targetFacing,evade) };
        return new(new(new(new(width,5,1,tiles),reverse ? bodies.Reverse() : bodies,rng),
            new(reverse ? turns.Reverse() : turns,rng,ticks,active ? TurnCyclePhase.UnitTurn : TurnCyclePhase.PickUnit)),
            reverse ? combat.Reverse() : combat);
    }
}
