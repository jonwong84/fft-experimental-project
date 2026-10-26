using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Zodiac.Framework.Events;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed partial class DarkScreenIntroductionContractTests
{
    private static readonly AssetId Scene = new("sample:scene/dark-introduction");
    private static readonly AssetId Owner = new("sample:owner/dark-introduction");
    private static readonly AssetId Other = new("sample:owner/other");
    private static DarkScreenDefinition Definition(int maximum = 41) => new(new("sample:definition/dark"), new(),
        Enumerable.Range(0, 288).Select(i => new DarkScreenTileDefinition(i % 24 * 12 - 12,
            i / 24 * 24 - (i % 2 == 0 ? 12 : 24), i == 287 ? maximum : i == 1 && maximum > 0 ? 1 : 0)));
    private static DarkScreenIntroductionProgram Program(int general = 60, int ready = 60, int maximum = 41) =>
        new(new("sample:sequence/dark-introduction"), new(Definition(maximum), Owner),
            new(new(new("sample:definition/general"), 8, general), new("sample:art/general"), Owner),
            new(new(new("sample:definition/ready"), 0, ready), new("sample:art/ready"), Owner));
    private static DarkScreenIntroductionBinding Binding(DarkScreenIntroductionProgram p) => new(
        new(p.Dark.Definition.Id, Owner), new(p.General.Definition.Id, p.General.ArtworkId, Owner),
        new(p.Ready.Definition.Id, p.Ready.ArtworkId, Owner));
    private static DarkScreenIntroductionState Fresh(int general = 60, int ready = 60, int maximum = 41)
    { var p = Program(general, ready, maximum); return p.Start(Scene, new("sample:consumer/dark-introduction"), Binding(p)); }
    private static DarkScreenIntroductionState Accept(DarkScreenIntroductionResult r)
    { Assert.True(r.Accepted, r.Diagnostic); Assert.Null(r.Diagnostic); return r.State; }
    private static DarkScreenIntroductionState Start(int general = 60, int ready = 60, int maximum = 41)
        => Accept(DarkScreenIntroductionExecutor.RunUntilBlocked(Fresh(general, ready, maximum)));
    private static DarkScreenIntroductionState Through(DarkScreenIntroductionState s, long tick, params int[] circle)
    {
        while (s.Event.Tick < tick) s = Accept(DarkScreenIntroductionExecutor.AdvanceTick(s, circle.Contains(checked((int)s.Event.Tick + 1))));
        return s;
    }
    private static string Json(object value) => JsonSerializer.Serialize(value);
    private static DarkScreenIntroductionResult Reject(DarkScreenIntroductionState s, Func<DarkScreenIntroductionResult> command,
        DarkScreenIntroductionRejection? reason = null)
    {
        string before = Json(s); var result = command();
        Assert.False(result.Accepted); Assert.Same(s, result.State); Assert.Equal(before, Json(s));
        Assert.Empty(result.Trace); Assert.False(result.BattleBecameAvailable); Assert.NotEmpty(result.Diagnostic!);
        Assert.Equal((0,0,0), (result.InstructionsExecuted, result.AcceptedTaskUpdates, result.TicksConsumed));
        if (reason is not null) Assert.Equal(reason, result.Rejection);
        return result;
    }
    private static T Clone<T>(T value) where T : class =>
        (T)typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(value, null)!;
    private static void Set(object value, string property, object? field) => value.GetType()
        .GetField("<" + property + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(value, field);
    private static DarkScreenIntroductionState Copy(DarkScreenIntroductionState s)
    {
        var copy = Clone(s); var ev = Clone(s.Event); Set(ev, "Tasks", Clone(s.Event.Tasks)); Set(copy, "Event", ev);
        if (s.Dark is not null) Set(copy, "Dark", Clone(s.Dark));
        return copy;
    }
    private static void Pending(DarkScreenIntroductionState s, int offset, SceneTaskClass selector)
    {
        Assert.False(s.Event.Exited); Assert.False(s.BattleAvailable);
        Assert.Equal(SceneEventSuspension.Barrier, s.Event.Suspension); Assert.Equal(offset, s.Event.WaitingAtOffset);
        Assert.Equal(offset+1, s.Event.Offset); Assert.Equal(selector, s.Event.Tasks.Barrier!.Selector);
        Assert.Equal(SceneBarrierStatus.Pending, s.Event.Tasks.BarrierStatus);
    }
    private static void Terminal(DarkScreenIntroductionState s, long tick)
    {
        Assert.Equal(tick, s.Event.Tick); Assert.True(s.Event.Exited); Assert.True(s.BattleAvailable);
        Assert.True(s.Dark!.IsTerminal); Assert.True(s.General!.IsTerminal); Assert.True(s.Ready!.IsTerminal);
        Assert.Equal(0, s.Event.Tasks.LiveTasks); Assert.Equal(3, s.Event.Tasks.TotalStarted); Assert.Equal(4, s.Event.Tasks.NextSequence);
        Assert.Equal(new long[] {1,2,3}, s.Event.Tasks.Tasks.Select(t => t.Id.Sequence));
        Assert.Equal(new long[] {3,1,1}, s.Event.Tasks.Tasks.Select(t => t.Revision));
        Assert.All(s.Event.Tasks.Tasks, t => { Assert.Equal(Scene, t.Id.SceneRunId); Assert.Equal(Owner, t.OwnerId); Assert.Equal(SceneTaskPhase.Completed, t.Phase); Assert.Null(t.CancellationReason); });
    }

    // Independent cell equations, not calls into the worker's transition implementation.
    private static void Cells(DarkScreenIntroductionState s, int bodyTick, bool leaving, bool flash = false)
    {
        var definition = s.Program.Dark.Definition;
        var expectedDraws = new List<DarkScreenTileDraw>();
        for (int i = 0; i < 288; i++)
        {
            int delay = definition.Tiles[i].InitialDelay;
            int active = Math.Max(0, bodyTick-delay), previous = Math.Max(0, bodyTick-1-delay);
            int angle = leaving ? Math.Max(0,512-active*64) : Math.Min(512,active*64);
            int radius = leaving ? Math.Max(0,48-active*4) : Math.Min(48,active*4);
            Assert.Equal(new DarkScreenTileState(Math.Max(0,delay-bodyTick), angle, radius), s.Dark!.Tiles[i]);
            if (!flash && (leaving || bodyTick > delay)) expectedDraws.Add(new(i,
                leaving ? Math.Max(0,512-previous*64) : Math.Min(512,previous*64),
                leaving ? Math.Max(0,48-previous*4) : Math.Min(48,previous*4)));
        }
        Assert.Equal(flash ? DarkScreenDrawKind.Flash : DarkScreenDrawKind.Tiles, s.Dark!.Draw.Kind);
        Assert.Equal(expectedDraws, s.Dark.Draw.Tiles);
    }
    private static void NaturalTimeline(int g, int q)
    {
        var s = Start(g,q); int release = 98+g+q, end = release+57;
        for (int tick = 1; tick <= end; tick++)
        {
            var result = DarkScreenIntroductionExecutor.AdvanceTick(s, false); s = Accept(result);
            Assert.Equal(tick,s.Event.Tick); Assert.Equal(1,result.TicksConsumed);
            Assert.Single(result.Trace.Where(t => t.Event?.TaskTrace?.Kind == SceneTaskTraceKind.TickAdvanced));
            if (tick <= 55) Cells(s,tick,false);
            if (tick == release) Cells(s,0,true,true);
            if (tick > release && tick <= release+55) Cells(s,tick-release,true);
            if (tick is >= 56 && tick < release) { Assert.Equal(DarkScreenPhase.Holding,s.Dark!.Phase); Assert.Equal(DarkScreenDrawKind.Flash,s.Dark.Draw.Kind); }
            if (tick < 56) Pending(s,1,SceneTaskClass.DarkScreenTransition);
            else if (tick < 88+g) Pending(s,3,SceneTaskClass.ConditionDisplay);
            else if (tick < release) Pending(s,5,SceneTaskClass.ConditionDisplay);
            else if (tick < end) Pending(s,7,SceneTaskClass.DarkScreenTransition);
            Assert.Equal(tick == end,result.BattleBecameAvailable);
            Assert.Equal(tick == end,result.Trace.Any(t => t.Kind == DarkScreenIntroductionTraceKind.BattleAvailable));
            Assert.InRange(s.Event.Tasks.LiveTasks,0,2);
            NaturalDisplays(s,g,q,tick);
        }
        Terminal(s,end);
    }
    private static void NaturalDisplays(DarkScreenIntroductionState s, int g, int q, int tick)
    {
        if (tick < 56) { Assert.Null(s.General); Assert.Null(s.Ready); return; }
        int elapsed=Math.Min(tick-56,g+31);
        var general=s.General!;
        Assert.Equal(Math.Min(20,elapsed),general.RevealCompleted);
        Assert.Equal(Math.Clamp(elapsed-20,0,g),general.HoldCompleted);
        Assert.Equal(Math.Clamp(elapsed-20-g,0,10),general.HideCompleted);
        var generalPhase=elapsed<20?GeneralBannerPhase.Revealing:elapsed<20+g?GeneralBannerPhase.Holding:
            elapsed<30+g?GeneralBannerPhase.Hiding:elapsed==30+g?GeneralBannerPhase.ExitYield:GeneralBannerPhase.Completed;
        Assert.Equal(generalPhase,general.Phase);Assert.Equal(Math.Min(tick,87+g),general.LastProcessedTick);
        int birth=88+g;
        if(tick<birth){Assert.Null(s.Ready);return;}
        var ready=s.Ready!;int age=Math.Min(tick-birth,q+9);int tail=age-q;
        Assert.Equal(Math.Min(age,q),ready.WaitSamplesTaken);
        Assert.Equal(age<q?ReadyDispatcherPhase.WaitingInput:tail<9?ReadyDispatcherPhase.WaitingChild:ReadyDispatcherPhase.Completed,ready.DispatcherPhase);
        Assert.Equal(age<q?(age<10?ReadyChildPhase.FadeIn:ReadyChildPhase.Holding):tail<7?ReadyChildPhase.FadeOut:tail==7?ReadyChildPhase.ExitYield:ReadyChildPhase.Completed,ready.ChildPhase);
        int? scale=age<q?Math.Min(128,48+age*8):tail<7?128-tail*12:null;
        Assert.Equal(scale,ready.DrawScale);Assert.Equal(age<q?0:Math.Min(7,tail+1),ready.FadeOutDraws);
        Assert.Equal(age<q?(long?)null:birth+q,ready.ReleaseTick);Assert.Equal(Math.Min(tick,birth+q+9),ready.LastProcessedTick);
    }

    public static IEnumerable<object[]> CoreCases()
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures","dark-screen-intro-contract-v1.json")));
        Assert.True(doc.RootElement.GetProperty("specificationOnly").GetBoolean()); Assert.False(doc.RootElement.GetProperty("executed").GetBoolean());
        Assert.Equal("11464943bbdbd2a0af022d90cb7f10455cf911df",doc.RootElement.GetProperty("referenceRevision").GetString());
        foreach (var item in doc.RootElement.GetProperty("cases").EnumerateArray().Where(c => c.GetProperty("implementationTicket").GetString() == "ZOD-097"))
        { Assert.False(item.GetProperty("executed").GetBoolean()); yield return [item.GetProperty("id").GetString()!]; }
    }
    [Theory]
    [MemberData(nameof(CoreCases))]
    public void Historical_core_contract_cases_execute(string id)
    {
        switch (id)
        {
            case "DSI-001":
            {
                var p=Program();var b=Binding(p);
                foreach (var binding in new DarkScreenIntroductionBinding?[] {null,b with {Dark=null!},b with {General=null!},b with {Ready=null!},
                    b with {Dark=b.Dark with {DefinitionId=Other}},b with {General=b.General with {ArtworkId=Other}},b with {Ready=b.Ready with {OwnerId=Other}}})
                {
                    var fresh=p.Start(Scene,new("sample:consumer/test"),binding);
                    Reject(fresh,()=>DarkScreenIntroductionExecutor.RunUntilBlocked(fresh),DarkScreenIntroductionRejection.InvalidManagedBinding);
                    Assert.Null(fresh.Dark);Assert.Empty(fresh.Event.Tasks.Tasks);
                }
                var result=DarkScreenIntroductionExecutor.RunUntilBlocked(Fresh(),2,1);var s=Accept(result);
                Assert.Equal((2,1,0),(result.InstructionsExecuted,result.AcceptedTaskUpdates,result.TicksConsumed));
                Assert.Equal(0,s.Event.Tick);Assert.Equal(DarkScreenDrawKind.None,s.Dark!.Draw.Kind);Assert.Null(s.General);Assert.Null(s.Ready);
                Assert.Equal(1,s.Dark.TaskId.Sequence);Pending(s,1,SceneTaskClass.DarkScreenTransition);break;
            }
            case "DSI-002":
            {
                var s=Start();for (int tick=1;tick<=54;tick++) {s=Through(s,tick);Cells(s,tick,false);Assert.Equal(DarkScreenPhase.Entering,s.Dark!.Phase);Assert.Null(s.General);}
                Assert.True(s.Dark!.BodyFinished);Pending(s,1,SceneTaskClass.DarkScreenTransition);break;
            }
            case "DSI-003":
            {
                var s=Through(Start(),55);Assert.Equal(DarkScreenPhase.Holding,s.Dark!.Phase);Assert.Equal(1,s.Event.Tasks.Tasks[0].Revision);
                Assert.Equal(SceneTaskClass.DarkScreenHold,s.Event.Tasks.Tasks[0].ActiveClass);Cells(s,55,false);Assert.Null(s.General);Pending(s,1,SceneTaskClass.DarkScreenTransition);break;
            }
            case "DSI-004":
            {
                var before=Through(Start(),55);var no=DarkScreenIntroductionExecutor.AdvanceTick(before,false);var yes=DarkScreenIntroductionExecutor.AdvanceTick(before,true);
                Assert.Equal(Json(no),Json(yes));var s=Accept(yes);Assert.Equal(56,s.General!.StartTick);Assert.Equal(0,s.General.RevealCompleted);
                Assert.Equal(DarkScreenDrawKind.Flash,s.Dark!.Draw.Kind);Assert.DoesNotContain(yes.Trace,t=>t.Kind==DarkScreenIntroductionTraceKind.GeneralAdvance);
                Pending(s,3,SceneTaskClass.ConditionDisplay);break;
            }
            case "DSI-005":
            {
                var s=Through(Start(),56);while(s.Event.Tick<=147)
                { Assert.Equal(DarkScreenDrawKind.Flash,s.Dark!.Draw.Kind);Assert.Null(s.Ready);Assert.Equal(s.Event.Tick<147?2:1,s.Event.Tasks.LiveTasks);
                    Assert.Equal(new long[]{1,2},s.Event.Tasks.Tasks.Select(t=>t.Id.Sequence));if(s.Event.Tick==147)break;s=Through(s,s.Event.Tick+1); }
                break;
            }
            case "DSI-006":
            {
                var s=Through(Start(),147);Assert.True(s.General!.IsTerminal);Assert.Null(s.Ready);Pending(s,3,SceneTaskClass.ConditionDisplay);
                var no=DarkScreenIntroductionExecutor.AdvanceTick(s,false);var yes=DarkScreenIntroductionExecutor.AdvanceTick(s,true);Assert.Equal(Json(no),Json(yes));s=Accept(yes);
                Assert.Equal(148,s.Ready!.StartTick);Assert.Equal(48,s.Ready.DrawScale);Assert.Equal(0,s.Ready.WaitSamplesTaken);Assert.Equal(3,s.Ready.TaskId.Sequence);
                Assert.DoesNotContain(yes.Trace,t=>t.Kind is DarkScreenIntroductionTraceKind.ReadyDispatcher or DarkScreenIntroductionTraceKind.ReadyChild);break;
            }
            case "DSI-007":
            {
                var s=Through(Start(),208);Assert.Equal(208,s.Ready!.ReleaseTick);Assert.Equal(128,s.Ready.DrawScale);Assert.Equal(60,s.Ready.WaitSamplesTaken);
                s=Through(s,215);Assert.Equal(ReadyChildPhase.ExitYield,s.Ready!.ChildPhase);Assert.Null(s.Ready.DrawScale);
                s=Through(s,216);Assert.True(s.Ready!.ChildIsTerminal);Assert.False(s.Ready.IsTerminal);Assert.False(s.BattleAvailable);
                s=Through(s,217);Assert.True(s.Ready!.IsTerminal);Assert.Equal(DarkScreenPhase.Holding,s.Dark!.Phase);Pending(s,5,SceneTaskClass.ConditionDisplay);break;
            }
            case "DSI-008":
            {
                var r=DarkScreenIntroductionExecutor.AdvanceTick(Through(Start(),217),true);var s=Accept(r);Assert.Equal(DarkScreenPhase.Leaving,s.Dark!.Phase);
                Assert.False(s.Dark.ReleaseYieldPending);Cells(s,0,true,true);Assert.Equal(2,s.Event.Tasks.Tasks[0].Revision);
                Assert.True(Array.FindIndex(r.Trace.ToArray(),t=>t.Kind==DarkScreenIntroductionTraceKind.DarkRelease)<Array.FindIndex(r.Trace.ToArray(),t=>t.Kind==DarkScreenIntroductionTraceKind.DarkAdvance));
                s=Through(s,219);Cells(s,1,true);Pending(s,7,SceneTaskClass.DarkScreenTransition);break;
            }
            case "DSI-009":
            {
                var s=Through(Start(),273);Assert.True(s.Dark!.ExitYield);Cells(s,55,true);Pending(s,7,SceneTaskClass.DarkScreenTransition);
                s=Through(s,274);Assert.True(s.Dark!.IsTerminal);Assert.Equal(DarkScreenDrawKind.None,s.Dark.Draw.Kind);Pending(s,7,SceneTaskClass.DarkScreenTransition);
                var result=DarkScreenIntroductionExecutor.AdvanceTick(s,false);Terminal(Accept(result),275);Assert.True(result.BattleBecameAvailable);Assert.Single(result.Trace.Where(t=>t.Kind==DarkScreenIntroductionTraceKind.BattleAvailable));break;
            }
            case "DSI-010": NaturalTimeline(0,0);break;
            case "DSI-011": NaturalTimeline(255,255);break;
            case "DSI-012":
            {
                int[] circle=[1,55,56,57,59,60];var s=Through(Start(),58,circle);Assert.True(s.General!.IsTerminal);Assert.Null(s.Ready);
                s=Through(s,59,circle);Assert.Equal(48,s.Ready!.DrawScale);Assert.Null(s.Ready.ReleaseTick);
                s=Through(s,60,circle);Assert.Equal(60,s.Ready!.ReleaseTick);Assert.Equal(ReadyExitCause.CircleSkipped,s.Ready.ExitCause);
                s=Through(s,68,circle);Assert.True(s.Ready!.ChildIsTerminal);Assert.False(s.Ready.IsTerminal);
                s=Through(s,69,circle);Assert.True(s.Ready!.IsTerminal);Assert.Equal(DarkScreenPhase.Holding,s.Dark!.Phase);
                s=Through(s,70,circle);Assert.Equal(DarkScreenPhase.Leaving,s.Dark!.Phase);s=Through(s,126,circle);Assert.False(s.BattleAvailable);Terminal(Through(s,127,circle),127);break;
            }
            case "DSI-013":
            {
                var s=Through(Start(),136);Assert.Equal(GeneralBannerPhase.Hiding,s.General!.Phase);
                var no=DarkScreenIntroductionExecutor.AdvanceTick(s,false);var yes=DarkScreenIntroductionExecutor.AdvanceTick(s,true);Assert.Equal(Json(no),Json(yes));Terminal(Through(Accept(yes),275),275);break;
            }
            case "DSI-014": BindingFailures();break;
            case "DSI-015": ClosedSurface();break;
            case "DSI-016":
            {
                var definition=Definition();var program=new DarkScreenProgram(new("sample:program/standalone"),
                    [new(0),new(1,new StartDarkScreen(definition,Owner)),new(2),new(new SceneEventInstruction(3,SceneTaskClass.DarkScreenTransition)),new(new SceneEventInstruction(new ConditionalEventInstruction(4,ConditionalEventOperation.Exit)))]);
                var result=DarkScreenExecutor.RunUntilBlocked(program.StartScene(default,SceneTaskState.Create(Scene),new("sample:consumer/standalone"),new(definition.Id,Owner)),4);
                Assert.True(result.Accepted);Assert.Equal(2,result.Trace.Count(t=>t.Kind==DarkScreenTraceKind.ReleaseNoMatch));var s=result.State;
                for(int tick=1;tick<=100;tick++) {var r=DarkScreenExecutor.AdvanceTick(s,[],8);Assert.True(r.Accepted);s=r.State;}
                Assert.True(s.Event.Exited);Assert.Equal(DarkScreenPhase.Holding,s.Worker!.Phase);break;
            }
            case "DSI-017":
            {
                var fresh=Fresh();Reject(fresh,()=>DarkScreenIntroductionExecutor.RunUntilBlocked(fresh,1,1),DarkScreenIntroductionRejection.InstructionBudgetExceeded);
                var s=Through(Start(),55);Reject(s,()=>DarkScreenIntroductionExecutor.AdvanceTick(s,true,1,1));
                Assert.Equal(Json(DarkScreenIntroductionExecutor.AdvanceTick(s,true)),Json(DarkScreenIntroductionExecutor.AdvanceTick(s,true,2,1)));break;
            }
            case "DSI-018":
            {
                var s=Through(Start(),146);var result=Reject(s,()=>DarkScreenIntroductionExecutor.AdvanceTick(s,false,0,0));Assert.Equal(SceneTaskRejection.UpdateBudgetExceeded,result.TaskRejection);
                Assert.True(Accept(DarkScreenIntroductionExecutor.AdvanceTick(s,false,0,1)).General!.IsTerminal);break;
            }
            case "DSI-019":
            {
                var s=Through(Start(),54);Reject(s,()=>DarkScreenIntroductionExecutor.AdvanceTick(s,false,0,0));
                var after=Accept(DarkScreenIntroductionExecutor.AdvanceTick(s,false,0,1));Assert.Equal(DarkScreenPhase.Holding,after.Dark!.Phase);break;
            }
            case "DSI-020":
            {
                var s=Through(Start(),217);Reject(s,()=>DarkScreenIntroductionExecutor.AdvanceTick(s,false,2,0));
                Assert.Equal(DarkScreenPhase.Leaving,Accept(DarkScreenIntroductionExecutor.AdvanceTick(s,false,2,1)).Dark!.Phase);break;
            }
            case "DSI-021": InvalidCommandsAndOverflow();break;
            case "DSI-022":
            {
                var a=Start(1,1);var b=Start(1,1);
                while(!a.Event.Exited)
                {
                    var first=DarkScreenIntroductionExecutor.AdvanceTick(a,false);_=Json(first.State);_=Json(first.State);
                    var second=DarkScreenIntroductionExecutor.AdvanceTick(b,false);Assert.Equal(Json(first),Json(second));a=Accept(first);b=Accept(second);
                }
                Terminal(a,157);break;
            }
            case "DSI-023":
            {
                var s=Through(Start(),275);Terminal(s,275);var dark=s.Dark;var general=s.General;var ready=s.Ready;
                Reject(s,()=>DarkScreenIntroductionExecutor.AdvanceTick(s,false),DarkScreenIntroductionRejection.AlreadyExited);
                Reject(s,()=>DarkScreenIntroductionExecutor.RunUntilBlocked(s),DarkScreenIntroductionRejection.AlreadyExited);
                Assert.Same(dark,s.Dark);Assert.Same(general,s.General);Assert.Same(ready,s.Ready);break;
            }
            case "DSI-024":
            {
                var s=Start();Reject(s,()=>DarkScreenIntroductionExecutor.RunUntilBlocked(s),DarkScreenIntroductionRejection.AwaitingTick);
                var contaminated=Copy(Through(Start(),100));var successor=Through(Start(),148);
                Set(contaminated,"Ready",successor.Ready);Set(contaminated.Event.Tasks,"Tasks",contaminated.Event.Tasks.Tasks.Add(successor.Event.Tasks.Tasks[2]));Set(contaminated.Event.Tasks,"NextSequence",4L);
                Reject(contaminated,()=>DarkScreenIntroductionExecutor.AdvanceTick(contaminated,false),DarkScreenIntroductionRejection.InvalidManagedBinding);break;
            }
            default: throw new InvalidOperationException("Unmapped core contract case: "+id);
        }
    }

    private static void ClosedSurface()
    {
        var methods=typeof(DarkScreenIntroductionExecutor).GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly);
        Assert.Equal(new[]{"AdvanceTick","RunUntilBlocked"},methods.Select(m=>m.Name).Order());
        Assert.DoesNotContain(methods.SelectMany(m=>m.GetParameters()),p=>p.ParameterType==typeof(ImmutableArray<SceneTaskUpdate>) || p.ParameterType==typeof(SceneTaskUpdate));
        Assert.Empty(typeof(DarkScreenIntroductionState).GetConstructors());
        Assert.All(typeof(DarkScreenIntroductionState).GetProperties(),p=>Assert.False(p.SetMethod?.IsPublic??false));
        Assert.Null(typeof(DarkScreenIntroductionProgram).GetProperty("Instructions"));
    }
    private static void BindingFailures()
    {
        var s=Through(Start(),150);
        foreach(string change in new[]{"scene","owner","definition","revision","clock","binding","program","barrier"})
        {
            var invalid=Copy(s);
            switch(change)
            {
                case "scene":Set(invalid.Event.Tasks,"SceneRunId",Other);break;
                case "owner":Set(invalid.Dark!,"OwnerId",Other);break;
                case "definition":Set(invalid.Dark!,"Definition",Definition(40));break;
                case "revision":Set(invalid.Event.Tasks,"Tasks",invalid.Event.Tasks.Tasks.SetItem(0,invalid.Event.Tasks.Tasks[0] with {Revision=9}));break;
                case "clock":Set(invalid.Dark!,"LastProcessedTick",149L);break;
                case "binding":Set(invalid,"Binding",invalid.Binding! with {Ready=invalid.Binding!.Ready with {ArtworkId=Other}});break;
                case "program":Set(invalid.Event,"Program",new SceneEventProgram(new("sample:program/other"),[new(new ConditionalEventInstruction(0,ConditionalEventOperation.Exit))]));break;
                case "barrier":Set(invalid.Event.Tasks,"Barrier",invalid.Event.Tasks.Barrier! with {ConsumerId=Other});break;
            }
            Reject(invalid,()=>DarkScreenIntroductionExecutor.AdvanceTick(invalid,false),DarkScreenIntroductionRejection.InvalidManagedBinding);
        }
    }
    private static void InvalidCommandsAndOverflow()
    {
        var fresh=Fresh();Reject(fresh,()=>DarkScreenIntroductionExecutor.AdvanceTick(fresh,false),DarkScreenIntroductionRejection.NotWaiting);
        var s=Start();Reject(s,()=>DarkScreenIntroductionExecutor.AdvanceTick(s,null),DarkScreenIntroductionRejection.MissingCircleSample);
        foreach(int budget in new[]{-1,8193})Reject(s,()=>DarkScreenIntroductionExecutor.AdvanceTick(s,false,budget),DarkScreenIntroductionRejection.InvalidInstructionBudget);
        foreach(int budget in new[]{-1,65})Reject(s,()=>DarkScreenIntroductionExecutor.AdvanceTick(s,false,2,budget),DarkScreenIntroductionRejection.InvalidUpdateBudget);
        var overflow=Copy(Through(Start(),55));Set(overflow.Event.Tasks,"Tick",long.MaxValue);Set(overflow.Dark!,"LastProcessedTick",long.MaxValue);
        var result=Reject(overflow,()=>DarkScreenIntroductionExecutor.AdvanceTick(overflow,false),DarkScreenIntroductionRejection.EventRejected);
        Assert.Equal(SceneTaskRejection.CounterOverflow,result.TaskRejection);
    }
}
