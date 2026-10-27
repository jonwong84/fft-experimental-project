using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Zodiac.Framework.Events;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class DarkScreenContractTests
{
    private static readonly AssetId Scene = new("sample:scene/dark-screen");
    private static readonly AssetId Owner = new("sample:owner/dark-screen");
    private static readonly AssetId Other = new("sample:owner/other");
    private static readonly AssetId Consumer = new("sample:consumer/dark-screen");
    private static readonly AssetId DefinitionId = new("sample:effect/dark-screen");
    private static readonly ReadyDarkScreenBinding Binding = new(DefinitionId, Owner);

    // Authored delays exercise 0, 1 and the selected maximum, without copying the original table/grid.
    private static DarkScreenTileDefinition[] Cells(int max = 41) => Enumerable.Range(0, 288)
        .Select(i => new DarkScreenTileDefinition(i % 24 * 12 - 12, i / 24 * 24 - (i % 2 == 0 ? 12 : 24), i == 287 ? max : i == 1 && max > 0 ? 1 : 0)).ToArray();
    private static DarkScreenDefinition Definition(int max = 41) => new(DefinitionId, new(), Cells(max));
    private static DarkScreenInstruction Start(int offset = 4, DarkScreenDefinition? definition = null) => new(offset, new StartDarkScreen(definition ?? Definition(), Owner));
    private static DarkScreenInstruction Barrier(int offset) => new(new SceneEventInstruction(offset, SceneTaskClass.DarkScreenTransition));
    private static DarkScreenInstruction Wait(int offset, int frames) => new(new SceneEventInstruction(offset, frames));
    private static DarkScreenInstruction Op(int offset, ConditionalEventOperation op, int? a = null, int? b = null, int? target = null)
        => new(new SceneEventInstruction(new ConditionalEventInstruction(offset, op, a, b, target)));
    private static DarkScreenInstruction Exit(int offset) => Op(offset, ConditionalEventOperation.Exit);
    private static DarkScreenProgram Program(params DarkScreenInstruction[] instructions) => new(new("sample:event/dark-screen"), instructions);
    private static DarkScreenProgram Natural(int wait = 44, int max = 41) => Program(Start(definition: Definition(max)), Barrier(7), Wait(10, wait), new(13), Barrier(16), Exit(19));
    private static DarkScreenSceneState Fresh(DarkScreenProgram? program = null, ReadyDarkScreenBinding? binding = null, SceneTaskState? tasks = null)
        => (program ?? Natural()).StartScene(default, tasks ?? SceneTaskState.Create(Scene), Consumer, binding ?? Binding);
    private static DarkScreenSceneState Accept(DarkScreenResult result)
    {
        Assert.True(result.Accepted, result.Diagnostic);
        return result.State;
    }
    private static DarkScreenSceneState Born(DarkScreenProgram? program = null) => Accept(DarkScreenExecutor.RunUntilBlocked(Fresh(program), 8));
    private static DarkScreenSceneState Through(DarkScreenSceneState state, long tick)
    {
        while (state.Event.Tick < tick) state = Accept(DarkScreenExecutor.AdvanceTick(state, [], 16));
        return state;
    }
    private static void Reject(DarkScreenSceneState input, DarkScreenResult result, DarkScreenRejection reason,
        SceneTaskRejection task = SceneTaskRejection.None, SceneEventRejection sceneEvent = SceneEventRejection.None)
    {
        Assert.False(result.Accepted);
        Assert.Same(input, result.State);
        Assert.Equal(reason, result.Rejection);
        Assert.Equal(task, result.TaskRejection);
        Assert.Equal(sceneEvent, result.EventRejection);
        Assert.NotEmpty(result.Diagnostic!);
        Assert.Empty(result.Trace);
        Assert.Equal((0, 0, 0, 0), (result.InstructionsExecuted, result.AcceptedTaskUpdates, result.AcceptedWorkerTicks, result.TicksConsumed));
    }
    private static void Task(DarkScreenSceneState state, DarkScreenPhase phase, SceneTaskClass? activeClass, long revision)
    {
        Assert.Equal(phase, state.Worker!.Phase);
        var task = Assert.Single(state.Event.Tasks.Tasks.Where(t => t.Id == state.Worker.TaskId));
        Assert.Equal(Owner, task.OwnerId);
        Assert.Equal(SceneTaskFamily.DarkScreen, task.Family);
        Assert.Equal(activeClass, task.ActiveClass);
        Assert.Equal(revision, task.Revision);
        Assert.Null(task.CancellationReason);
    }

    public static IEnumerable<object[]> CoreCases()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "dark-screen-contract-v1.json")));
        Assert.Equal("11464943bbdbd2a0af022d90cb7f10455cf911df", doc.RootElement.GetProperty("referenceRevision").GetString());
        Assert.True(doc.RootElement.GetProperty("specificationOnly").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("executed").GetBoolean());
        foreach (var item in doc.RootElement.GetProperty("cases").EnumerateArray().Take(24))
        {
            Assert.False(item.GetProperty("executed").GetBoolean());
            yield return [item.GetProperty("id").GetString()!];
        }
    }

    [Theory]
    [MemberData(nameof(CoreCases))]
    public void Historical_logical_contract_cases_execute(string id)
    {
        switch (id)
        {
            case "DSC-001":
            {
                foreach (var binding in new ReadyDarkScreenBinding?[] { null, new(new("sample:wrong/definition"), Owner), new(DefinitionId, Other) })
                {
                    var state = Natural().StartScene(default, SceneTaskState.Create(Scene), Consumer, binding);
                    Reject(state, DarkScreenExecutor.RunUntilBlocked(state, 8), DarkScreenRejection.InvalidManagedBinding);
                    Assert.Equal(1, state.Event.Tasks.NextSequence);
                    Assert.Null(state.Worker);
                }
                break;
            }
            case "DSC-002":
            {
                var result = DarkScreenExecutor.RunUntilBlocked(Fresh(), 2, 1);
                var state = Accept(result);
                Task(state, DarkScreenPhase.Entering, SceneTaskClass.DarkScreenTransition, 0);
                Assert.Equal(0, state.Worker!.LastProcessedTick);
                Assert.Equal(DarkScreenDrawKind.None, state.Worker.Draw.Kind);
                Assert.Equal(Definition().Tiles.Select(t => new DarkScreenTileState(t.InitialDelay, 0, 0)), state.Worker.Tiles);
                Assert.Equal((2, 1, 0, 0), (result.InstructionsExecuted, result.AcceptedTaskUpdates, result.AcceptedWorkerTicks, result.TicksConsumed));
                break;
            }
            case "DSC-003":
            {
                var state = Through(Born(), 1);
                Assert.Equal(new DarkScreenTileDraw(0, 0, 0), state.Worker!.Draw.Tiles[0]);
                Assert.Equal(new DarkScreenTileState(0, 64, 4), state.Worker.Tiles[0]);
                break;
            }
            case "DSC-004":
            {
                var state = Through(Born(), 1);
                Assert.DoesNotContain(state.Worker!.Draw.Tiles, t => t.TileIndex == 1);
                Assert.Equal(new DarkScreenTileState(0, 0, 0), state.Worker.Tiles[1]);
                state = Through(state, 2);
                Assert.Equal(new DarkScreenTileDraw(1, 0, 0), Assert.Single(state.Worker!.Draw.Tiles.Where(t => t.TileIndex == 1)));
                break;
            }
            case "DSC-005":
            {
                var state = Through(Born(), 54);
                Task(state, DarkScreenPhase.Entering, SceneTaskClass.DarkScreenTransition, 0);
                Assert.True(state.Worker!.BodyFinished);
                state = Through(state, 55);
                Task(state, DarkScreenPhase.Holding, SceneTaskClass.DarkScreenHold, 1);
                Assert.Equal(288, state.Worker!.Draw.Tiles.Length);
                Assert.All(state.Worker.Draw.Tiles, t => Assert.Equal((512, 48), (t.Angle, t.Radius)));
                break;
            }
            case "DSC-006":
            {
                var state = Through(Born(), 55);
                Assert.Equal(SceneEventSuspension.Barrier, state.Event.Suspension);
                var result = DarkScreenExecutor.AdvanceTick(state, [], 1, 0);
                state = Accept(result);
                Assert.Equal(SceneEventSuspension.Frames, state.Event.Suspension);
                Assert.Equal(10, state.Event.WaitingAtOffset);
                Task(state, DarkScreenPhase.Holding, SceneTaskClass.DarkScreenHold, 1);
                Assert.Contains(result.Trace, t => t.EventTrace?.TaskTrace?.Kind == SceneTaskTraceKind.BarrierReleased);
                break;
            }
            case "DSC-007":
            {
                var state = Through(Born(), 55);
                Assert.Equal(DarkScreenDrawKind.Tiles, state.Worker!.Draw.Kind);
                state = Through(state, 56);
                Assert.Equal(DarkScreenDrawKind.Flash, state.Worker!.Draw.Kind);
                Assert.Equal((48, 40, 16), (DarkScreenDraw.Red, DarkScreenDraw.Green, DarkScreenDraw.Blue));
                break;
            }
            case "DSC-008":
            {
                var state = Through(Born(Program(Start(), Barrier(7), Exit(10))), 1024);
                Assert.True(state.Event.Exited);
                Task(state, DarkScreenPhase.Holding, SceneTaskClass.DarkScreenHold, 1);
                Assert.Equal(DarkScreenDrawKind.Flash, state.Worker!.Draw.Kind);
                break;
            }
            case "DSC-009":
            {
                var state = Fresh(Program(new(1), Start(), new(6), Barrier(7), Exit(10)));
                var result = DarkScreenExecutor.RunUntilBlocked(state, 4, 1);
                Assert.Equal(2, result.Trace.Count(t => t.Kind == DarkScreenTraceKind.ReleaseNoMatch));
                state = Through(Accept(result), 100);
                Task(state, DarkScreenPhase.Holding, SceneTaskClass.DarkScreenHold, 1);
                break;
            }
            case "DSC-010":
            {
                var state = Through(Born(Natural(0)), 55);
                Assert.Equal(DarkScreenPhase.Holding, state.Worker!.Phase);
                state = Through(state, 56);
                Task(state, DarkScreenPhase.Leaving, SceneTaskClass.DarkScreenTransition, 2);
                Assert.Equal(DarkScreenDrawKind.Flash, state.Worker!.Draw.Kind);
                break;
            }
            case "DSC-011":
            {
                var result = DarkScreenExecutor.AdvanceTick(Through(Born(), 99), [], 2, 1);
                var state = Accept(result);
                Task(state, DarkScreenPhase.Leaving, SceneTaskClass.DarkScreenTransition, 2);
                Assert.Equal(DarkScreenDrawKind.Flash, state.Worker!.Draw.Kind);
                Assert.False(state.Worker.ReleaseYieldPending);
                Assert.Equal(Definition().Tiles.Select(t => t.InitialDelay), state.Worker.Tiles.Select(t => t.RemainingDelay));
                Assert.All(state.Worker.Tiles, t => Assert.Equal((512, 48), (t.Angle, t.Radius)));
                Assert.True(Array.FindIndex(result.Trace.ToArray(), t => t.Kind == DarkScreenTraceKind.ReleaseMatched) <
                    Array.FindIndex(result.Trace.ToArray(), t => t.Kind == DarkScreenTraceKind.WorkerAdvanced));
                break;
            }
            case "DSC-012":
            {
                var state = Through(Born(), 101);
                Assert.Equal(new DarkScreenTileDraw(287, 512, 48), state.Worker!.Draw.Tiles[287]);
                Assert.Equal(new DarkScreenTileState(40, 512, 48), state.Worker.Tiles[287]);
                Assert.Equal(new DarkScreenTileDraw(0, 512, 48), state.Worker.Draw.Tiles[0]);
                Assert.Equal(new DarkScreenTileState(0, 448, 44), state.Worker.Tiles[0]);
                break;
            }
            case "DSC-013":
            {
                var state = Through(Born(), 153);
                Assert.False(state.Worker!.BodyFinished);
                Assert.Equal(new DarkScreenTileState(0, 0, 0), state.Worker.Tiles[287]);
                state = Through(state, 154);
                Assert.True(state.Worker!.BodyFinished);
                Assert.False(state.Worker.ExitYield);
                Assert.Equal(new DarkScreenTileDraw(287, 0, 0), state.Worker.Draw.Tiles[287]);
                state = Through(state, 155);
                Assert.True(state.Worker!.ExitYield);
                Assert.All(state.Worker.Draw.Tiles, t => Assert.Equal((0, 0), (t.Angle, t.Radius)));
                break;
            }
            case "DSC-014":
            {
                var state = Through(Born(), 155);
                Task(state, DarkScreenPhase.Leaving, SceneTaskClass.DarkScreenTransition, 2);
                Assert.True(state.Worker!.ExitYield);
                Assert.Equal(DarkScreenDrawKind.Tiles, state.Worker.Draw.Kind);
                state = Through(state, 156);
                Task(state, DarkScreenPhase.Completed, null, 3);
                Assert.Equal(DarkScreenDrawKind.None, state.Worker!.Draw.Kind);
                break;
            }
            case "DSC-015":
            {
                var state = Through(Born(), 156);
                Assert.False(state.Event.Exited);
                Assert.Equal(SceneBarrierStatus.Pending, state.Event.Tasks.BarrierStatus);
                state = Through(state, 157);
                Assert.True(state.Event.Exited);
                Assert.Equal(SceneBarrierStatus.Released, state.Event.Tasks.BarrierStatus);
                Assert.Equal(156, state.Worker!.LastProcessedTick);
                break;
            }
            case "DSC-016":
            {
                var program = Program(Start(), Barrier(7), new(10), new(11), Barrier(13), new(16), Exit(19));
                var state = Through(Born(program), 113);
                Assert.True(state.Event.Exited);
                Task(state, DarkScreenPhase.Completed, null, 3);
                Assert.Equal(1, state.Event.Tasks.TotalStarted);
                break;
            }
            case "DSC-017":
            {
                var state = Born();
                var identity = state.Worker!.TaskId;
                foreach (var (tick, phase, cls, rev) in new (int, DarkScreenPhase, SceneTaskClass?, int)[]
                         { (55, DarkScreenPhase.Holding, SceneTaskClass.DarkScreenHold, 1), (100, DarkScreenPhase.Leaving, SceneTaskClass.DarkScreenTransition, 2), (156, DarkScreenPhase.Completed, null, 3) })
                {
                    state = Through(state, tick);
                    Task(state, phase, cls, rev);
                    Assert.Equal(identity, state.Worker!.TaskId);
                    Assert.Equal(Owner, state.Worker.OwnerId);
                    Assert.Equal(2, state.Event.Tasks.NextSequence);
                }
                Assert.Equal(0, state.Event.Tasks.LiveTasks);
                break;
            }
            case "DSC-018":
            {
                var state = Fresh();
                Reject(state, DarkScreenExecutor.RunUntilBlocked(state, 1, 1), DarkScreenRejection.EventRejected,
                    sceneEvent: SceneEventRejection.InstructionBudgetExceeded);
                Assert.Equal(1, state.Event.Tasks.NextSequence);
                Assert.Equal(1, Accept(DarkScreenExecutor.RunUntilBlocked(state, 2, 1)).Worker!.TaskId.Sequence);
                break;
            }
            case "DSC-019":
            {
                var state = Through(Born(), 54);
                Reject(state, DarkScreenExecutor.AdvanceTick(state, [], 0, 0), DarkScreenRejection.TaskLedgerRejected, SceneTaskRejection.UpdateBudgetExceeded);
                Assert.Equal(DarkScreenPhase.Holding, Accept(DarkScreenExecutor.AdvanceTick(state, [], 0, 1)).Worker!.Phase);
                break;
            }
            case "DSC-020":
            {
                var state = Through(Born(), 99);
                Reject(state, DarkScreenExecutor.AdvanceTick(state, [new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Other)], 2, 1),
                    DarkScreenRejection.TaskLedgerRejected, SceneTaskRejection.UpdateBudgetExceeded);
                Assert.Equal(DarkScreenPhase.Holding, state.Worker!.Phase);
                var result = DarkScreenExecutor.AdvanceTick(state, [], 2, 1);
                Task(Accept(result), DarkScreenPhase.Leaving, SceneTaskClass.DarkScreenTransition, 2);
                Assert.Single(result.Trace.Where(t => t.Kind == DarkScreenTraceKind.ReleaseMatched));
                break;
            }
            case "DSC-021":
            {
                var state = Through(Born(), 155);
                Reject(state, DarkScreenExecutor.AdvanceTick(state, [], 0, 0), DarkScreenRejection.TaskLedgerRejected, SceneTaskRejection.UpdateBudgetExceeded);
                Assert.False(state.Event.Exited);
                Assert.True(Accept(DarkScreenExecutor.AdvanceTick(state, [], 0, 1)).Worker!.IsTerminal);
                break;
            }
            case "DSC-022":
            {
                var a = Born(); var b = Born();
                while (a.Event.Tick < 158)
                {
                    var before = Snapshot(a);
                    for (int read = 0; read < 3; read++) _ = a.Worker!.Draw.Tiles.ToArray();
                    Assert.Equal(before, Snapshot(a));
                    var ar = DarkScreenExecutor.AdvanceTick(a, [], 16);
                    var br = DarkScreenExecutor.AdvanceTick(b, [], 16);
                    Assert.Equal(Snapshot(ar), Snapshot(br));
                    a = Accept(ar); b = Accept(br);
                    Assert.Equal(Snapshot(a), Snapshot(b));
                }
                break;
            }
            case "DSC-023":
                foreach (var parameters in InvalidParameters())
                    Assert.Throws<ArgumentException>(() => new DarkScreenDefinition(DefinitionId, parameters, Cells()));
                break;
            case "DSC-024":
            {
                var state = Born(); var idValue = state.Worker!.TaskId;
                foreach (var update in new SceneTaskUpdate[] { new SceneTaskUpdate.Start(SceneTaskFamily.DarkScreen, Other),
                             new SceneTaskUpdate.Phase(idValue, 99, SceneTaskPhase.Holding), new SceneTaskUpdate.Complete(idValue, 0),
                             new SceneTaskUpdate.Cancel(idValue, 0, SceneTaskCancellationReason.OwnerAcknowledgedCancellation) })
                    Reject(state, DarkScreenExecutor.AdvanceTick(state, [update], 8), DarkScreenRejection.ManagedTaskUpdateForbidden);
                var ledger = SceneTaskLedger.ApplyUpdates(SceneTaskState.Create(Scene), [new SceneTaskUpdate.Start(SceneTaskFamily.DarkScreen, Other)]).State;
                var foreign = Fresh(tasks: ledger);
                Reject(foreign, DarkScreenExecutor.RunUntilBlocked(foreign, 8), DarkScreenRejection.InvalidManagedBinding);
                break;
            }
            default: throw new InvalidOperationException($"Unmapped logical case {id}.");
        }
    }

    private static string Snapshot<T>(T value) => JsonSerializer.Serialize(value);
    private static IEnumerable<DarkScreenParameters> InvalidParameters()
    {
        yield return new(Reserved: 1); yield return new(Mode: 0); yield return new(Mode: 2);
        yield return new(Divisor: 0); yield return new(Divisor: 256); yield return new(AngleStep: 0);
        yield return new(AngleStep: 65536); yield return new(RadiusStep: 0); yield return new(RadiusStep: -1); yield return new(Image: 1);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)] [InlineData(41)]
    public void Authored_delay_boundaries_preserve_strict_phase_lengths(int maximum)
    {
        int holding = maximum + 14, release = holding + 1, clear = release + holding + 1;
        var state = Through(Born(Natural(0, maximum)), holding - 1);
        Assert.Equal(DarkScreenPhase.Entering, state.Worker!.Phase);
        state = Through(state, holding);
        Assert.Equal(DarkScreenPhase.Holding, state.Worker!.Phase);
        state = Through(state, release);
        Assert.Equal(DarkScreenPhase.Leaving, state.Worker!.Phase);
        state = Through(state, clear - 1);
        Assert.True(state.Worker!.ExitYield);
        state = Through(state, clear);
        Assert.True(state.Worker!.IsTerminal);
        Assert.False(state.Event.Exited);
        Assert.True(Through(state, clear + 1).Event.Exited);
    }

    [Fact]
    public void Definition_defensively_copies_authored_data_and_enforces_layout_envelope()
    {
        var cells = Cells(); var def = new DarkScreenDefinition(DefinitionId, new(), cells);
        cells[0] = new(999, 999, 999);
        Assert.Equal(new DarkScreenTileDefinition(-12, -12, 0), def.Tiles[0]);
        foreach (var data in new[] { Cells()[..287], Cells().Append(new()).ToArray(), cells,
                     Cells().Select((x,i) => i == 0 ? x with { InitialDelay = -1 } : x).ToArray(),
                     Cells().Select((x,i) => i == 0 ? x with { InitialDelay = 42 } : x).ToArray() })
            Assert.Throws<ArgumentException>(() => new DarkScreenDefinition(DefinitionId, new(), data));
        Assert.Throws<ArgumentException>(() => new DarkScreenDefinition(default, new(), Cells()));
        Assert.Throws<ArgumentNullException>(() => new DarkScreenDefinition(DefinitionId, null!, Cells()));
        Assert.Throws<ArgumentNullException>(() => new DarkScreenDefinition(DefinitionId, new(), null!));
    }

    [Fact]
    public void Draws_preserve_row_major_submission_order_and_input_snapshots()
    {
        var before = Through(Born(), 41); var snapshot = Snapshot(before);
        var after = Through(before, 42);
        Assert.Equal(Enumerable.Range(0, 288), after.Worker!.Draw.Tiles.Select(t => t.TileIndex));
        Assert.Equal(snapshot, Snapshot(before));
        Assert.Equal(new DarkScreenTileDraw(287, 0, 0), after.Worker.Draw.Tiles[287]);
    }

    [Fact]
    public void Worker_born_during_parent_resume_stays_at_first_yield()
    {
        var state = Fresh(Program(Wait(1, 1), Start(), Barrier(7), Exit(10)));
        state = Accept(DarkScreenExecutor.RunUntilBlocked(state, 1, 0));
        var result = DarkScreenExecutor.AdvanceTick(state, [], 2, 1);
        state = Accept(result);
        Assert.Equal((1, 1, 0), (state.Worker!.StartTick, state.Worker.LastProcessedTick, result.AcceptedWorkerTicks));
        Assert.Equal(DarkScreenDrawKind.None, state.Worker.Draw.Kind);
        Assert.Equal(new DarkScreenTileState(0, 64, 4), Through(state, 2).Worker!.Tiles[0]);
    }

    [Fact]
    public void Predicted_newborn_identity_cannot_be_completed_by_external_updates()
    {
        var state = Accept(DarkScreenExecutor.RunUntilBlocked(Fresh(Program(Wait(1, 1), Start(), Barrier(7), Exit(10))), 1));
        Reject(state, DarkScreenExecutor.AdvanceTick(state, [new SceneTaskUpdate.Phase(new(Scene, 1), 0, SceneTaskPhase.Holding)], 2),
            DarkScreenRejection.ManagedTaskUpdateForbidden);
        Assert.Null(state.Worker);
        Assert.Equal(1, state.Event.Tasks.NextSequence);
    }

    [Fact]
    public void Late_arithmetic_failure_rolls_back_admission_and_release()
    {
        var fail = Op(6, ConditionalEventOperation.AddImmediate, 0, 1);
        var program = Program(Start(), fail, Barrier(7), Exit(10));
        var state = program.StartScene(new(65535, 0, 0, 0), SceneTaskState.Create(Scene), Consumer, Binding);
        Reject(state, DarkScreenExecutor.RunUntilBlocked(state, 8), DarkScreenRejection.EventRejected,
            sceneEvent: SceneEventRejection.ArithmeticDomainExceeded);
        program = Program(Start(), Barrier(7), new(10), Op(11, ConditionalEventOperation.AddImmediate, 0, 1), Barrier(13), Exit(16));
        state = program.StartScene(new(65535, 0, 0, 0), SceneTaskState.Create(Scene), Consumer, Binding);
        state = Through(Accept(DarkScreenExecutor.RunUntilBlocked(state, 8)), 55);
        Reject(state, DarkScreenExecutor.AdvanceTick(state, [], 8), DarkScreenRejection.EventRejected,
            sceneEvent: SceneEventRejection.ArithmeticDomainExceeded);
        Task(state, DarkScreenPhase.Holding, SceneTaskClass.DarkScreenHold, 1);
    }

    [Theory]
    [InlineData(-1)] [InlineData(8193)]
    public void Invalid_instruction_budgets_are_atomic(int budget)
    {
        var state = Fresh();
        Reject(state, DarkScreenExecutor.RunUntilBlocked(state, budget), DarkScreenRejection.EventRejected,
            sceneEvent: SceneEventRejection.InvalidInstructionBudget);
        state = Born();
        Reject(state, DarkScreenExecutor.AdvanceTick(state, [], budget), DarkScreenRejection.EventRejected,
            sceneEvent: SceneEventRejection.InvalidInstructionBudget);
    }

    [Theory]
    [InlineData(-1)] [InlineData(65)]
    public void Invalid_update_budgets_are_atomic(int budget)
    {
        var state = Fresh();
        Reject(state, DarkScreenExecutor.RunUntilBlocked(state, 8, budget), DarkScreenRejection.TaskLedgerRejected, SceneTaskRejection.InvalidUpdateBudget);
        state = Born();
        Reject(state, DarkScreenExecutor.AdvanceTick(state, [], 8, budget), DarkScreenRejection.TaskLedgerRejected, SceneTaskRejection.InvalidUpdateBudget);
    }

    [Fact]
    public void Invalid_update_batches_reject_before_tick_and_worker()
    {
        var state = Born();
        Reject(state, DarkScreenExecutor.AdvanceTick(state, default, 8), DarkScreenRejection.TaskLedgerRejected, SceneTaskRejection.InvalidUpdateBatch);
        Reject(state, DarkScreenExecutor.AdvanceTick(state, [null!], 8), DarkScreenRejection.TaskLedgerRejected, SceneTaskRejection.InvalidUpdate);
        Reject(state, DarkScreenExecutor.AdvanceTick(state, [new SceneTaskUpdate.Start((SceneTaskFamily)99, Other)], 8),
            DarkScreenRejection.TaskLedgerRejected, SceneTaskRejection.UnsupportedTaskFamily);
    }

    [Fact]
    public void Unmanaged_tasks_apply_after_parent_poll_and_share_update_budget()
    {
        var state = Through(Born(), 55);
        var result = DarkScreenExecutor.AdvanceTick(state, [new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Other)], 1, 1);
        state = Accept(result);
        Assert.Equal(2, state.Event.Tasks.LiveTasks);
        Assert.Equal(10, state.Event.WaitingAtOffset);
        var facts = result.Trace.ToArray();
        Assert.True(Array.FindIndex(facts, t => t.EventTrace?.TaskTrace?.Kind == SceneTaskTraceKind.BarrierReleased) <
                    Array.FindIndex(facts, t => t.TaskTrace?.Kind == SceneTaskTraceKind.TaskStarted));
        state = Through(state, 99);
        Reject(state, DarkScreenExecutor.AdvanceTick(state, [new SceneTaskUpdate.Complete(new(Scene, 2), 0)], 2, 1),
            DarkScreenRejection.TaskLedgerRejected, SceneTaskRejection.UpdateBudgetExceeded);
    }

    [Fact]
    public void Parent_resume_budget_failure_does_not_publish_barrier_success()
    {
        var state = Through(Born(), 156);
        Reject(state, DarkScreenExecutor.AdvanceTick(state, [], 0, 0), DarkScreenRejection.EventRejected,
            sceneEvent: SceneEventRejection.InstructionBudgetExceeded);
        Assert.Equal(SceneBarrierStatus.Pending, state.Event.Tasks.BarrierStatus);
        Assert.True(Accept(DarkScreenExecutor.AdvanceTick(state, [], 1, 0)).Event.Exited);
    }

    [Fact]
    public void Program_rejects_second_syntactic_admission()
    {
        Assert.Throws<ArgumentException>(() => Program(Start(), Wait(7, 1), Start(10), Exit(13)));
    }

    [Fact]
    public void Clock_overflow_is_atomic()
    {
        var tasks = SceneTaskState.Create(Scene);
        typeof(SceneTaskState).GetField("<Tick>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(tasks, long.MaxValue);
        var state = BornAt(tasks);
        Reject(state, DarkScreenExecutor.AdvanceTick(state, [], 8), DarkScreenRejection.EventRejected,
            SceneTaskRejection.CounterOverflow, SceneEventRejection.TaskLedgerRejected);
        static DarkScreenSceneState BornAt(SceneTaskState tasks) => Accept(DarkScreenExecutor.RunUntilBlocked(Fresh(tasks: tasks), 8));
    }

    [Fact]
    public void Exited_event_retains_worker_and_terminal_tombstone_without_revival()
    {
        var state = Through(Born(Program(Start(), Barrier(7), new(10), Exit(13))), 112);
        Assert.True(state.Event.Exited);
        Assert.True(state.Worker!.IsTerminal);
        var worker = state.Worker;
        var result = DarkScreenExecutor.AdvanceTick(state, [], 0, 0);
        state = Accept(result);
        Assert.Same(worker, state.Worker);
        Assert.Equal(0, result.AcceptedWorkerTicks);
        Assert.Single(result.Trace.Where(t => t.Kind == DarkScreenTraceKind.WorkerObserved));
        Reject(state, DarkScreenExecutor.RunUntilBlocked(state, 1), DarkScreenRejection.EventRejected,
            sceneEvent: SceneEventRejection.AlreadyExited);
    }

    [Fact]
    public void Every_authored_cell_matches_an_independent_closed_form_through_both_phases()
    {
        var state = Born();
        for (int tick = 1; tick <= 156; tick++)
        {
            state = Through(state, tick);
            for (int index = 0; index < 288; index++)
            {
                int delay = DefinitionDelay(index);
                int elapsed = tick <= 55 ? tick : tick < 100 ? 55 : tick - 100;
                int active = Math.Max(0, elapsed - delay);
                int angle = tick < 100 ? Math.Min(512, active * 64) : Math.Max(0, 512 - active * 64);
                int radius = tick < 100 ? Math.Min(48, active * 4) : Math.Max(0, 48 - active * 4);
                int remaining = tick is >= 55 and < 100 ? 0 : Math.Max(0, delay - elapsed);
                Assert.Equal(new DarkScreenTileState(remaining, angle, radius), state.Worker!.Tiles[index]);
                if (tick is > 55 and <= 100 || tick == 156) continue;
                var draw = state.Worker.Draw.Tiles.Where(t => t.TileIndex == index).ToArray();
                if (tick <= 55 && tick <= delay) { Assert.Empty(draw); continue; }
                int beforeActive = Math.Max(0, elapsed - 1 - delay);
                var expected = new DarkScreenTileDraw(index,
                    tick <= 55 ? Math.Min(512, beforeActive * 64) : Math.Max(0, 512 - beforeActive * 64),
                    tick <= 55 ? Math.Min(48, beforeActive * 4) : Math.Max(0, 48 - beforeActive * 4));
                Assert.Equal(expected, Assert.Single(draw));
            }
        }
        static int DefinitionDelay(int index) => index == 287 ? 41 : index == 1 ? 1 : 0;
    }

    [Fact]
    public void External_late_unknown_task_failure_restores_event_signal_and_clock()
    {
        var state = Through(Born(), 99);
        Reject(state, DarkScreenExecutor.AdvanceTick(state, [new SceneTaskUpdate.Complete(new(Scene, 99), 0)], 2),
            DarkScreenRejection.TaskLedgerRejected, SceneTaskRejection.UnknownTask);
        Task(state, DarkScreenPhase.Holding, SceneTaskClass.DarkScreenHold, 1);
        Assert.Equal(99, state.Event.Tick);
    }

    [Fact]
    public void External_update_uses_remaining_budget_before_worker_phase_publication()
    {
        var state = Through(Born(), 54);
        Reject(state, DarkScreenExecutor.AdvanceTick(state, [new SceneTaskUpdate.Start(SceneTaskFamily.ConditionDisplay, Other)], 0, 1),
            DarkScreenRejection.TaskLedgerRejected, SceneTaskRejection.UpdateBudgetExceeded);
        Assert.Equal(1, state.Event.Tasks.TotalStarted);
        Assert.Equal(2, state.Event.Tasks.NextSequence);
        state = Accept(DarkScreenExecutor.AdvanceTick(state, [new SceneTaskUpdate.Start(SceneTaskFamily.ConditionDisplay, Other)], 0, 2));
        Assert.Equal(2, state.Event.Tasks.TotalStarted);
        Task(state, DarkScreenPhase.Holding, SceneTaskClass.DarkScreenHold, 1);
    }

    [Fact]
    public void Completion_still_obeys_scene_wide_barriers_for_other_transition_tasks()
    {
        // Other live DarkScreen workers are deliberately unsupported; a condition-display barrier remains independent.
        var tasks = SceneTaskLedger.ApplyUpdates(SceneTaskState.Create(Scene), [new SceneTaskUpdate.Start(SceneTaskFamily.ConditionDisplay, Other)]).State;
        var program = Program(Start(), Barrier(7), new(10), Barrier(13),
            new(new SceneEventInstruction(16, SceneTaskClass.ConditionDisplay)), Exit(19));
        var state = Through(Accept(DarkScreenExecutor.RunUntilBlocked(Fresh(program, tasks: tasks), 8)), 114);
        Assert.True(state.Worker!.IsTerminal);
        Assert.False(state.Event.Exited);
        var result = DarkScreenExecutor.AdvanceTick(state, [new SceneTaskUpdate.Complete(new(Scene, 1), 0)], 0, 1);
        state = Accept(result);
        Assert.False(state.Event.Exited);
        Assert.True(Through(state, 116).Event.Exited);
    }

    [Fact]
    public void A_skipped_syntactic_start_does_not_allocate_and_release_is_no_match()
    {
        var program = Program(Op(1, ConditionalEventOperation.JumpForward, target: 7), Start(), new(7), Barrier(10), Exit(13));
        var result = DarkScreenExecutor.RunUntilBlocked(Fresh(program), 3, 0);
        var state = Accept(result);
        Assert.Null(state.Worker);
        Assert.Equal(1, state.Event.Tasks.NextSequence);
        Assert.Single(result.Trace.Where(t => t.Kind == DarkScreenTraceKind.ReleaseNoMatch));
        state = Through(state, 1);
        Assert.True(state.Event.Exited);
        Assert.Empty(state.Event.Tasks.Tasks);
    }

    [Fact]
    public void No_effect_program_needs_no_binding_and_empty_barrier_still_yields()
    {
        var state = Program(new(1), Barrier(4), Exit(7)).StartScene(default, SceneTaskState.Create(Scene), Consumer, null);
        state = Accept(DarkScreenExecutor.RunUntilBlocked(state, 2, 0));
        Assert.False(state.Event.Exited);
        Assert.True(Accept(DarkScreenExecutor.AdvanceTick(state, [], 1, 0)).Event.Exited);
        Assert.Null(state.Worker);
    }

    [Fact]
    public void Pure_adapter_rejects_ready_or_suspended_wrong_command_without_advancing()
    {
        var state = Fresh();
        Reject(state, DarkScreenExecutor.AdvanceTick(state, [], 8), DarkScreenRejection.EventRejected, sceneEvent: SceneEventRejection.NotWaiting);
        state = Born();
        Reject(state, DarkScreenExecutor.RunUntilBlocked(state, 8), DarkScreenRejection.EventRejected, sceneEvent: SceneEventRejection.AwaitingTick);
    }

    [Theory]
    [InlineData("owner")] [InlineData("revision")] [InlineData("phase")] [InlineData("clock")] [InlineData("foreign")]
    public void Corrupted_retained_binding_or_external_ledger_state_rejects(string field)
    {
        var state = Born();
        switch (field)
        {
            case "owner":
                Set(state.Event.Tasks, "Tasks", ImmutableArray.Create(state.Event.Tasks.Tasks[0] with { OwnerId = Other })); break;
            case "revision":
                Set(state.Event.Tasks, "Tasks", ImmutableArray.Create(state.Event.Tasks.Tasks[0] with { Revision = 1 })); break;
            case "phase":
                Set(state.Event.Tasks, "Tasks", ImmutableArray.Create(state.Event.Tasks.Tasks[0] with { Phase = SceneTaskPhase.Holding })); break;
            case "clock": Set(state.Worker!, "LastProcessedTick", 1L); break;
            case "foreign":
                Set(state.Event.Tasks, "Tasks", state.Event.Tasks.Tasks.Add(new(new(Scene, 2), Other, SceneTaskFamily.DarkScreen, SceneTaskPhase.Holding, 1))); break;
        }
        Reject(state, DarkScreenExecutor.AdvanceTick(state, [], 8), DarkScreenRejection.InvalidManagedBinding);
        static void Set<T>(object target, string property, T value)
            => target.GetType().GetField($"<{property}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }

    [Fact]
    public void Starting_on_an_existing_scene_preserves_sequences_and_completed_history()
    {
        var tasks = SceneTaskLedger.ApplyUpdates(SceneTaskState.Create(Scene), [new SceneTaskUpdate.Start(SceneTaskFamily.DarkScreen, Other)]).State;
        tasks = SceneTaskLedger.ApplyUpdates(tasks, [new SceneTaskUpdate.Phase(new(Scene, 1), 0, SceneTaskPhase.Holding),
            new SceneTaskUpdate.Phase(new(Scene, 1), 1, SceneTaskPhase.Leaving), new SceneTaskUpdate.Complete(new(Scene, 1), 2)]).State;
        var state = Accept(DarkScreenExecutor.RunUntilBlocked(Fresh(tasks: tasks), 2, 1));
        Assert.Equal(2, state.Worker!.TaskId.Sequence);
        Assert.Equal(3, state.Event.Tasks.NextSequence);
        Assert.Equal(SceneTaskPhase.Completed, state.Event.Tasks.Tasks[0].Phase);
        Assert.Equal(3, state.Event.Tasks.Tasks[0].Revision);
    }

    [Fact]
    public void Admission_rejects_scene_capacity_without_losing_prior_history()
    {
        var tasks = SceneTaskState.Create(Scene);
        for (int i = 0; i < 64; i++)
            tasks = SceneTaskLedger.ApplyUpdates(tasks, [new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Other)]).State;
        var state = Fresh(tasks: tasks);
        Reject(state, DarkScreenExecutor.RunUntilBlocked(state, 2, 1), DarkScreenRejection.TaskLedgerRejected, SceneTaskRejection.LiveTaskLimitExceeded);
        Assert.Equal(65, state.Event.Tasks.NextSequence);
    }

    [Fact]
    public void Profile_remains_typed_and_rejects_invalid_program_payloads()
    {
        Assert.Throws<ArgumentNullException>(() => new DarkScreenProgram(new("sample:event/invalid"), null!));
        Assert.Throws<ArgumentException>(() => Program(new DarkScreenInstruction[] { null! }));
        Assert.Throws<ArgumentException>(() => Program(new(1, new StartDarkScreen(null!, Owner)), Exit(4)));
        Assert.Throws<ArgumentException>(() => Program(new(1, new StartDarkScreen(Definition(), default)), Exit(4)));
        Assert.Throws<ArgumentException>(() => Program(Start(), Barrier(7))); // Missing explicit exit.
        Assert.Throws<ArgumentException>(() => Program(Start(), new(4), Exit(7)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneEventInstruction(4, (SceneTaskClass)999));
    }
}
