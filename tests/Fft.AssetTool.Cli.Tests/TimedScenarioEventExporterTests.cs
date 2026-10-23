using System.Buffers.Binary;
using Fft.Content.Events;
using Fft.Core.Events;
using Xunit;

namespace Fft.AssetTool.Cli.Tests;

public sealed class TimedScenarioEventExporterTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(32767)]
    public void Explicit_timed_export_preserves_signed_count_and_untimed_export_stays_closed(short frames)
    {
        var inspection = ScenarioBlockInspector.Inspect(Block([0xf1,(byte)frames,(byte)(frames >> 8),0xd4,0,0xdb]), ScenarioBlockEncoding.Raw);
        var definition = ScenarioEventExporter.CreateSyntheticTimed(inspection,new("sample:event/wait"));
        Assert.Equal(PreparedScenarioEvent.TimedProfile,definition.ExecutionProfile);
        Assert.Equal(frames,definition.Instructions[0].Operand1);
        Assert.Throws<InvalidDataException>(() => ScenarioEventExporter.CreateSynthetic(inspection,new("sample:event/wait"),ScenarioEventScene.Battle));
        var first = TimedEventExecutor.RunUntilBlocked(PreparedScenarioEvent.Parse(definition.ToUtf8()).ToTimedExecutionDefinition().Start(default),2);
        Assert.True(first.Accepted);
        Assert.Equal(frames > 0,first.State.IsWaiting);
        if (frames > 0) Assert.True(TimedEventExecutor.AdvanceTicks(first.State,frames,1).State.Exited);
        else Assert.True(first.State.Exited);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-32768)]
    public void Negative_signed_halfwords_are_rejected_instead_of_reinterpreted_or_clamped(short frames)
    {
        var inspection = ScenarioBlockInspector.Inspect(Block([0xf1,(byte)frames,(byte)(frames >> 8),0xd4,0,0xdb]),ScenarioBlockEncoding.Raw);
        Assert.True(inspection.Complete);
        Assert.Throws<InvalidDataException>(() => ScenarioEventExporter.CreateSyntheticTimed(inspection,new("sample:event/negative")));
    }

    [Theory]
    [InlineData("barrier")]
    [InlineData("speed-change")]
    [InlineData("reachable-end")]
    [InlineData("unknown")]
    [InlineData("truncated")]
    public void New_profile_does_not_admit_other_capabilities_or_malformed_records(string problem)
    {
        byte[] code = problem switch
        {
            "barrier" => [0xe5,4,0,0xd4,0,0xdb],
            "speed-change" => [0x1c,1,0xd4,0,0xdb],
            "reachable-end" => [0xf1,1,0,0xdb],
            "unknown" => [0xff,0xd4,0,0xdb],
            _ => [0xf1,1],
        };
        var inspection = ScenarioBlockInspector.Inspect(Block(code),ScenarioBlockEncoding.Raw);
        Assert.Throws<InvalidDataException>(() => ScenarioEventExporter.CreateSyntheticTimed(inspection,new("sample:event/invalid")));
    }

    [Fact]
    public void Authored_sequence_matches_independent_block_hash_and_processed_input_preserves_boundary()
    {
        byte[] block = Block([0xbe,3,0,0xb0,3,0,1,0,0xf1,1,0,0xb0,3,0,2,0,0xf1,0,0,0xf1,60,0,
            0xb0,3,0,4,0,0xf1,1,0,0xb0,3,0,8,0,0xd4,1,0xdb]);
        var raw = ScenarioEventExporter.CreateSyntheticTimed(ScenarioBlockInspector.Inspect(block,ScenarioBlockEncoding.Raw),new("sample:event/wait-sequence-v1"));
        Assert.Equal("6240a68745c1414f57f1dc08fad7aa44519103035d195b4eacbc92e3cf199b91",raw.Source.BlockSha256);
        Assert.Equal(42,raw.Source.TextOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(block,ScenarioBlockInspector.ProcessedMarker);
        var processed = ScenarioEventExporter.CreateSyntheticTimed(ScenarioBlockInspector.Inspect(block,ScenarioBlockEncoding.Processed,42),raw.Id);
        Assert.Equal(raw.Instructions.ToArray(),processed.Instructions.ToArray());
        Assert.NotEqual(raw.Source.BlockSha256,processed.Source.BlockSha256);
    }

    [Fact]
    public void Forward_jump_to_exit_marker_can_land_on_a_wait_without_executing_that_exit()
    {
        var definition = ScenarioEventExporter.CreateSyntheticTimed(ScenarioBlockInspector.Inspect(
            Block([0xd0,1,0xd4,1,0xf1,1,0,0xd4,2,0xdb]),ScenarioBlockEncoding.Raw),new("sample:event/jump-wait"));
        var program = definition.ToTimedExecutionDefinition();
        var taken = TimedEventExecutor.RunUntilBlocked(program.Start(default),2);
        Assert.True(taken.State.IsWaiting);
        Assert.Equal(8,taken.State.WaitingAtOffset);
        Assert.True(TimedEventExecutor.AdvanceTicks(taken.State,1,1).State.Exited);
        var fallthrough = TimedEventExecutor.RunUntilBlocked(program.Start(new(1,0,0,0)),2);
        Assert.True(fallthrough.State.Exited);
        Assert.Equal(6,fallthrough.State.Offset);
    }

    private static byte[] Block(byte[] code)
    {
        byte[] block = new byte[8192];
        BinaryPrimitives.WriteUInt32LittleEndian(block,(uint)(4+code.Length));
        code.CopyTo(block,4);block[4+code.Length]=0xfe;
        return block;
    }
}
