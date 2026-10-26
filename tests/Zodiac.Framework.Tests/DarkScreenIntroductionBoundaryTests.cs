using System.Reflection;
using Zodiac.Framework.Events;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed partial class DarkScreenIntroductionContractTests
{
    [Theory]
    [InlineData(-1,0)] [InlineData(256,0)] [InlineData(0,-1)] [InlineData(0,256)]
    public void Constructor_keeps_both_holds_in_unsigned_byte_domain(int general,int ready)
        => Assert.Throws<ArgumentException>(()=>Program(general,ready));

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Constructor_requires_one_shared_owner(int role)
    {
        var p=Program();
        Assert.Throws<ArgumentException>(()=>new DarkScreenIntroductionProgram(p.Id,
            role==0?p.Dark with {OwnerId=Other}:p.Dark,
            role==1?p.General with {OwnerId=Other}:p.General,
            role==2?p.Ready with {OwnerId=Other}:p.Ready));
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void Other_conditions_are_not_admitted(bool general)
    {
        var p=Program();
        Assert.Throws<ArgumentException>(()=>new DarkScreenIntroductionProgram(p.Id,p.Dark,
            general?p.General with {Definition=p.General.Definition with {Condition=9}}:p.General,
            general?p.Ready:p.Ready with {Definition=p.Ready.Definition with {Condition=1}}));
    }
    [Fact]
    public void Resource_definition_program_and_owner_collisions_reject()
    {
        var p=Program();
        Assert.Throws<ArgumentException>(()=>new DarkScreenIntroductionProgram(p.Id,p.Dark,p.General,p.Ready with {ArtworkId=p.General.ArtworkId}));
        Assert.Throws<ArgumentException>(()=>new DarkScreenIntroductionProgram(p.Id,p.Dark,p.General with {Definition=p.General.Definition with {Id=p.Dark.Definition.Id}},p.Ready));
        Assert.Throws<ArgumentException>(()=>new DarkScreenIntroductionProgram(Owner,p.Dark,p.General,p.Ready));
    }
    [Theory]
    [InlineData(256)] [InlineData(257)]
    public void Pure_program_ID_bound_is_explicit_and_independent_of_future_descriptor_suffix_limit(int length)
    {
        var p=Program();var id=new AssetId("sample:"+new string('a',length-7));
        if(length==256)Assert.NotNull(new DarkScreenIntroductionProgram(id,p.Dark,p.General,p.Ready));
        else Assert.Throws<ArgumentException>(()=>new DarkScreenIntroductionProgram(id,p.Dark,p.General,p.Ready));
    }
    [Fact]
    public void Default_and_null_definitions_or_identifiers_do_not_create_a_program()
    {
        var p=Program();
        Assert.Throws<ArgumentException>(()=>new DarkScreenIntroductionProgram(default,p.Dark,p.General,p.Ready));
        Assert.Throws<ArgumentException>(()=>new DarkScreenIntroductionProgram(p.Id,p.Dark with {Definition=null!},p.General,p.Ready));
        Assert.Throws<ArgumentException>(()=>new DarkScreenIntroductionProgram(p.Id,p.Dark,p.General with {Definition=null!},p.Ready));
        Assert.Throws<ArgumentException>(()=>new DarkScreenIntroductionProgram(p.Id,p.Dark,p.General,p.Ready with {Definition=null!}));
        Assert.Throws<ArgumentNullException>(()=>new DarkScreenIntroductionProgram(p.Id,null!,p.General,p.Ready));
        Assert.Throws<ArgumentNullException>(()=>DarkScreenIntroductionExecutor.AdvanceTick(null!,false));
        Assert.Throws<ArgumentException>(()=>p.Start(default,new("sample:consumer/test"),Binding(p)));
        Assert.Throws<ArgumentException>(()=>p.Start(Scene,default,Binding(p)));
    }
    [Theory]
    [InlineData(0,60)] [InlineData(60,0)] [InlineData(1,1)] [InlineData(60,60)]
    public void Every_natural_snapshot_matches_independent_cell_display_and_barrier_equations(int general,int ready)
        =>NaturalTimeline(general,ready);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(7)] [InlineData(41)]
    public void Valid_authored_delay_maxima_do_not_turn_into_a_hardcoded_animation_timer(int maximum)
    {
        int birth=maximum+15,end=73+2*maximum+3+4;
        var s=Through(Start(3,4,maximum),birth-1);Assert.Equal(DarkScreenPhase.Holding,s.Dark!.Phase);Assert.Null(s.General);
        s=Through(s,birth);Assert.Equal(birth,s.General!.StartTick);
        Terminal(Through(s,end),end);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(41)] [InlineData(56)] [InlineData(149)] [InlineData(220)]
    public void Pending_parent_allows_worker_progress_without_instruction_or_update_budget(int tick)
    {
        var s=Through(Start(),tick);var r=DarkScreenIntroductionExecutor.AdvanceTick(s,false,0,0);
        Assert.Equal(tick+1,Accept(r).Event.Tick);Assert.Equal(0,r.InstructionsExecuted);Assert.Equal(0,r.AcceptedTaskUpdates);
    }
    [Theory]
    [InlineData(54)] [InlineData(146)] [InlineData(216)] [InlineData(273)]
    public void Late_worker_publication_failure_preserves_entire_snapshot_and_retry_matches_clean_execution(int tick)
    {
        var s=Through(Start(),tick);
        var result=Reject(s,()=>DarkScreenIntroductionExecutor.AdvanceTick(s,false,0,0),DarkScreenIntroductionRejection.TaskLedgerRejected);
        Assert.Equal(SceneTaskRejection.UpdateBudgetExceeded,result.TaskRejection);
        Assert.Equal(Json(DarkScreenIntroductionExecutor.AdvanceTick(s,false)),Json(DarkScreenIntroductionExecutor.AdvanceTick(s,false,0,1)));
    }
    [Theory]
    [InlineData(55)] [InlineData(147)] [InlineData(217)] [InlineData(274)]
    public void Parent_pump_budget_failures_roll_back_released_barrier_and_any_candidate_admission(int tick)
    {
        var s=Through(Start(),tick);int budget=tick==274?0:1;
        Reject(s,()=>DarkScreenIntroductionExecutor.AdvanceTick(s,true,budget,1),DarkScreenIntroductionRejection.InstructionBudgetExceeded);
        var retried=Accept(DarkScreenIntroductionExecutor.AdvanceTick(s,true));Assert.Equal(tick+1,retried.Event.Tick);
    }
    [Theory]
    [InlineData(0)] [InlineData(55)] [InlineData(147)] [InlineData(208)] [InlineData(217)] [InlineData(218)]
    public void Unconsumed_circle_samples_do_not_appear_in_state_or_trace(int tick)
    {
        var s=Through(Start(),tick);
        Assert.Equal(Json(DarkScreenIntroductionExecutor.AdvanceTick(s,false)),Json(DarkScreenIntroductionExecutor.AdvanceTick(s,true)));
    }
    [Fact]
    public void Event_then_dark_then_dispatcher_then_child_trace_order_is_explicit()
    {
        var s=Through(Start(),207);var r=DarkScreenIntroductionExecutor.AdvanceTick(s,true);Accept(r);
        var kinds=r.Trace.Select(t=>t.Kind).ToArray();int dark=Array.IndexOf(kinds,DarkScreenIntroductionTraceKind.DarkAdvance);
        int dispatcher=Array.IndexOf(kinds,DarkScreenIntroductionTraceKind.ReadyDispatcher),child=Array.IndexOf(kinds,DarkScreenIntroductionTraceKind.ReadyChild);
        Assert.True(dark>=0 && dispatcher>dark && child>dispatcher);
        Assert.All(r.Trace.Take(dark),t=>Assert.Equal(DarkScreenIntroductionTraceKind.Event,t.Kind));
        Assert.Null(r.Trace[dark].Circle);Assert.True(r.Trace[dispatcher].Circle);Assert.Null(r.Trace[child].Circle);
    }
    [Theory]
    [InlineData("owner")] [InlineData("definition")] [InlineData("clock")] [InlineData("child")] [InlineData("scale")] [InlineData("release")] [InlineData("cancel")]
    public void Invalid_ready_binding_or_lifecycle_cannot_publish_progress(string change)
    {
        var s=Copy(Through(Start(),150));var ready=Clone(s.Ready!);Set(s,"Ready",ready);
        switch(change)
        {
            case "owner":Set(ready,"OwnerId",Other);break;
            case "definition":Set(ready,"Definition",ready.Definition with {HoldFrames=1});break;
            case "clock":Set(ready,"LastProcessedTick",149L);break;
            case "child":Set(ready,"ChildPhase",ReadyChildPhase.Completed);break;
            case "scale":Set(ready,"DrawScale",999);break;
            case "release":Set(ready,"ReleaseTick",150L);break;
            case "cancel":Set(ready,"DispatcherPhase",ReadyDispatcherPhase.Cancelled);break;
        }
        Reject(s,()=>DarkScreenIntroductionExecutor.AdvanceTick(s,false),DarkScreenIntroductionRejection.InvalidManagedBinding);
    }
    [Theory]
    [InlineData("consumer")] [InlineData("selector")] [InlineData("offset")] [InlineData("sequence")] [InlineData("variables")] [InlineData("inner-program")]
    public void Altered_event_or_ledger_cannot_bypass_the_closed_program(string change)
    {
        var s=Copy(Through(Start(),150));
        switch(change)
        {
            case "consumer":Set(s.Event,"ConsumerId",Other);break;
            case "selector":Set(s.Event.Tasks,"Barrier",s.Event.Tasks.Barrier! with {Selector=SceneTaskClass.Camera});break;
            case "offset":Set(s.Event.Tasks,"Barrier",s.Event.Tasks.Barrier! with {SourceOffset=7});break;
            case "sequence":Set(s.Event.Tasks,"NextSequence",99L);break;
            default:
                var inner=Clone(typeof(SceneEventState).GetProperty("Inner",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(s.Event)!);
                Set(s.Event,"Inner",inner);
                if(change=="variables")Set(inner,"Variables",new ConditionalEventVariables(1,0,0,0));
                else Set(inner,"Program",new ConditionalEventProgram(new("sample:program/foreign"),[new(0,ConditionalEventOperation.Exit)]));
                break;
        }
        Reject(s,()=>DarkScreenIntroductionExecutor.AdvanceTick(s,false),DarkScreenIntroductionRejection.InvalidManagedBinding);
    }
    [Fact]
    public void Terminal_task_facts_do_not_open_gate_before_synthetic_exit()
    {
        var s=Through(Start(),274);Assert.All(s.Event.Tasks.Tasks,t=>Assert.True(t.IsTerminal));Assert.False(s.BattleAvailable);
        var result=DarkScreenIntroductionExecutor.AdvanceTick(s,false,1,0);Assert.True(Accept(result).BattleAvailable);
        Assert.True(result.BattleBecameAvailable);Assert.Equal(1,result.InstructionsExecuted);Assert.Equal(0,result.AcceptedTaskUpdates);
    }
}
