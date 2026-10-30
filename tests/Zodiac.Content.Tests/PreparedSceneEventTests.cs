using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Zodiac.Framework;
using Zodiac.Framework.Events;
using Zodiac.Content.Events;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class PreparedSceneEventTests
{
    private static readonly AssetId Scene = new("sample:scene/barrier-sequence");
    private static readonly AssetId Consumer = new("sample:consumer/event-a");
    private static readonly AssetId Event = new("sample:event/task-barrier-sequence-v1");
    private static SceneTaskId Id(long sequence) => new(Scene,sequence);
    private static ConditionalEventVariables V(int value) => new(7,8,2,value);

    [Fact]
    public void Independent_fixture_matches_full_combined_states_ordered_traces_and_fresh_definition_replay()
    {
        byte[] bytes = Fixture();
        var definition = PreparedScenarioEvent.Parse(bytes);
        Assert.Equal(PreparedScenarioEvent.TaskBarrierProfile,definition.ExecutionProfile);
        Assert.Equal("a9618ad699ad4d101a60fc3a8a6809e8dc04309a34d86a9d2a0a989e75e83310",definition.Source.BlockSha256);
        Assert.Equal(51,definition.Source.TextOffset);
        Assert.Equal(14,definition.Instructions.Length);
        byte[] frozen = definition.ToUtf8();
        string first = Execute(definition.ToSceneExecutionDefinition());
        string replay = Execute(PreparedScenarioEvent.Parse(frozen).ToSceneExecutionDefinition());
        Assert.Equal(first,replay);
        Assert.Equal(frozen,definition.ToUtf8());
        Assert.Equal(bytes,Fixture());
    }

    private static string Execute(SceneEventProgram program)
    {
        var setup = SceneTaskLedger.ApplyUpdates(SceneTaskState.Create(Scene),
            [new SceneTaskUpdate.Start(SceneTaskFamily.Camera,new("sample:owner/a")),
             new SceneTaskUpdate.Start(SceneTaskFamily.Camera,new("sample:owner/b")),
             new SceneTaskUpdate.Start(SceneTaskFamily.DarkScreen,new("sample:owner/b")),
             new SceneTaskUpdate.Start(SceneTaskFamily.ConditionDisplay,new("sample:owner/a"))]);
        Assert.True(setup.Accepted);
        var state = program.Start(V(9),setup.State,Consumer);
        var histories = new List<string>();
        var expected = ExpectedSteps();
        var commands = new (ImmutableArray<SceneTaskUpdate> Updates,int Budget)[]
        {
            ([],2),
            ([new SceneTaskUpdate.Complete(Id(1),0),new SceneTaskUpdate.Start(SceneTaskFamily.Camera,new("sample:owner/c"))],0),
            ([new SceneTaskUpdate.Complete(Id(2),0),new SceneTaskUpdate.Complete(Id(5),0)],2),
            ([],1),
            ([new SceneTaskUpdate.Phase(Id(3),0,SceneTaskPhase.Holding)],2),
            ([new SceneTaskUpdate.Phase(Id(3),1,SceneTaskPhase.Leaving)],2),
            ([new SceneTaskUpdate.Complete(Id(3),2)],2),
            ([new SceneTaskUpdate.Complete(Id(4),0)],2),
        };
        for (int i=0;i<commands.Length;i++)
        {
            string before = JsonSerializer.Serialize(state);
            var result = i == 0 ? SceneEventExecutor.RunUntilBlocked(state,commands[i].Budget) :
                SceneEventExecutor.AdvanceTick(state,commands[i].Updates,commands[i].Budget);
            Assert.Equal(before,JsonSerializer.Serialize(state));
            Assert.True(result.Accepted,result.Diagnostic);
            Assert.Equal(SceneEventRejection.None,result.Rejection);
            Assert.Equal(SceneTaskRejection.None,result.TaskRejection);
            Assert.Null(result.FailureOffset);
            Assert.Null(result.FailureUpdateIndex);
            Assert.Null(result.Diagnostic);
            var target = expected[i];
            Assert.Equal(i,result.State.Tick);
            Assert.Equal(target.Offset,result.State.Offset);
            Assert.Equal(target.WaitAt,result.State.WaitingAtOffset);
            Assert.Equal(target.Remaining,result.State.RemainingWaitTicks);
            Assert.Equal(target.Suspension,result.State.Suspension);
            Assert.Equal(target.Exited,result.State.Exited);
            Assert.Equal(V(target.Variable3),result.State.Variables);
            Assert.Same(program,result.State.Program);
            Assert.Equal(Consumer,result.State.ConsumerId);
            Assert.Equal(Scene,result.State.Tasks.SceneRunId);
            Assert.Equal(target.Barrier,result.State.Tasks.Barrier);
            Assert.Equal(target.Barrier.Status,result.State.Tasks.BarrierStatus);
            Assert.Equal(target.Tasks,result.State.Tasks.Tasks.ToArray());
            Assert.Equal(target.Tasks.Length,result.State.Tasks.TotalStarted);
            Assert.Equal(target.Tasks.Length+1,result.State.Tasks.NextSequence);
            Assert.Equal(target.Live,result.State.Tasks.LiveTasks);
            Assert.Equal(commands[i].Budget,result.InstructionsExecuted);
            Assert.Equal(commands[i].Updates.Length,result.AcceptedTaskUpdates);
            Assert.Equal(i==0?0:1,result.TicksConsumed);
            Assert.Equal(JsonSerializer.Serialize(target.Trace),JsonSerializer.Serialize(result.Trace));
            histories.Add(JsonSerializer.Serialize(result));
            state = result.State;
        }
        return string.Join('\n',histories);
    }

    private sealed record Expected(int Offset,int? WaitAt,int Remaining,SceneEventSuspension Suspension,
        int Variable3,bool Exited,SceneBarrier Barrier,SceneTask[] Tasks,int Live,SceneEventTrace[] Trace);

    private static Expected[] ExpectedSteps()
    {
        // These literal phase/revision/offset boundaries are independent of the decoder and VM reducer.
        var a = T(1,"a",SceneTaskFamily.Camera,SceneTaskPhase.Running,0);
        var aDone = T(1,"a",SceneTaskFamily.Camera,SceneTaskPhase.Completed,1);
        var b = T(2,"b",SceneTaskFamily.Camera,SceneTaskPhase.Running,0);
        var bDone = T(2,"b",SceneTaskFamily.Camera,SceneTaskPhase.Completed,1);
        var entering = T(3,"b",SceneTaskFamily.DarkScreen,SceneTaskPhase.Entering,0);
        var holding = T(3,"b",SceneTaskFamily.DarkScreen,SceneTaskPhase.Holding,1);
        var leaving = T(3,"b",SceneTaskFamily.DarkScreen,SceneTaskPhase.Leaving,2);
        var darkDone = T(3,"b",SceneTaskFamily.DarkScreen,SceneTaskPhase.Completed,3);
        var condition = T(4,"a",SceneTaskFamily.ConditionDisplay,SceneTaskPhase.Running,0);
        var conditionDone = T(4,"a",SceneTaskFamily.ConditionDisplay,SceneTaskPhase.Completed,1);
        var c = T(5,"c",SceneTaskFamily.Camera,SceneTaskPhase.Running,0);
        var cDone = T(5,"c",SceneTaskFamily.Camera,SceneTaskPhase.Completed,1);
        var camera = B(SceneTaskClass.Camera,7,0);
        var cameraReleased = camera with {Status=SceneBarrierStatus.Released};
        var transition = B(SceneTaskClass.DarkScreenTransition,18,3);
        var hold = B(SceneTaskClass.DarkScreenHold,26,4);
        var release = B(SceneTaskClass.DarkScreenTransition,34,5);
        var banner = B(SceneTaskClass.ConditionDisplay,40,6);
        return
        [
            new(10,7,0,SceneEventSuspension.Barrier,0,false,camera,[a,b,entering,condition],4,
                [E(SceneEventTraceKind.Instruction,0,4,9,0,7,operation:ConditionalEventOperation.ZeroVariable),
                 E(SceneEventTraceKind.BarrierWaitEntered,0,7,0,0,10),
                 L(0,7,10,0,new(SceneTaskTraceKind.BarrierEntered,0,null,null,camera,[]))]),
            new(10,7,0,SceneEventSuspension.Barrier,0,false,camera,[aDone,b,entering,condition,c],4,
                [L(1,7,10,0,new(SceneTaskTraceKind.TickAdvanced,1,null,null,null,[])),
                 L(1,7,10,0,new(SceneTaskTraceKind.TaskCompleted,1,a,aDone,null,[])),
                 L(1,7,10,0,new(SceneTaskTraceKind.TaskStarted,1,null,c,null,[])),
                 L(1,7,10,0,new(SceneTaskTraceKind.BarrierPolled,1,null,null,camera,[Id(2),Id(5)]))]),
            new(18,15,1,SceneEventSuspension.Frames,1,false,cameraReleased,[aDone,bDone,entering,condition,cDone],2,
                [L(2,7,10,0,new(SceneTaskTraceKind.TickAdvanced,2,null,null,null,[])),
                 L(2,7,10,0,new(SceneTaskTraceKind.TaskCompleted,2,b,bDone,null,[])),
                 L(2,7,10,0,new(SceneTaskTraceKind.TaskCompleted,2,c,cDone,null,[])),
                 L(2,7,10,0,new(SceneTaskTraceKind.BarrierPolled,2,null,null,camera,[])),
                 L(2,7,10,0,new(SceneTaskTraceKind.BarrierReleased,2,null,null,cameraReleased,[])),
                 E(SceneEventTraceKind.BarrierWaitResumed,2,7,0,0,10),
                 E(SceneEventTraceKind.Instruction,2,10,0,1,15,operation:ConditionalEventOperation.AddImmediate),
                 E(SceneEventTraceKind.FrameWaitEntered,2,15,1,1,18,1)]),
            new(21,18,0,SceneEventSuspension.Barrier,1,false,transition,[aDone,bDone,entering,condition,cDone],2,
                [L(3,15,18,1,new(SceneTaskTraceKind.TickAdvanced,3,null,null,null,[]),1),
                 E(SceneEventTraceKind.FrameWaitResumed,3,15,1,1,18),
                 E(SceneEventTraceKind.BarrierWaitEntered,3,18,1,1,21),
                 L(3,18,21,1,new(SceneTaskTraceKind.BarrierEntered,3,null,null,transition,[]))]),
            new(29,26,0,SceneEventSuspension.Barrier,3,false,hold,[aDone,bDone,holding,condition,cDone],2,
                [L(4,18,21,1,new(SceneTaskTraceKind.TickAdvanced,4,null,null,null,[])),
                 L(4,18,21,1,new(SceneTaskTraceKind.TaskPhaseChanged,4,entering,holding,null,[])),
                 L(4,18,21,1,new(SceneTaskTraceKind.BarrierPolled,4,null,null,transition,[])),
                 L(4,18,21,1,new(SceneTaskTraceKind.BarrierReleased,4,null,null,transition with {Status=SceneBarrierStatus.Released},[])),
                 E(SceneEventTraceKind.BarrierWaitResumed,4,18,1,1,21),
                 E(SceneEventTraceKind.Instruction,4,21,1,3,26,operation:ConditionalEventOperation.AddImmediate),
                 E(SceneEventTraceKind.BarrierWaitEntered,4,26,3,3,29),
                 L(4,26,29,3,new(SceneTaskTraceKind.BarrierEntered,4,null,null,hold,[]))]),
            new(37,34,0,SceneEventSuspension.Barrier,7,false,release,[aDone,bDone,leaving,condition,cDone],2,
                [L(5,26,29,3,new(SceneTaskTraceKind.TickAdvanced,5,null,null,null,[])),
                 L(5,26,29,3,new(SceneTaskTraceKind.TaskPhaseChanged,5,holding,leaving,null,[])),
                 L(5,26,29,3,new(SceneTaskTraceKind.BarrierPolled,5,null,null,hold,[])),
                 L(5,26,29,3,new(SceneTaskTraceKind.BarrierReleased,5,null,null,hold with {Status=SceneBarrierStatus.Released},[])),
                 E(SceneEventTraceKind.BarrierWaitResumed,5,26,3,3,29),
                 E(SceneEventTraceKind.Instruction,5,29,3,7,34,operation:ConditionalEventOperation.AddImmediate),
                 E(SceneEventTraceKind.BarrierWaitEntered,5,34,7,7,37),
                 L(5,34,37,7,new(SceneTaskTraceKind.BarrierEntered,5,null,null,release,[]))]),
            new(43,40,0,SceneEventSuspension.Barrier,7,false,banner,[aDone,bDone,darkDone,condition,cDone],1,
                [L(6,34,37,7,new(SceneTaskTraceKind.TickAdvanced,6,null,null,null,[])),
                 L(6,34,37,7,new(SceneTaskTraceKind.TaskCompleted,6,leaving,darkDone,null,[])),
                 L(6,34,37,7,new(SceneTaskTraceKind.BarrierPolled,6,null,null,release,[])),
                 L(6,34,37,7,new(SceneTaskTraceKind.BarrierReleased,6,null,null,release with {Status=SceneBarrierStatus.Released},[])),
                 E(SceneEventTraceKind.BarrierWaitResumed,6,34,7,7,37),
                 E(SceneEventTraceKind.ZeroWait,6,37,7,7,40),
                 E(SceneEventTraceKind.BarrierWaitEntered,6,40,7,7,43),
                 L(6,40,43,7,new(SceneTaskTraceKind.BarrierEntered,6,null,null,banner,[]))]),
            new(48,null,0,SceneEventSuspension.Ready,15,true,banner with {Status=SceneBarrierStatus.Released},[aDone,bDone,darkDone,conditionDone,cDone],0,
                [L(7,40,43,7,new(SceneTaskTraceKind.TickAdvanced,7,null,null,null,[])),
                 L(7,40,43,7,new(SceneTaskTraceKind.TaskCompleted,7,condition,conditionDone,null,[])),
                 L(7,40,43,7,new(SceneTaskTraceKind.BarrierPolled,7,null,null,banner,[])),
                 L(7,40,43,7,new(SceneTaskTraceKind.BarrierReleased,7,null,null,banner with {Status=SceneBarrierStatus.Released},[])),
                 E(SceneEventTraceKind.BarrierWaitResumed,7,40,7,7,43),
                 E(SceneEventTraceKind.Instruction,7,43,7,15,48,operation:ConditionalEventOperation.AddImmediate),
                 E(SceneEventTraceKind.Instruction,7,48,15,15,48,exited:true,operation:ConditionalEventOperation.Exit)]),
        ];
    }

    private static SceneTask T(long sequence,string owner,SceneTaskFamily family,SceneTaskPhase phase,long revision)
        => new(Id(sequence),new("sample:owner/"+owner),family,phase,revision);
    private static SceneBarrier B(SceneTaskClass selector,int offset,long tick) => new(SceneBarrierStatus.Pending,selector,Consumer,offset,Event,tick);
    private static SceneEventTrace E(SceneEventTraceKind kind,long tick,int offset,int before,int after,int next,int remaining=0,
        bool exited=false,ConditionalEventOperation? operation=null) => new(kind,tick,offset,V(before),V(after),next,remaining,exited,operation);
    private static SceneEventTrace L(long tick,int offset,int next,int value,SceneTaskTrace fact,int remaining=0)
        => new(SceneEventTraceKind.TaskLedger,tick,offset,V(value),V(value),next,remaining,false,TaskTrace:fact);

    [Theory]
    [InlineData("conditional-profile")]
    [InlineData("timed-profile")]
    [InlineData("world")]
    [InlineData("unknown-profile")]
    [InlineData("negative-selector")]
    [InlineData("large-selector")]
    [InlineData("unsupported-selector")]
    [InlineData("missing-selector")]
    [InlineData("extra-operand")]
    [InlineData("extra-target")]
    [InlineData("negative-wait")]
    [InlineData("large-wait")]
    [InlineData("reachable-db")]
    [InlineData("unknown-operation")]
    [InlineData("duplicate-field")]
    [InlineData("extra-field")]
    [InlineData("missing-field")]
    public void Explicit_profile_retains_strict_closed_admission(string problem)
    {
        var root = JsonNode.Parse(Fixture())!.AsObject();
        var code = root["instructions"]!.AsArray();
        switch(problem)
        {
            case "conditional-profile": root["profile"]=PreparedScenarioEvent.Profile;break;
            case "timed-profile": root["profile"]=PreparedScenarioEvent.TimedProfile;break;
            case "world": root["scene"]="World";break;
            case "unknown-profile": root["profile"]="sample:event-profile/unknown";break;
            case "negative-selector": code[1]!["operand1"]=-1;break;
            case "large-selector": code[1]!["operand1"]=65536;break;
            case "unsupported-selector": code[1]!["operand1"]=32772;break;
            case "missing-selector": code[1]!["operand1"]=null;break;
            case "extra-operand": code[1]!["operand2"]=0;break;
            case "extra-target": code[1]!["targetOffset"]=10;break;
            case "negative-wait": code[3]!["operand1"]=-1;break;
            case "large-wait": code[3]!["operand1"]=32768;break;
            case "reachable-db": code[^2]!["operation"]="ForwardTarget";break;
            case "unknown-operation": code[1]!["operation"]="StartCamera";break;
            case "extra-field": root["clock"]=1;break;
            case "missing-field": root.Remove("source");break;
        }
        string json=root.ToJsonString();
        if(problem=="duplicate-field") json=json.Replace("\"schemaVersion\":1","\"schemaVersion\":1,\"schemaVersion\":1");
        Assert.Throws<InvalidDataException>(() => PreparedScenarioEvent.Parse(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(65535)]
    public void Unsupported_selector_is_rejected_even_after_unconditional_exit(int selector)
    {
        var source = PreparedScenarioEvent.Parse(Fixture()).Source;
        Assert.Throws<InvalidDataException>(() => new PreparedScenarioEvent(Event,ScenarioEventScene.Battle,source,
            [new(4,ScenarioOperation.ForwardTargetExit,0),new(6,ScenarioOperation.WaitForTaskClass,selector),new(9,ScenarioOperation.SearchEnd)],PreparedScenarioEvent.TaskBarrierProfile));
    }

    [Fact]
    public void Profiles_cannot_be_projected_into_other_execution_contracts()
    {
        var scene=PreparedScenarioEvent.Parse(Fixture());
        Assert.Throws<InvalidDataException>(()=>scene.ToExecutionDefinition());
        Assert.Throws<InvalidDataException>(()=>scene.ToTimedExecutionDefinition());
        foreach(string file in new[]{"conditional-event-v1.json","timed-wait-sequence-v1.json"})
        {
            var other=PreparedScenarioEvent.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures","Events",file)));
            Assert.Throws<InvalidDataException>(()=>other.ToSceneExecutionDefinition());
        }
        Assert.Equal(scene.ToUtf8(),PreparedScenarioEvent.Parse(scene.ToUtf8()).ToUtf8());
    }

    [Fact]
    public void Hash_verified_prepared_load_projects_to_core_that_runs_after_files_are_removed()
    {
        byte[] json=Fixture();
        var definition=PreparedScenarioEvent.Parse(json);
        string directory=Path.Combine(Path.GetTempPath(),"fft-task-event-"+Guid.NewGuid().ToString("N"));
        string file=Path.Combine(directory,"event.json");
        string manifest=Path.Combine(directory,PreparedAssetManifest.FileName);
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(file,json);
            new PreparedAssetManifest([new(definition.Id,"event.json",json.Length,Convert.ToHexStringLower(SHA256.HashData(json)))])
                .SaveToFile(manifest);
            var program=PreparedScenarioEvent.Load(LooseAssetStore.Load(directory),definition.Id).ToSceneExecutionDefinition();
            File.AppendAllText(file," ");
            Assert.Throws<InvalidDataException>(()=>PreparedScenarioEvent.Load(LooseAssetStore.Load(directory),definition.Id));
            File.WriteAllBytes(file,json);
            var wrongId=new AssetId("sample:event/wrong");
            File.Delete(manifest);
            new PreparedAssetManifest([new(wrongId,"event.json",json.Length,Convert.ToHexStringLower(SHA256.HashData(json)))])
                .SaveToFile(manifest);
            Assert.Throws<InvalidDataException>(()=>PreparedScenarioEvent.Load(LooseAssetStore.Load(directory),wrongId));
            File.Delete(file); File.Delete(manifest);
            _=Execute(program);
        }
        finally { File.Delete(file); File.Delete(manifest); Directory.Delete(directory); }
    }

    private static byte[] Fixture()=>File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures","Events","task-barrier-sequence-v1.json"));
}
