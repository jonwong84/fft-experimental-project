using System.Collections.Immutable;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai.Tests;

public sealed class OrdinarySpellAiCandidateTests
{
    private static readonly BattleUnitId Mage=new("mage"),Guard=new("guard");
    private static OrdinarySpellAiCandidateRequest Request(bool follow=true)=>new(Mage,0,new(4,2,0),follow);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 4)]
    [InlineData(131, 3)]
    public void Candidate_and_live_cast_share_preparation_without_mutating_the_candidate_input(byte raw, byte expected)
    {
        var (_, catalog, state) = OrdinarySpellAiReadyTests.Setup(charge: raw);
        string before = OrdinarySpellAiReadyTests.Receipt(state);
        var candidate = OrdinarySpellAiCandidateQuery.Evaluate(state, catalog, Request(), new(57, 0));
        var live = OrdinaryMagicBattle.Apply(state,
            new OrdinaryMagicCommand.Cast(Mage, new("test:spell/fire"), new(4, 2, 0)));
        Assert.True(candidate.Accepted, candidate.Rejection);
        Assert.True(live.Accepted, live.Rejection);
        var queued = Assert.Single(live.Events.OfType<OrdinaryMagicEvent.Queued>());
        Assert.Equal(expected, queued.ChargeTicks);
        Assert.Equal(queued.ChargeTicks, candidate.Outcome!.ActualChargeTime);
        if (expected != 0)
        {
            Assert.Equal(expected, live.State.Pending[Mage].Counter);
            Assert.True(live.State.Units.Single(u => u.Id == Mage).Current.Statuses.Charging);
            Assert.Equal((ushort)75, live.State.Units.Single(u => u.Id == Mage).Current.Mp);
        }
        Assert.False(candidate.Outcome.Units.Single(u => u.Id == Mage).Current.Statuses.Charging);
        Assert.Equal((ushort)69, candidate.Outcome.Units.Single(u => u.Id == Mage).Current.Mp);
        Assert.Equal(before, OrdinarySpellAiReadyTests.Receipt(state));
        var replay = OrdinarySpellAiCandidateQuery.Evaluate(state, catalog, Request(), new(57, 0));
        Assert.Equal(candidate.RngState, replay.RngState);
        Assert.Equal(candidate.Outcome.Score!.Priority, replay.Outcome!.Score!.Priority);
        Assert.Equal(candidate.Outcome.Score.HitPercent, replay.Outcome.Score.HitPercent);
        Assert.Equal(candidate.Outcome.Score.Contributions.ToArray(), replay.Outcome.Score.Contributions.ToArray());
        Assert.Equal(candidate.Outcome.Units.ToArray(), replay.Outcome.Units.ToArray());
    }

    [Fact]
    public void Delayed_fire_derives_clock_opportunities_before_real_formula_work_without_live_changes()
    {
        var (_,catalog,state)=OrdinarySpellAiReadyTests.Setup();string before=OrdinarySpellAiReadyTests.Receipt(state);
        var r=OrdinarySpellAiCandidateQuery.Evaluate(state,catalog,Request(),new(57,9));
        Assert.True(r.Accepted,r.Rejection);var o=r.Outcome!;
        Assert.Equal(OrdinarySpellAiCandidateDisposition.Scored,r.Disposition);Assert.Equal((short)49,o.Score!.Priority);
        Assert.Equal((byte)4,o.ActualChargeTime);Assert.Equal(13UL,o.Clock!.Ticks);
        Assert.True(o.Clock.HadMoveOpportunity.SetEquals([Guard]));Assert.Equal(10UL,o.FormulaEntryRng!.Value.DrawCount);
        Assert.Equal(14UL,r.RngState.DrawCount);Assert.Equal((ushort)146,o.Units[1].Vitals.Hp);
        Assert.Equal((ushort)69,o.Units[0].Current.Mp);Assert.False(o.Units[0].Current.Statuses.Charging);
        Assert.Equal(before,OrdinarySpellAiReadyTests.Receipt(state));
    }

    [Theory]
    [InlineData(3,true,12,4)] [InlineData(4,false,13,5)] [InlineData(10,true,19,6)]
    public void Source_deadlines_determine_tile_value_and_actor_turn_without_wait_refund(byte charge,bool valuable,ulong tick,ulong draws)
    {
        var (_,catalog,state)=OrdinarySpellAiReadyTests.Setup(charge:charge);
        var r=OrdinarySpellAiCandidateQuery.Evaluate(state,catalog,Request(follow:charge==10),new(57,0));
        Assert.True(r.Accepted,r.Rejection);Assert.Equal(tick,r.Outcome!.Clock!.Ticks);Assert.Equal(draws,r.RngState.DrawCount);
        Assert.Equal(valuable,r.Outcome.Score is not null);
        if(!valuable){Assert.Equal((ushort)200,r.Outcome.Units[1].Vitals.Hp);Assert.Equal((ushort)75,r.Outcome.Units[0].Current.Mp);}
        if(charge==10){Assert.Equal(10,r.Outcome.Clock.Units[0].Ct);Assert.False(r.Outcome.Clock.Units[0].ActionTaken);Assert.Contains(Mage,r.Outcome.Clock.HadMoveOpportunity);}
    }

    [Fact]
    public void Immediate_formula_precedes_the_required_clock_event()
    {
        var (_,catalog,state)=OrdinarySpellAiReadyTests.Setup(charge:0);
        var r=OrdinarySpellAiCandidateQuery.Evaluate(state,catalog,Request(false),new(57,0));
        Assert.True(r.Accepted,r.Rejection);Assert.Equal((short)49,r.Outcome!.Score!.Priority);
        Assert.Equal(0UL,r.Outcome.FormulaEntryRng!.Value.DrawCount);Assert.Equal(5UL,r.RngState.DrawCount);
        uint seed=57;for(int i=0;i<5;i++)seed=unchecked(seed*1103515245+12345);
        Assert.Equal((seed>>16)&32767,Assert.Single(r.Outcome.Clock!.UpkeepPicks).RawRoll);
        Assert.Equal((ushort)146,r.Outcome.Units[1].Vitals.Hp); // A later move opportunity does not undo an immediate hit.
    }

    [Fact]
    public void Immediate_ko_peer_consumes_upkeep_but_never_gets_an_ai_turn_or_reward_pass()
    {
        var (_,catalog,state)=OrdinarySpellAiReadyTests.Setup(charge:0,targetHp:30);
        var r=OrdinarySpellAiCandidateQuery.Evaluate(state,catalog,Request(),new(57,0));
        Assert.True(r.Accepted,r.Rejection);Assert.Equal((short)334,r.Outcome!.Score!.Priority);
        Assert.True(r.Outcome.Units[1].Current.Statuses.Dead);Assert.Equal(18UL,r.Outcome.Clock!.Ticks);
        Assert.Equal(2,r.Outcome.Clock.UpkeepPicks.Length);Assert.True(r.Outcome.Clock.UpkeepPicks[0].Dead);
        Assert.True(r.Outcome.Clock.HadMoveOpportunity.SetEquals([Mage]));Assert.Equal((byte)95,state.Units[0].Experience);
        Assert.Equal(6UL,r.RngState.DrawCount);
    }

    [Theory]
    [InlineData(0)] [InlineData(4)]
    public void Healing_runs_only_upkeep_rng_and_clears_critical_privately(byte charge)
    {
        var (_,catalog,state)=OrdinarySpellAiReadyTests.Setup(charge:charge,heal:true);
        var r=OrdinarySpellAiCandidateQuery.Evaluate(state,catalog,Request(),new(57,0));
        Assert.True(r.Accepted,r.Rejection);Assert.Equal((ushort)84,r.Outcome!.Units[1].Vitals.Hp);
        Assert.False(r.Outcome.Units[1].Vitals.Critical);Assert.Equal(1UL,r.RngState.DrawCount);
    }

    [Fact]
    public void Already_used_movement_gets_no_extra_ct_refund()
    {
        var (_,catalog,state)=OrdinarySpellAiReadyTests.Setup();
        var moved=OrdinaryMagicBattle.Apply(state,new OrdinaryMagicCommand.Move(Mage,new(1,2,0)));
        Assert.True(moved.Accepted,moved.Rejection);
        var r=OrdinarySpellAiCandidateQuery.Evaluate(moved.State,catalog,Request(),new(57,0));
        Assert.True(r.Accepted,r.Rejection);Assert.Equal(30,r.Outcome!.Clock!.Units[0].Ct);
        Assert.Equal(new TilePosition(1,2,0),r.Outcome.Units[0].Occupant.Position);
    }

    [Fact]
    public void Actual_charge_masks_high_bit_instead_of_using_the_considered_loader_delay()
    {
        var (_,catalog,state)=OrdinarySpellAiReadyTests.Setup(charge:131);
        var r=OrdinarySpellAiCandidateQuery.Evaluate(state,catalog,Request(false),new(57,0));
        Assert.True(r.Accepted,r.Rejection);Assert.Equal((byte)131,r.Outcome!.Ability.ChargeTime);
        Assert.Equal((byte)3,r.Outcome.ActualChargeTime);Assert.Equal(12UL,r.Outcome.Clock!.Ticks);
        Assert.Equal(4UL,r.RngState.DrawCount);
    }

    [Fact]
    public void Event_limit_scores_unresolved_charging_state_with_source_hit255_and_no_mp_spend()
    {
        var (catalog,state,panels)=CrowdedRoster(16);
        var before=OrdinarySpellAiReadyTests.Receipt(state);
        var r=OrdinarySpellAiCandidateQuery.Evaluate(state,catalog,new(Mage,0,panels[0]),new(57,0));
        Assert.Equal(OrdinarySpellAiCandidateDisposition.ScoredUnresolved,r.Disposition);
        Assert.Equal((short)-6774,r.Outcome!.Score!.Priority);Assert.Equal((byte)255,r.Outcome.Score.HitPercent);
        Assert.True(r.Outcome.Clock!.EventLimitReached);Assert.Equal((byte)94,r.Outcome.Clock.RemainingCharge);
        Assert.Equal(43UL,r.Outcome.Clock.Ticks);
        Assert.Equal(255,r.Outcome.Clock.Events.Length);Assert.Equal(255UL,r.RngState.DrawCount);
        Assert.Null(r.Outcome.FormulaEntryRng);Assert.Empty(r.Outcome.Frames);
        Assert.True(r.Outcome.Units[0].Current.Statuses.Charging);Assert.Equal((ushort)75,r.Outcome.Units[0].Current.Mp);
        Assert.Equal(before,OrdinarySpellAiReadyTests.Receipt(state));
    }

    [Theory]
    [InlineData(17)]
    [InlineData(21)]
    public void Unrepresentable_active_rosters_reject_all_candidate_entry_points_without_rng_or_state_changes(int count)
    {
        var (catalog,state,panels)=CrowdedRoster(count);
        var before=OrdinarySpellAiReadyTests.Receipt(state);
        var rng=new RandomState(57,9);
        var candidate=OrdinarySpellAiCandidateQuery.Evaluate(state,catalog,new(Mage,0,panels[0]),rng);
        var ready=OrdinarySpellAiReadyQuery.Evaluate(state,catalog,new(Mage,0,panels[0],ImmutableHashSet<BattleUnitId>.Empty),rng);
        var search=OrdinarySpellAiSearchQuery.Evaluate(state,catalog,new(Mage,0),rng);
        var aims=OrdinarySpellAiAimQuery.Evaluate(state,catalog,new(Mage,0));
        Assert.False(candidate.Accepted);Assert.Null(candidate.Outcome);Assert.Equal(rng,candidate.RngState);
        Assert.False(ready.Accepted);Assert.Null(ready.Outcome);Assert.Equal(rng,ready.RngState);
        Assert.False(search.Accepted);Assert.Null(search.Outcome);Assert.Equal(rng,search.RngState);
        Assert.False(aims.Accepted);Assert.Null(aims.Outcome);
        Assert.Equal(before,OrdinarySpellAiReadyTests.Receipt(state));
    }

    [Fact]
    public void Sixteen_active_units_can_include_the_last_battle_slot()
    {
        var (catalog,state,panels)=CrowdedRoster(16,lastSourceOrder:20);
        var r=OrdinarySpellAiCandidateQuery.Evaluate(state,catalog,new(Mage,0,panels[0]),new(57,0));
        Assert.True(r.Accepted,r.Rejection);
        Assert.Equal((short)-6774,r.Outcome!.Score!.Priority);
        Assert.Equal(20,state.Units[^1].Occupant.SourceOrder);
    }

    private static (OrdinarySpellAiCatalog Catalog, OrdinaryMagicBattleState State, TilePosition[] Panels)
        CrowdedRoster(int count, int? lastSourceOrder=null)
    {
        var x=OrdinarySpellAiReadyTests.Setup(charge:127);var d=x.Content.Definition;
        var mage=d.Units[0] with {BaseAttributes=d.Units[0].BaseAttributes with {Speed=1},RawStats=d.Units[0].RawStats with {Speed=16384}};
        var panels=Enumerable.Range(0,35).Select(i=>new TilePosition(i%7,i/7,0)).Where(p=>p!=mage.Occupant.Position).Take(20).ToArray();
        var peers=Enumerable.Range(1,count-1).Select(i=>d.Units[1] with {
            Occupant=d.Units[1].Occupant with {UnitId=new("peer"+i),SourceOrder=i==count-1?lastSourceOrder??i:i,Position=panels[i-1]},
            Current=d.Units[1].Current with {UnitId=new("peer"+i)},BaseAttributes=d.Units[1].BaseAttributes with {Speed=50},
            RawStats=d.Units[1].RawStats with {Speed=819200}});
        var def=new OrdinaryMagicBattleDefinition(d.Map,new[]{mage}.Concat(peers),d.Spells.Values,d.Jobs,d.JobThresholds,d.InitialRng,d.MovementGround,d.Learning);
        var catalog=new OrdinarySpellAiCatalog(def,x.Catalog.PrimaryByJob);var start=def.Start();
        var state=start.With(turns:new(start.Turns.Units.Select(u=>new TurnUnitState(u.Id,u.Speed,u.Id==Mage?0:99,u.TieKey,u.Id==Mage)),new(57,0),10,TurnCyclePhase.UnitTurn),phase:OrdinaryMagicClockPhase.UnitTurn);
        return (catalog,state,panels);
    }

    [Fact]
    public void Late_formula_and_clock_budget_failures_restore_the_whole_entry()
    {
        var (_,catalog,state)=OrdinarySpellAiReadyTests.Setup();string before=OrdinarySpellAiReadyTests.Receipt(state);
        var rng=new RandomState(57,ulong.MaxValue-4);
        var r=OrdinarySpellAiCandidateQuery.Evaluate(state,catalog,Request(),rng);
        Assert.False(r.Accepted);Assert.Null(r.Outcome);Assert.Equal(rng,r.RngState);Assert.Equal(before,OrdinarySpellAiReadyTests.Receipt(state));
        var max=state.With(turns:new(state.Turns.Units,state.Turns.RngState,ulong.MaxValue,state.Turns.Phase));
        Assert.False(OrdinarySpellAiCandidateQuery.Evaluate(max,catalog,Request(),new(57,0)).Accepted);
    }

    [Fact]
    public void Unrelated_pending_cast_and_foreign_definition_are_rejected_without_rng()
    {
        var (_,catalog,state)=OrdinarySpellAiReadyTests.Setup();
        var queued=OrdinaryMagicBattle.Apply(state,new OrdinaryMagicCommand.Cast(Mage,new("test:spell/fire"),new(4,2,0))).State;
        Assert.False(OrdinarySpellAiCandidateQuery.Evaluate(queued,catalog,Request(),new(57,0)).Accepted);
        Assert.False(OrdinarySpellAiCandidateQuery.Evaluate(state,OrdinarySpellAiReadyTests.Setup().Catalog,Request(),new(57,0)).Accepted);
    }
}
