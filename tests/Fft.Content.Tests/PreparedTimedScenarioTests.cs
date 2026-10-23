using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Fft.Content.Events;
using Fft.Core.Events;
using Xunit;

namespace Fft.Content.Tests;

public sealed class PreparedTimedScenarioTests
{
    [Fact]
    public void Authored_fixture_has_independent_full_boundary_traces_and_fresh_definition_replay()
    {
        var definition = PreparedScenarioEvent.Parse(Fixture());
        Assert.Equal(PreparedScenarioEvent.TimedProfile, definition.ExecutionProfile);
        Assert.Equal("6240a68745c1414f57f1dc08fad7aa44519103035d195b4eacbc92e3cf199b91", definition.Source.BlockSha256);
        Assert.Equal(42, definition.Source.TextOffset);
        byte[] before = definition.ToUtf8();
        var initial = definition.ToTimedExecutionDefinition().Start(new(7,8,2,9));
        var entered = TimedEventExecutor.RunUntilBlocked(initial, 3);
        var first = TimedEventExecutor.AdvanceTicks(entered.State, 1, 3);
        var middle = TimedEventExecutor.AdvanceTicks(first.State, 59, 0);
        var next = TimedEventExecutor.AdvanceTicks(middle.State, 1, 2);
        var last = TimedEventExecutor.AdvanceTicks(next.State, 1, 2);
        Assert.All(new[] {entered,first,middle,next,last}, result => Assert.True(result.Accepted));
        Assert.Equal(new TimedEventTrace[]
        {
            new(TimedEventTraceKind.Instruction,4,ConditionalEventOperation.ZeroVariable,new(7,8,2,9),new(7,8,2,0),7,0,0,false),
            new(TimedEventTraceKind.Instruction,7,ConditionalEventOperation.AddImmediate,new(7,8,2,0),new(7,8,2,1),12,0,0,false),
            new(TimedEventTraceKind.WaitEntered,12,null,new(7,8,2,1),new(7,8,2,1),15,1,0,false),
        }, entered.Trace.ToArray());
        Assert.Equal(new TimedEventTrace[]
        {
            new(TimedEventTraceKind.WaitResumed,12,null,new(7,8,2,1),new(7,8,2,1),15,0,1,false),
            new(TimedEventTraceKind.Instruction,15,ConditionalEventOperation.AddImmediate,new(7,8,2,1),new(7,8,2,3),20,0,1,false),
            new(TimedEventTraceKind.ZeroWait,20,null,new(7,8,2,3),new(7,8,2,3),23,0,1,false),
            new(TimedEventTraceKind.WaitEntered,23,null,new(7,8,2,3),new(7,8,2,3),26,60,1,false),
        }, first.Trace.ToArray());
        Assert.True(middle.State.IsWaiting);
        Assert.Equal(1, middle.State.RemainingWaitTicks);
        Assert.Equal(60, middle.State.ElapsedTicks);
        Assert.Equal(new TimedEventTrace[]
        {
            new(TimedEventTraceKind.WaitResumed,23,null,new(7,8,2,3),new(7,8,2,3),26,0,61,false),
            new(TimedEventTraceKind.Instruction,26,ConditionalEventOperation.AddImmediate,new(7,8,2,3),new(7,8,2,7),31,0,61,false),
            new(TimedEventTraceKind.WaitEntered,31,null,new(7,8,2,7),new(7,8,2,7),34,1,61,false),
        }, next.Trace.ToArray());
        Assert.Equal(new TimedEventTrace[]
        {
            new(TimedEventTraceKind.WaitResumed,31,null,new(7,8,2,7),new(7,8,2,7),34,0,62,false),
            new(TimedEventTraceKind.Instruction,34,ConditionalEventOperation.AddImmediate,new(7,8,2,7),new(7,8,2,15),39,0,62,false),
            new(TimedEventTraceKind.Instruction,39,ConditionalEventOperation.Exit,new(7,8,2,15),new(7,8,2,15),39,0,62,true),
        }, last.Trace.ToArray());
        Assert.True(last.State.Exited);
        Assert.Equal(new(7,8,2,9), initial.Variables);
        Assert.Equal(before, definition.ToUtf8());
        var fresh = PreparedScenarioEvent.Parse(before).ToTimedExecutionDefinition().Start(new(7,8,2,9));
        var replayStart = TimedEventExecutor.RunUntilBlocked(fresh, 3);
        var replay = TimedEventExecutor.AdvanceTicks(replayStart.State, 62, 7);
        Assert.True(replay.Accepted);
        Assert.Equal(first.Trace.Concat(middle.Trace).Concat(next.Trace).Concat(last.Trace).ToArray(), replay.Trace.ToArray());
        Assert.Equal(last.State.Variables, replay.State.Variables);
        Assert.Equal(last.State.ElapsedTicks, replay.State.ElapsedTicks);
    }

