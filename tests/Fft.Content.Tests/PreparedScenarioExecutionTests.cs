using System.Security.Cryptography;
using Fft.Content.Events;
using Fft.Core.Events;
using Xunit;

namespace Fft.Content.Tests;

public sealed class PreparedScenarioExecutionTests
{
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void Hash_verified_prepared_fixture_projects_and_executes_without_raw_block(int input, int outcome)
    {
        byte[] json = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Events", "conditional-event-v1.json"));
        string directory = Path.Combine(Path.GetTempPath(), "fft-event-run-" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(directory, "event.json");
        var id = new Fft.Core.AssetId("sample:event/conditional-outcome-v1");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(file, json);
            new PreparedAssetManifest([new(id, "event.json", json.Length, Convert.ToHexStringLower(SHA256.HashData(json)))])
                .SaveToFile(Path.Combine(directory, PreparedAssetManifest.FileName));
            var prepared = PreparedScenarioEvent.Load(LooseAssetStore.Load(directory), id);
            byte[] before = prepared.ToUtf8();
            var program = prepared.ToExecutionDefinition();
            var result = ConditionalEventExecutor.Run(program.Start(new(7, 8, input, 9)), 10);
            Assert.True(result.Accepted);
            Assert.Equal(new(input == 1 ? 1 : 0, 1, input, outcome), result.State.Variables);
            Assert.Equal(id, program.Id);
            Assert.Equal(14, program.Instructions.Length); // DB is omitted; labels are no runtime operands.
            Assert.Equal(45, program.Instructions[^1].Offset);
            Assert.Equal(before, prepared.ToUtf8()); // Projection/execution does not change the prepared schema/data.
            File.Delete(file);
            File.Delete(Path.Combine(directory, PreparedAssetManifest.FileName));
            var replay = ConditionalEventExecutor.Run(program.Start(new(7, 8, input, 9)), 10);
            Assert.Equal(result.Trace.ToArray(), replay.Trace.ToArray()); // Runtime definition survives removal of files too.
        }
        finally
        {
            File.Delete(file); File.Delete(Path.Combine(directory, PreparedAssetManifest.FileName)); Directory.Delete(directory);
        }
    }

    [Theory]
    [InlineData(0, 10, 3)]
    [InlineData(1, 9, 2)]
    public void Conditional_jump_to_D4_lands_after_marker_while_fallthrough_exits(int comparison, int outcome, int steps)
    {
        var prepared = Definition(
        [new(4, ScenarioOperation.JumpForwardIfZero, 1, TargetOffset: 8),
         new(6, ScenarioOperation.ForwardTargetExit, 1), new(8, ScenarioOperation.AddImmediate, 3, 1),
         new(13, ScenarioOperation.ForwardTargetExit, 2), new(15, ScenarioOperation.SearchEnd)]);
        var result = ConditionalEventExecutor.Run(prepared.ToExecutionDefinition().Start(new(comparison, 0, 0, 9)), 3);
        Assert.True(result.Accepted);
        Assert.Equal(outcome, result.State.Variables.V3);
        Assert.Equal(steps, result.Trace.Length); // Taken: jump, add, exit. Sequential: jump, exit.
        Assert.Equal(comparison == 0 ? new[] { 4, 8, 13 } : new[] { 4, 6 }, result.Trace.Select(t => t.Offset).ToArray());
    }

    [Fact]
    public void Unconditional_jump_ignores_D4_label_and_skips_D2_marker()
    {
        var prepared = Definition(
        [new(4, ScenarioOperation.JumpForward, 1, TargetOffset: 10), new(6, ScenarioOperation.ForwardTargetExit, 1),
         new(8, ScenarioOperation.ForwardTarget, 1), new(10, ScenarioOperation.AddImmediate, 3, 1),
         new(15, ScenarioOperation.ForwardTargetExit, 2), new(17, ScenarioOperation.SearchEnd)]);
        var result = ConditionalEventExecutor.Run(prepared.ToExecutionDefinition().Start(default), 3);
        Assert.True(result.Accepted);
        Assert.Equal(new[] { 4, 10, 15 }, result.Trace.Select(t => t.Offset).ToArray());
        Assert.Equal(1, result.State.Variables.V3);
    }

    private static PreparedScenarioEvent Definition(PreparedScenarioInstruction[] instructions) => new(
        new("sample:event/landing"), ScenarioEventScene.Battle,
        new("synthetic", "SCUS-94221", "11464943bbdbd2a0af022d90cb7f10455cf911df", new string('0', 64), "raw", instructions[^1].Offset + 1),
        instructions);
}
