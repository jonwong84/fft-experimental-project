using System.Buffers.Binary;
using Fft.Content.Events;
using Fft.Core;
using Fft.Core.Events;
using Xunit;

namespace Fft.AssetTool.Cli.Tests;

public sealed class SceneScenarioEventExporterTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(54)]
    [InlineData(55)]
    [InlineData(56)]
    public void Explicit_task_export_decodes_supported_unsigned_selector_and_old_profiles_reject_it(int selector)
    {
        var inspection=ScenarioBlockInspector.Inspect(Block([0xe5,(byte)selector,0,0xd4,0,0xdb]),ScenarioBlockEncoding.Raw);
        var definition=ScenarioEventExporter.CreateSyntheticTaskBarriers(inspection,new("sample:event/barrier"));
        Assert.Equal(PreparedScenarioEvent.TaskBarrierProfile,definition.ExecutionProfile);
        Assert.Equal(ScenarioEventScene.Battle,definition.Scene);
        Assert.Equal(ScenarioOperation.WaitForTaskClass,definition.Instructions[0].Operation);
        Assert.Equal(selector,definition.Instructions[0].Operand1);
        Assert.Throws<InvalidDataException>(()=>ScenarioEventExporter.CreateSyntheticTimed(inspection,definition.Id));
        Assert.Throws<InvalidDataException>(()=>ScenarioEventExporter.CreateSynthetic(inspection,definition.Id,ScenarioEventScene.Battle));
        Assert.Throws<InvalidDataException>(()=>ScenarioEventExporter.CreateSynthetic(inspection,definition.Id,ScenarioEventScene.World));
        var program=PreparedScenarioEvent.Parse(definition.ToUtf8()).ToSceneExecutionDefinition();
        var entered=SceneEventExecutor.RunUntilBlocked(program.Start(default,SceneTaskState.Create(new("sample:scene/a")),new("sample:consumer/a")),1);
        Assert.Equal(SceneEventSuspension.Barrier,entered.State.Suspension);
        Assert.Equal((SceneTaskClass)selector,entered.State.Tasks.Barrier!.Selector);
        Assert.True(SceneEventExecutor.AdvanceTick(entered.State,[],1).State.Exited);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(260)]
    [InlineData(32772)]
    [InlineData(65535)]
    public void Unsupported_unsigned_halfword_cannot_be_narrowed_to_supported_low_byte_even_when_unreachable(int selector)
    {
        // D4 exits before E5. Admission must still inspect all structurally decoded records.
        var inspection=ScenarioBlockInspector.Inspect(Block([0xd4,0,0xe5,(byte)selector,(byte)(selector>>8),0xdb]),ScenarioBlockEncoding.Raw);
        Assert.True(inspection.Complete);
        Assert.Equal((byte)selector,inspection.Instructions[1].Operands[0]);
        Assert.Equal((byte)(selector>>8),inspection.Instructions[1].Operands[1]);
        Assert.Throws<InvalidDataException>(()=>ScenarioEventExporter.CreateSyntheticTaskBarriers(inspection,new("sample:event/unsupported")));
    }

    [Theory]
    [InlineData("speed")]
    [InlineData("camera")]
    [InlineData("remove-dark")]
    [InlineData("unknown")]
    [InlineData("reachable-end")]
    [InlineData("truncated")]
    [InlineData("negative-wait")]
    public void New_profile_rejects_unselected_capabilities_and_malformed_input(string problem)
    {
        byte[] code=problem switch
        {
            "speed"=>[0xd4,0,0x1c,1,0xdb],
            "camera"=>[0x28,0,0,0xd4,0,0xdb],
            "remove-dark"=>[0xd4,0,0x77,0xdb],
            "unknown"=>[0xff,0xd4,0,0xdb],
            "reachable-end"=>[0xe5,4,0,0xdb],
            "negative-wait"=>[0xf1,0xff,0xff,0xd4,0,0xdb],
            _=>[0xe5,4],
        };
        Assert.Throws<InvalidDataException>(()=>ScenarioEventExporter.CreateSyntheticTaskBarriers(
            ScenarioBlockInspector.Inspect(Block(code),ScenarioBlockEncoding.Raw),new("sample:event/invalid")));
    }

    [Fact]
    public void Literal_block_matches_independent_fixture_hash_offsets_and_processed_inspection()
    {
        byte[] block=Block([0xbe,3,0,0xe5,4,0,0xb0,3,0,1,0,0xf1,1,0,0xe5,54,0,0xb0,3,0,2,0,
            0xe5,55,0,0xb0,3,0,4,0,0xe5,54,0,0xf1,0,0,0xe5,56,0,0xb0,3,0,8,0,0xd4,1,0xdb]);
        var raw=ScenarioEventExporter.CreateSyntheticTaskBarriers(ScenarioBlockInspector.Inspect(block,ScenarioBlockEncoding.Raw),new("sample:event/task-barrier-sequence-v1"));
        Assert.Equal("a9618ad699ad4d101a60fc3a8a6809e8dc04309a34d86a9d2a0a989e75e83310",raw.Source.BlockSha256);
        Assert.Equal(51,raw.Source.TextOffset);
        Assert.Equal(new[]{4,7,10,15,18,21,26,29,34,37,40,43,48,50},raw.Instructions.Select(i=>i.Offset));
        Assert.Equal(new int?[]{4,54,55,54,56},raw.Instructions.Where(i=>i.Operation==ScenarioOperation.WaitForTaskClass).Select(i=>i.Operand1));
        BinaryPrimitives.WriteUInt32LittleEndian(block,ScenarioBlockInspector.ProcessedMarker);
        var processed=ScenarioEventExporter.CreateSyntheticTaskBarriers(ScenarioBlockInspector.Inspect(block,ScenarioBlockEncoding.Processed,51),raw.Id);
        Assert.Equal(raw.Instructions.ToArray(),processed.Instructions.ToArray());
        Assert.NotEqual(raw.Source.BlockSha256,processed.Source.BlockSha256);
        Assert.Equal("processed",processed.Source.Encoding);
        Assert.Equal(raw.ToUtf8(),PreparedScenarioEvent.Parse(raw.ToUtf8()).ToUtf8());
    }

    [Fact]
    public void Forward_jump_to_exit_marker_lands_after_it_on_barrier_then_yields()
    {
        var definition=ScenarioEventExporter.CreateSyntheticTaskBarriers(ScenarioBlockInspector.Inspect(
            Block([0xd0,1,0xd4,1,0xe5,4,0,0xd4,2,0xdb]),ScenarioBlockEncoding.Raw),new("sample:event/jump-barrier"));
        Assert.Equal(8,definition.Instructions[0].TargetOffset);
        var program=definition.ToSceneExecutionDefinition();
        var tasks=SceneTaskState.Create(new("sample:scene/a"));
        var taken=SceneEventExecutor.RunUntilBlocked(program.Start(default,tasks,new("sample:consumer/a")),2);
        Assert.Equal(SceneEventSuspension.Barrier,taken.State.Suspension);
        Assert.Equal(8,taken.State.WaitingAtOffset);
        Assert.Equal(11,taken.State.Offset);
        Assert.Equal(0,taken.State.Tick);
        Assert.True(SceneEventExecutor.AdvanceTick(taken.State,[],1).State.Exited);
        var fallthrough=SceneEventExecutor.RunUntilBlocked(program.Start(new(1,0,0,0),tasks,new("sample:consumer/a")),2);
        Assert.True(fallthrough.State.Exited);
        Assert.Null(fallthrough.State.Tasks.Barrier);
        Assert.Equal(6,fallthrough.State.Offset);
    }

    private static byte[] Block(byte[] code)
    {
        byte[] block=new byte[8192];
        BinaryPrimitives.WriteUInt32LittleEndian(block,(uint)(4+code.Length));
        code.CopyTo(block,4);block[4+code.Length]=0xfe;
        return block;
    }
}