    [Theory]
    [InlineData("conditional-profile")]
    [InlineData("unknown-profile")]
    [InlineData("world-scene")]
    [InlineData("negative-count")]
    [InlineData("large-count")]
    [InlineData("missing-count")]
    [InlineData("extra-operand")]
    [InlineData("extra-target")]
    [InlineData("unknown-operation")]
    [InlineData("reachable-end")]
    [InlineData("truncated")]
    public void Timed_profile_rejects_unsupported_context_or_program_before_execution(string problem)
    {
        var root = JsonNode.Parse(Fixture())!.AsObject();
        var instructions = root["instructions"]!.AsArray();
        switch (problem)
        {
            case "conditional-profile": root["profile"] = PreparedScenarioEvent.Profile; break;
            case "unknown-profile": root["profile"] = "sample:event-profile/tutorial-speed2"; break;
            case "world-scene": root["scene"] = "World"; break;
            case "negative-count": instructions[2]!["operand1"] = -1; break;
            case "large-count": instructions[2]!["operand1"] = 32768; break;
            case "missing-count": instructions[2]!["operand1"] = null; break;
            case "extra-operand": instructions[2]!["operand2"] = 1; break;
            case "extra-target": instructions[2]!["targetOffset"] = 23; break;
            case "unknown-operation": instructions[2]!["operation"] = "WaitForInstruction"; break;
            case "reachable-end": instructions[^2]!["operation"] = "ForwardTarget"; break;
        }
        byte[] json = Encoding.UTF8.GetBytes(root.ToJsonString());
        if (problem == "truncated") json = json[..^5];
        Assert.Throws<InvalidDataException>(() => PreparedScenarioEvent.Parse(json));
    }

    [Fact]
    public void Profile_selection_is_explicit_and_conditional_serialization_and_execution_are_unchanged()
    {
        var timed = PreparedScenarioEvent.Parse(Fixture());
        Assert.Throws<InvalidDataException>(() => timed.ToExecutionDefinition());
        var conditional = PreparedScenarioEvent.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Events", "conditional-event-v1.json")));
        Assert.Equal(PreparedScenarioEvent.Profile, conditional.ExecutionProfile);
        Assert.Throws<InvalidDataException>(() => conditional.ToTimedExecutionDefinition());
        var result = ConditionalEventExecutor.Run(conditional.ToExecutionDefinition().Start(new(7,8,1,9)),10);
        Assert.True(result.Accepted);
        Assert.Equal(new(1,1,1,1), result.State.Variables);
        Assert.Equal(conditional.ToUtf8(), PreparedScenarioEvent.Parse(conditional.ToUtf8()).ToUtf8());
    }

    [Fact]
    public void Timed_loading_is_hash_verified_and_runtime_needs_no_prepared_or_original_file_after_projection()
    {
        byte[] json = Fixture();
        var definition = PreparedScenarioEvent.Parse(json);
        string directory = Path.Combine(Path.GetTempPath(), "fft-timed-event-" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(directory,"event.json");
        string manifest = Path.Combine(directory,PreparedAssetManifest.FileName);
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(file,json);
            new PreparedAssetManifest([new(definition.Id,"event.json",json.Length,Convert.ToHexStringLower(SHA256.HashData(json)))])
                .SaveToFile(manifest);
            var program = PreparedScenarioEvent.Load(LooseAssetStore.Load(directory), definition.Id).ToTimedExecutionDefinition();
            File.AppendAllText(file," ");
            Assert.Throws<InvalidDataException>(() => PreparedScenarioEvent.Load(LooseAssetStore.Load(directory), definition.Id));
            File.Delete(file); File.Delete(manifest);
            var initial = TimedEventExecutor.RunUntilBlocked(program.Start(default),3);
            Assert.True(TimedEventExecutor.AdvanceTicks(initial.State,62,7).State.Exited);
        }
        finally { File.Delete(file); File.Delete(manifest); Directory.Delete(directory); }
    }

    private static byte[] Fixture() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures","Events","timed-wait-sequence-v1.json"));
}
