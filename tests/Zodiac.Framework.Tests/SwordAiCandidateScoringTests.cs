using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

/// <summary>Source-derived closed score regressions; no full selector or original execution claim.</summary>
[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiCandidateScoringTests
{
    private static readonly BattleUnitId A=new("sample:unit/a"), B=new("sample:unit/b");
    private static readonly AssetId Weapon=new("sample:item/plain-sword");
    private static readonly TerrainSurfaceId Ground=new("sample:terrain/ordinary-ground");
    private static readonly SwordAiCandidateProfile CandidateProfile=new(new(Ground),new(Ground,A,B),[new(A,255),new(B,19)]);
    private static readonly SwordAiScoreProfile ScoreProfile=new([new(A,SwordAiScoreFacts.ClosedProfile),new(B,SwordAiScoreFacts.ClosedProfile)]);
    private static readonly FftRandomState Decision=new(1,7);

    public static IEnumerable<object[]> Proposals()
    {
        using var document=Contract();
        foreach (var row in document.RootElement.GetProperty("vectors").EnumerateArray().Where(r=>r.GetProperty("family").GetString()=="proposed-scoring"))
            yield return [row.GetProperty("id").GetString()!];
    }

    [Theory] [MemberData(nameof(Proposals))]
    public void Addresses_14_ZOD126_normalized_proposals_with_13_wrapper_calls_and_one_MP_model_guard(string id)
    {
        using var document=Contract();
        var row=document.RootElement.GetProperty("vectors").EnumerateArray().Single(r=>r.GetProperty("id").GetString()==id);
        var d=row.GetProperty("inputs");var c=d.GetProperty("candidate");var p=d.GetProperty("profile");var e=row.GetProperty("expected");
        if (id=="unsupported-mp")
        {
            // The normalized fact maxMP1 cannot enter the existing live model. Do not extend it for this scorer.
            Assert.Throws<NotSupportedException>(()=>State(maxMp:p.GetProperty("units")[1].GetProperty("maxMp").GetInt32()));
            return;
        }
        var state=State(actorHp:d.GetProperty("actorHp").GetInt32(),actorMax:d.GetProperty("actorMaxHp").GetInt32(),
            hp:c.GetProperty("terminal").GetBoolean()?0:c.GetProperty("targetHp").GetInt32(),maxHp:c.GetProperty("targetMaxHp").GetInt32(),
            evade:100-c.GetProperty("hitPercent").GetInt32());
        var certificates=new List<SwordAiScoreUnitCertificate>();
        for(int i=0;i<2;i++)
        {
            var unit=p.GetProperty("units")[i];var facts=SwordAiScoreFacts.ClosedProfile;
            if(unit.GetProperty("monster").GetBoolean())facts&=~SwordAiScoreFacts.NonMonster;
            if(unit.GetProperty("keptSlots").GetInt32()!=7)facts&=~SwordAiScoreFacts.AllSevenEquipmentSlotsUnchanged;
            if(!p.GetProperty("stableOpponents").GetBoolean()||!p.GetProperty("unchangedTeams").GetBoolean())facts&=~SwordAiScoreFacts.StableOpponents;
            if(unit.GetProperty("golem").GetInt32()!=0||unit.GetProperty("baselineGolem").GetInt32()!=0)facts&=~SwordAiScoreFacts.ZeroCurrentAndBaselineGolem;
            var unitId=i==0?A:B;
            if(i==1&&!p.GetProperty("idsMatch").GetBoolean())unitId=new("sample:unit/other");
            certificates.Add(new(unitId,facts));
        }
        var result=Evaluate(state,power:c.GetProperty("damage").GetInt32()/5,scoreProfile:new(certificates));
        Assert.Equal(e.GetProperty("disposition").GetString(),result.Disposition.ToString());
        Assert.Equal(new FftRandomState(e.GetProperty("rngWord").GetUInt32(),e.GetProperty("rngCount").GetUInt64()),result.RngState);
        if(result.Disposition==SwordAiScoringDisposition.Rejected)
        {
            Assert.Equal(e.GetProperty("reason").GetString(),result.Rejection.ToString());Assert.Null(result.Score);
            if(result.Rejection==SwordAiScoringRejection.ProjectionRejected)
                Assert.Equal(e.GetProperty("projectionReason").GetString(),result.Projection!.Rejection.ToString());
            else Assert.Null(result.Projection);
            return;
        }
        Assert.Equal(SwordAiScoringRejection.None,result.Rejection);Assert.NotNull(result.Projection);
        if(result.Disposition==SwordAiScoringDisposition.NotValuable){Assert.Null(result.Score);return;}
        var expected=e.GetProperty("score");var score=Scored(result);
        Assert.Equal(expected.GetProperty("priority").GetInt32(),score.Priority);
        Assert.Equal(expected.GetProperty("hitPercent").GetInt32(),score.HitPercent);
        AssertContributions(score,expected.GetProperty("contributions"));
    }

    public static IEnumerable<object[]> StateKernels()
    {
        using var document=Contract();
        foreach(var row in document.RootElement.GetProperty("vectors").EnumerateArray().Where(r=>r.GetProperty("family").GetString()=="source-state"))
            yield return [row.GetProperty("id").GetString()!];
    }

    [Theory] [MemberData(nameof(StateKernels))]
    public void Source_state_kernels_preserve_priority_and_each_unit_contribution(string id)
    {
        using var document=Contract();
        var row=document.RootElement.GetProperty("vectors").EnumerateArray().Single(r=>r.GetProperty("id").GetString()==id);
        var units=row.GetProperty("inputs").GetProperty("units").EnumerateArray().ToArray();
        var ally=units.Single(u=>!u.GetProperty("enemy").GetBoolean());var enemy=units.Single(u=>u.GetProperty("enemy").GetBoolean());
        int targetHp=enemy.GetProperty("hp").GetInt32();
        // A Dead scoring kernel is reached with pre-hit HP1 and a one-damage sword; initial Dead remains rejected.
        var state=State(actorHp:ally.GetProperty("hp").GetInt32(),actorMax:ally.GetProperty("maxHp").GetInt32(),
            hp:targetHp==0?1:targetHp,maxHp:enemy.GetProperty("maxHp").GetInt32(),pa:1);
        var result=Evaluate(state,power:targetHp==0?1:0);var score=Scored(result);
        Assert.Equal(row.GetProperty("expected").GetProperty("priority").GetInt32(),score.Priority);
        AssertContributions(score,row.GetProperty("expected").GetProperty("contributions"));
        Assert.Equal(A,result.Projection!.Outcome!.ActorId);
        Assert.Equal(new[]{0,1},score.Contributions.Select(u=>u.TieKey));
        Assert.Equal(new[]{false,true},score.Contributions.Select(u=>u.Enemy));
    }

    [Theory] [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    [Trait("Contract", "QueryIsolation")]
    public void Actor_identity_and_collection_order_preserve_living_and_dead_score_contributions(bool second, bool lethal)
    {
        var state = State(second: second); var reversed = State(second: second, reverse: true);
        var before = Snapshot(state); var reversedBefore = Snapshot(reversed);
        var first = Evaluate(state, second: second, power: lethal ? 10 : 5);
        var score = Scored(first); Assert.Equal(second ? B : A, first.Projection!.Outcome!.ActorId);
        Assert.Equal(lethal ? 320 : 64, score.Priority); Assert.Equal(100, score.HitPercent);
        Assert.False(score.Contributions.Single(u => u.UnitId == (second ? B : A)).Enemy);
        Assert.True(score.Contributions.Single(u => u.UnitId == (second ? A : B)).Enemy);
        Assert.Equal(lethal, first.Projection.Outcome.TargetKnockedOut);
        Assert.Equal(Serialize(first), Serialize(Evaluate(reversed, second: second, power: lethal ? 10 : 5)));
        Assert.Equal(before, Snapshot(state)); Assert.Equal(reversedBefore, Snapshot(reversed));
    }

    [Theory] [InlineData(50,50,0,0)] [InlineData(50,25,0,64)] [InlineData(50,10,0,135)]
    [InlineData(50,1,1,320)] [InlineData(10,50,0,-135)] [InlineData(10,25,0,-71)] [InlineData(10,10,0,0)]
    public void Canonical_absolute_scores_are_not_damage_delta_or_missing_zero(int actorHp,int targetHp,int power,int expected)
    {
        var score=Scored(Evaluate(State(actorHp:actorHp,hp:targetHp,pa:1),power:power));
        Assert.Equal(expected,score.Priority);Assert.Equal(100,score.HitPercent);
        Assert.Equal(357,score.Contributions[0].EquipmentTerm);Assert.Equal(357,score.Contributions[1].EquipmentTerm);
        Assert.Equal(score.Priority,score.Contributions[^1].RunningPriority);
    }

    [Theory] [InlineData(65535,65535,50,50,128,64)] [InlineData(1,65535,50,50,0,-96)]
    [InlineData(50,50,51,51,128,63)] [InlineData(1,1,1,1,128,320)]
    public void HP_product_and_unequal_maxima_use_integer_floor(int actorHp,int actorMax,int hp,int maxHp,int actorTerm,int expected)
    {
        // Default damage25; maxHP1 takes a lethal projection, preserving the zero equipment-cancel rule.
        var score=Scored(Evaluate(State(actorHp:actorHp,actorMax:actorMax,hp:hp,maxHp:maxHp)));
        Assert.Equal(actorTerm,score.Contributions[0].HpTerm);Assert.Equal(expected,score.Priority);
    }

    [Theory] [InlineData(0,100)] [InlineData(50,50)] [InlineData(99,1)] [InlineData(100,0)]
    public void Hit_percent_is_independent_and_zero_accuracy_is_not_a_zero_score(int evade,int percent)
    {
        var result=Evaluate(State(evade:evade));
        if(percent==0)
        {
            Assert.Equal(SwordAiScoringDisposition.NotValuable,result.Disposition);Assert.Null(result.Score);
            Assert.Equal(SwordAiCandidateDisposition.NotValuable,result.Projection!.Disposition);
            Assert.Equal(new FftRandomState(3295386429,11),result.RngState);
        }
        else
        {
            var score=Scored(result);Assert.Equal((64,percent),((int)score.Priority,(int)score.HitPercent));
            Assert.Equal(new FftRandomState(4182499122,12),result.RngState);
            Assert.Equal(31051u,Assert.Single(result.Projection!.Outcome!.UpkeepPicks).RawRoll);
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Original_TieKey_order_controls_running_priority_including_enemy_first(bool reverse)
    {
        var state=State(slot:1,reverse:reverse);var score=Scored(Evaluate(state));
        Assert.Equal(new[]{B,A},score.Contributions.Select(u=>u.UnitId));
        Assert.Equal(new[]{true,false},score.Contributions.Select(u=>u.Enemy));
        Assert.Equal(new short[]{-421,64},score.Contributions.Select(u=>u.RunningPriority));
        Assert.Equal(64,score.Priority);
    }

    [Theory] [InlineData(SwordAiCandidateOrder.MoveThenAct)] [InlineData(SwordAiCandidateOrder.ActThenMove)]
    public void Movement_and_clock_choices_are_forwarded_but_not_direct_score_terms(SwordAiCandidateOrder order)
    {
        var state=State();var candidate=new SwordAiCandidate(A,B,new(2,3,0),order);
        // Both origins are adjacent to enemy(1,2) only for Act-first; moved origin uses enemy(1,3).
        if(order==SwordAiCandidateOrder.MoveThenAct)state=State(enemy:new(1,3,0));
        var result=Evaluate(state,candidate:candidate);Assert.Equal(64,Scored(result).Priority);
        var expected=SwordAiCandidateProjection.Evaluate(state,candidate,new(Weapon,5),CandidateProfile,Decision);
        Assert.Equal(Serialize(expected),Serialize(result.Projection));Assert.Equal(expected.RngState,result.RngState);
        Assert.Equal(BattleFacing.West,result.Projection!.Outcome!.Units.Single(u=>u.UnitId==A).Facing);
        Assert.Equal(15,result.Projection.Outcome.TurnEndCt);
        var other=Evaluate(State(ct:99,targetCt:254));Assert.Equal(64,Scored(other).Priority);
        Assert.Equal(0ul,other.Projection!.Outcome!.ElapsedTicks);
    }

    [Fact]
    public void Lethal_projection_keeps_dead_equipment_and_101_upkeep_draws_without_rescoring_live_state()
    {
        var state=State(ct:0,targetCt:0,speed:1,targetSpeed:255,enemy:new(4,2,0));var before=Snapshot(state);
        var result=Evaluate(state,power:10,candidate:new(A,B,new(3,2,0),SwordAiCandidateOrder.MoveThenAct));
        var score=Scored(result);var dead=score.Contributions.Single(u=>u.UnitId==B);
        Assert.Equal((0,-192,357,165,-165),(dead.HpTerm,dead.StatusTerm,dead.EquipmentTerm,dead.UnitTotal,dead.SignedContribution));
        Assert.Equal(320,score.Priority);Assert.True(result.Projection!.Outcome!.TargetKnockedOut);
        Assert.Equal(101,result.Projection.Outcome.UpkeepPicks.Length);Assert.Equal(112ul,result.RngState.DrawCount);
        Assert.Equal(100ul,result.Projection.Outcome.ElapsedTicks);Assert.Equal(before,Snapshot(state));
        Assert.Equal(255,state.Combatants.Single(u=>u.UnitId==B).KoCount);
    }

    public static IEnumerable<object[]> Budgets()
    {
        // Projection owns draw-prefix arithmetic; this wrapper forwards failure and exact/spare success.
        foreach (var (evade, power, draws) in new[] { (0, 5, 5), (0, 10, 6), (100, 5, 4) })
        foreach (int remaining in new[] { 0, draws - 1, draws, draws + 1 }) yield return [evade,power,remaining];
    }

    [Theory] [MemberData(nameof(Budgets))]
    public void Projection_budget_boundaries_are_forwarded_without_scoring_draws(int evade,int power,int remaining)
    {
        var state=State(evade:evade);var rng=new FftRandomState(1,ulong.MaxValue-(ulong)remaining);
        int draws=evade==100?4:power==10?6:5;var result=Evaluate(state,power:power,rng:rng);
        if(remaining<draws)
        { Forwarded(result,SwordAiCandidateRejection.RandomDrawCountExhausted,rng); }
        else
        {
            Assert.Equal(evade==100?SwordAiScoringDisposition.NotValuable:SwordAiScoringDisposition.Scored,result.Disposition);
            Assert.Equal(rng.DrawCount+(ulong)draws,result.RngState.DrawCount);
            Assert.Equal(result.Projection!.RngState,result.RngState);
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Tick_failure_rolls_back_and_already_ready_projection_can_score_at_MAX_ticks(bool lethal)
    {
        Forwarded(Evaluate(State(ticks:ulong.MaxValue),power:lethal?10:5),SwordAiCandidateRejection.TickCountExhausted);
        var result=Evaluate(State(ticks:ulong.MaxValue,targetCt:254));
        Assert.Equal(64,Scored(result).Priority);Assert.Equal(ulong.MaxValue,result.Projection!.Outcome!.Ticks);
    }

    public static IEnumerable<object[]> IncompleteFacts()
    {
        foreach(bool second in new[]{false,true})
        foreach(int flags in new[]{0,1,2,4,8,14,13,11,7,31,128,-1})yield return [second,(SwordAiScoreFacts)flags];
    }

    [Theory] [MemberData(nameof(IncompleteFacts))]
    [Trait("Coverage", "CurrentAdmission")]
    public void Missing_unknown_or_unsupported_certificates_reject_before_invalid_projection(bool second,SwordAiScoreFacts facts)
    {
        var p=new SwordAiScoreProfile([new(A,second?SwordAiScoreFacts.ClosedProfile:facts),new(B,second?facts:SwordAiScoreFacts.ClosedProfile)]);
        var rng=new FftRandomState(1,ulong.MaxValue);
        Unsupported(Evaluate(State(hp:0),scoreProfile:p,rng:rng),rng);
        Unsupported(SwordAiCandidateScoring.Evaluate(State(),Stay(),null,CandidateProfile,p,rng),rng);
    }

    [Fact]
    public void Score_profile_identity_and_roster_certificates_must_match_before_projection()
    {
        var c=new BattleUnitId("sample:unit/c");var wrong=new SwordAiScoreProfile([new(A,SwordAiScoreFacts.ClosedProfile),new(c,SwordAiScoreFacts.ClosedProfile)]);
        Unsupported(Evaluate(State(),scoreProfile:wrong));
        var cp=new SwordAiCandidateProfile(new(Ground),new(Ground,A,c),[new(A,255),new(c,19)]);
        Unsupported(SwordAiCandidateScoring.Evaluate(State(),Stay(),new(Weapon,5),cp,ScoreProfile,Decision));
        Assert.Throws<ArgumentNullException>(()=>new SwordAiScoreProfile(null!));
        foreach(var units in new SwordAiScoreUnitCertificate[][] {[],[new(A,SwordAiScoreFacts.ClosedProfile)],
            [new(A,SwordAiScoreFacts.ClosedProfile),new(A,SwordAiScoreFacts.ClosedProfile)],
            [new(A,SwordAiScoreFacts.ClosedProfile),null!], [new(default,SwordAiScoreFacts.ClosedProfile),new(B,SwordAiScoreFacts.ClosedProfile)]})
            Assert.Throws<ArgumentException>(()=>new SwordAiScoreProfile(units));
    }

    [Fact]
    public void Projection_identity_authority_weapon_profile_and_terminal_rejections_retain_their_exact_reason()
    {
        var state=State();var unknown=new BattleUnitId("sample:unit/unknown");
        Forwarded(Evaluate(state,candidate:new(unknown,B,new(2,2,0),SwordAiCandidateOrder.MoveThenAct)),SwordAiCandidateRejection.UnknownActor);
        Forwarded(Evaluate(state,candidate:new(A,unknown,new(2,2,0),SwordAiCandidateOrder.MoveThenAct)),SwordAiCandidateRejection.UnknownTarget);
        Forwarded(Evaluate(State(acted:true)),SwordAiCandidateRejection.UnavailableAction);
        Forwarded(Evaluate(State(hp:0)),SwordAiCandidateRejection.TerminalInput);
        Forwarded(Evaluate(State(geometry:"cost-two")),SwordAiCandidateRejection.UnsupportedProfile);
        Forwarded(Evaluate(State(enemy:new(4,4,0))),SwordAiCandidateRejection.OutOfRange);
        Forwarded(SwordAiCandidateScoring.Evaluate(state,Stay(),null,CandidateProfile,ScoreProfile,Decision),SwordAiCandidateRejection.UnsupportedProfile);
        Forwarded(Evaluate(state,candidate:new(A,B,new(-1,2,0),SwordAiCandidateOrder.MoveThenAct)),SwordAiCandidateRejection.InvalidCandidate);
        Forwarded(Evaluate(state,candidate:new(A,B,new(2,2,0),(SwordAiCandidateOrder)99)),SwordAiCandidateRejection.InvalidCandidate);
    }

    [Fact]
    public void Null_arguments_are_programmer_errors()
    {
        var state=State();
        Assert.Throws<ArgumentNullException>(()=>SwordAiCandidateScoring.Evaluate(null!,Stay(),new(Weapon,5),CandidateProfile,ScoreProfile,Decision));
        Assert.Throws<ArgumentNullException>(()=>SwordAiCandidateScoring.Evaluate(state,null!,new(Weapon,5),CandidateProfile,ScoreProfile,Decision));
        Assert.Throws<ArgumentNullException>(()=>SwordAiCandidateScoring.Evaluate(state,Stay(),new(Weapon,5),null!,ScoreProfile,Decision));
        Assert.Throws<ArgumentNullException>(()=>SwordAiCandidateScoring.Evaluate(state,Stay(),new(Weapon,5),CandidateProfile,null!,Decision));
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Nonzero_MP_is_currently_unsupported()
    {
        Assert.Throws<NotSupportedException>(()=>State(mp:1));Assert.Throws<NotSupportedException>(()=>State(maxMp:1));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    [Trait("Contract", "QueryIsolation")]
    public void Replay_carry_and_independent_branches_preserve_live_state_and_input_profile(bool noAccuracy)
    {
        var state=State(evade:noAccuracy?100:0);var before=Snapshot(state);
        var result=Evaluate(state);var branch=Evaluate(state,rng:result.RngState);
        for(int i=0;i<4;i++)Assert.Equal(Serialize(result),Serialize(Evaluate(state)));
        Assert.NotEqual(result.RngState,branch.RngState);Assert.Equal(result.RngState.DrawCount+(noAccuracy?4ul:5ul),branch.RngState.DrawCount);
        Assert.Equal(before,Snapshot(state));Assert.Equal(new FftRandomState(57,100),state.TurnBattle.Turns.RngState);
        var list=new List<SwordAiScoreUnitCertificate>{new(A,SwordAiScoreFacts.ClosedProfile),new(B,SwordAiScoreFacts.ClosedProfile)};
        var profile=new SwordAiScoreProfile(list);list[0]=new(A,SwordAiScoreFacts.None);
        Assert.Equal(SwordAiScoreFacts.ClosedProfile,profile.Units[0].Facts);
        Assert.Equal(Serialize(result),Serialize(Evaluate(state,scoreProfile:profile)));
    }

    private static JsonDocument Contract()=>JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","ai-score-contract-v1.json")));
    private static SwordAiCandidate Stay(bool second=false)=>new(second?B:A,second?A:B,new(2,2,0),SwordAiCandidateOrder.MoveThenAct);
    private static SwordAiScoringResult Evaluate(AttackBattleSnapshot state,bool second=false,int power=5,SwordAiCandidate? candidate=null,
        SwordAiScoreProfile? scoreProfile=null,FftRandomState? rng=null)=>SwordAiCandidateScoring.Evaluate(state,candidate??Stay(second),
            new(Weapon,power),CandidateProfile,scoreProfile??ScoreProfile,rng??Decision);
    private static SwordAiCandidateScore Scored(SwordAiScoringResult result)
    { Assert.Equal(SwordAiScoringDisposition.Scored,result.Disposition);Assert.Equal(SwordAiScoringRejection.None,result.Rejection);
        Assert.Equal(SwordAiCandidateDisposition.Projected,result.Projection!.Disposition);Assert.Equal(result.Projection.RngState,result.RngState);
        return Assert.IsType<SwordAiCandidateScore>(result.Score); }
    private static void Unsupported(SwordAiScoringResult result,FftRandomState? rng=null)
    { Assert.Equal(SwordAiScoringDisposition.Rejected,result.Disposition);Assert.Equal(SwordAiScoringRejection.UnsupportedScoreProfile,result.Rejection);
        Assert.Null(result.Score);Assert.Null(result.Projection);Assert.Equal(rng??Decision,result.RngState); }
    private static void Forwarded(SwordAiScoringResult result,SwordAiCandidateRejection reason,FftRandomState? rng=null)
    { Assert.Equal(SwordAiScoringDisposition.Rejected,result.Disposition);Assert.Equal(SwordAiScoringRejection.ProjectionRejected,result.Rejection);
        Assert.Null(result.Score);Assert.Equal(reason,result.Projection!.Rejection);Assert.Equal(rng??Decision,result.RngState);Assert.Equal(result.Projection.RngState,result.RngState); }
    private static void AssertContributions(SwordAiCandidateScore score,JsonElement rows)
    {
        foreach(var e in rows.EnumerateArray())
        {
            var actual=score.Contributions.Single(u=>u.TieKey==e.GetProperty("slot").GetInt32());
            Assert.Equal(e.GetProperty("hpTerm").GetInt32(),actual.HpTerm);Assert.Equal(e.GetProperty("statusTerm").GetInt32(),actual.StatusTerm);
            Assert.Equal(e.GetProperty("equipmentTerm").GetInt32(),actual.EquipmentTerm);Assert.Equal(e.GetProperty("unitTotal").GetInt32(),actual.UnitTotal);
            Assert.Equal(e.GetProperty("signedContribution").GetInt32(),actual.SignedContribution);Assert.Equal(e.GetProperty("storedPriority").GetInt32(),actual.RunningPriority);
        }
    }
    private static string Serialize(object? value)=>JsonSerializer.Serialize(value);
    private static string Snapshot(AttackBattleSnapshot s)=>Serialize(new {s.Combatants,s.TurnBattle.Turns,s.TurnBattle.Battle.Map,s.TurnBattle.Battle.RngState,
        Units=s.TurnBattle.Battle.Units.Select(u=>new {u.Id,u.Position,u.HasMoved,u.MovementProfile.Move,u.MovementProfile.Jump,
            u.MovementProfile.BodyHeight,u.MovementProfile.Mode,u.MovementProfile.Traits,Costs=u.MovementProfile.TerrainCosts.Select(c=>new {Id=c.Key.Value,c.Value})})});
    private static AttackBattleSnapshot State(bool second=false,bool reverse=false,int actorHp=50,int actorMax=50,int hp=50,int maxHp=50,int pa=5,
        int evade=0,int slot=0,int ct=15,int targetCt=95,int speed=5,int targetSpeed=5,bool acted=false,ulong ticks=20,
        TilePosition? enemy=null,string? geometry=null,int mp=0,int maxMp=0)
    {
        var actor=second?B:A;var target=second?A:B;var rng=new FftRandomState(57,100);
        var move=new UnitMovementProfile(4,0,6,UnitMovementMode.Normal,UnitMovementTraits.None,
            [KeyValuePair.Create(Ground,new TerrainMovementCost(geometry=="cost-two"?2:1))]);
        var bodies=new BattleUnitState[]{new(actor,new(2,2,0),move),new(target,enemy??new(1,2,0),move,true)};
        var turns=new TurnUnitState[]{new(actor,speed,ct,slot,true,false,acted),new(target,targetSpeed,targetCt,1-slot,false,true,true)};
        AttackUnitState Combat(BattleUnitId id,int current,int maximum,int evadeValue)=>new(id,current,maximum,mp,maxMp,pa,evadeValue,0,0,0,70,0,
            id==actor?BattleFacing.West:BattleFacing.East,current==0?AttackUnitStatus.Dead:current<=maximum/5?AttackUnitStatus.Critical:AttackUnitStatus.None,255,Weapon);
        var combat=new[]{Combat(actor,actorHp,actorMax,0),Combat(target,hp,maxHp,evade)};
        return new(new(new(new(5,5,1,Enumerable.Repeat(new BattleTile(Ground,10),25).ToArray()),reverse?bodies.Reverse():bodies,rng),
            new(reverse?turns.Reverse():turns,rng,ticks,TurnCyclePhase.UnitTurn)),reverse?combat.Reverse():combat);
    }
}
