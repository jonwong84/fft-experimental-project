using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Zodiac.Framework.Events;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class OrderedBannerContractTests
{
    private static readonly AssetId Scene = new("sample:scene/ordered-banner-a");
    private static readonly AssetId Owner = new("sample:owner/a");
    private static readonly AssetId Other = new("sample:owner/b");
    private static readonly AssetId Consumer = new("sample:consumer/a");
    private static readonly AssetId Definition = new("sample:banner/definition");
    private static readonly AssetId Artwork = new("sample:art/general-banner-0");
    private static readonly PreloadedBannerBinding Binding = new(Definition, Artwork, Owner);

    private static OrderedBannerInstruction Op(int offset, ConditionalEventOperation op, int? a = null, int? b = null, int? target = null)
        => new(new SceneEventInstruction(new ConditionalEventInstruction(offset, op, a, b, target)));
    private static OrderedBannerInstruction Display(int offset = 4, int hold = 60)
        => new(offset, new(new(Definition, 8, hold), Artwork, Owner));
    private static OrderedBannerInstruction Barrier(int offset = 7) => new(new SceneEventInstruction(offset, SceneTaskClass.ConditionDisplay));
    private static OrderedBannerInstruction Frames(int offset, int ticks) => new(new SceneEventInstruction(offset, ticks));
    private static OrderedBannerInstruction Exit(int offset = 10) => Op(offset, ConditionalEventOperation.Exit);
    private static OrderedBannerProgram Program(params OrderedBannerInstruction[] instructions)
        => new(new("sample:event/ordered-banner"), instructions);
    private static OrderedBannerState Fresh(int hold = 60, OrderedBannerProgram? program = null,
        SceneTaskState? tasks = null, ConditionalEventVariables variables = default)
        => (program ?? Program(Display(hold: hold), Barrier(), Exit(), Op(12, ConditionalEventOperation.Nop)))
            .Start(variables, tasks ?? SceneTaskState.Create(Scene), Consumer, Binding);
    private static OrderedBannerState Accepted(OrderedBannerResult result)
    {
        Assert.True(result.Accepted, result.Diagnostic);
        return result.State;
    }
    private static OrderedBannerState Waiting(int hold = 60, SceneTaskState? tasks = null)
        => Accepted(OrderedBannerExecutor.RunUntilBlocked(Fresh(hold, tasks: tasks), 2, 1));
    private static OrderedBannerState Through(OrderedBannerState state, long tick, int? circleAt = null)
    {
        while (state.Event.Tick < tick)
            state = Accepted(OrderedBannerExecutor.AdvanceTick(state, state.Event.Tick + 1 == circleAt, [], 8));
        return state;
    }
    private static void Progress(OrderedBannerState state, int reveal, int hold, int hide)
    {
        Assert.NotNull(state.Banner);
        Assert.Equal((reveal, hold, hide), (state.Banner.RevealCompleted, state.Banner.HoldCompleted, state.Banner.HideCompleted));
    }
    private static void Rejected(OrderedBannerState state, OrderedBannerResult result, BannerSceneRejection rejection,
        SceneTaskRejection taskRejection = SceneTaskRejection.None)
    {
        Assert.False(result.Accepted);
        Assert.Same(state, result.State);
        Assert.Equal(rejection, result.Rejection);
        Assert.Equal(taskRejection, result.TaskRejection);
        Assert.NotNull(result.Diagnostic);
        Assert.Empty(result.Trace);
        Assert.Equal((0, 0, 0, 0), (result.TicksConsumed, result.InstructionsExecuted, result.AcceptedTaskUpdates, result.AcceptedEffectTicks));
    }

    public static IEnumerable<object[]> ContractCases()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "banner-opcode-admission-contract-v1.json")));
        Assert.Equal("11464943bbdbd2a0af022d90cb7f10455cf911df", doc.RootElement.GetProperty("referenceRevision").GetString());
        var cases = doc.RootElement.GetProperty("cases").EnumerateArray().Where(c => c.GetProperty("layer").GetString() == "Core").ToArray();
        Assert.Equal(doc.RootElement.GetProperty("scope").GetProperty("coreCaseIds").EnumerateArray().Select(c => c.GetString()),
            cases.Select(c => c.GetProperty("id").GetString()));
        foreach (var c in cases) yield return [c.GetProperty("id").GetString()!, c.Clone()];
    }

    [Theory]
    [MemberData(nameof(ContractCases))]
    public void Independently_authored_BOP_Core_expectations_execute(string id, JsonElement contract)
    {
        var expected = contract.GetProperty("expected");
        switch (id)
        {
            case "BOP-001":
            {
                var run = OrderedBannerExecutor.RunUntilBlocked(Fresh(), 3, 1);
                var state = Accepted(run);
                Assert.Equal(expected.GetProperty("tick").GetInt64(), state.Event.Tick);
                Assert.Equal(expected.GetProperty("instructions").GetInt32(), run.InstructionsExecuted);
                Assert.Equal(expected.GetProperty("updates").GetInt32(), run.AcceptedTaskUpdates);
                Assert.Equal(1, state.Banner!.TaskId.Sequence);
                Assert.Equal(0, state.Event.Tasks.Tasks[0].Revision);
                Assert.Equal(7, state.Event.WaitingAtOffset);
                Assert.Equal(10, state.Event.Offset);
                Assert.Equal(SceneBarrierStatus.Pending, state.Event.Tasks.BarrierStatus);
                Assert.Equal(GeneralBannerPhase.Revealing, state.Banner.Phase);
                Assert.Equal(0, state.Banner.StartTick);
                Progress(state, 0, 0, 0);
                Assert.All(run.Trace, t => Assert.Null(t.CirclePressed));
                break;
            }
            case "BOP-002": case "BOP-003": case "BOP-004":
            case "BOP-005": case "BOP-006": case "BOP-007": case "BOP-008":
            case "BOP-009": case "BOP-010":
                Timing(contract);
                break;
            case "BOP-011":
            {
                var program = Program(Frames(4, 1), Display(7), Barrier(10), Exit(13));
                var waiting = Accepted(OrderedBannerExecutor.RunUntilBlocked(Fresh(program: program), 1));
                var birth = OrderedBannerExecutor.AdvanceTick(waiting, true, [], 2, 1);
                var state = Accepted(birth);
                Assert.Equal(0, birth.AcceptedEffectTicks);
                Assert.Equal(1, state.Banner!.StartTick);
                Assert.Equal(1, state.Event.Tasks.Barrier!.EntryTick);
                Progress(state, 0, 0, 0);
                state = Through(state, 2);
                Progress(state, 1, 0, 0);
                state = Through(state, expected.GetProperty("effectClearedAt").GetInt64());
                Assert.True(state.Banner!.IsTerminal);
                Assert.False(state.Event.Exited);
                Assert.True(Through(state, expected.GetProperty("parentResumedAt").GetInt64()).Event.Exited);
                break;
            }
            case "BOP-012": case "BOP-013":
            {
                var program = id == "BOP-012" ? Program(Barrier(4), Exit(7)) : Program(Barrier(4), Barrier(7), Exit());
                var state = Accepted(OrderedBannerExecutor.RunUntilBlocked(Fresh(program: program), 8));
                Assert.Equal(SceneBarrierStatus.Pending, state.Event.Tasks.BarrierStatus);
                Assert.Empty(state.Event.Tasks.Tasks);
                state = Through(state, 1);
                if (id == "BOP-013")
                {
                    Assert.False(state.Event.Exited);
                    Assert.Equal(7, state.Event.WaitingAtOffset);
                    Assert.Equal(1, state.Event.Tasks.Barrier!.EntryTick);
                    state = Through(state, 2);
                }
                Assert.Equal(expected.GetProperty("parentResumedAt").GetInt64(), state.Event.Tick);
                Assert.True(state.Event.Exited);
                break;
            }
            case "BOP-014":
            {
                var ledger = SceneTaskLedger.ApplyUpdates(SceneTaskState.Create(Scene), [new SceneTaskUpdate.Start(SceneTaskFamily.ConditionDisplay, Other)]).State;
                var state = Through(Waiting(tasks: ledger), 91);
                Assert.True(state.Banner!.IsTerminal);
                state = Accepted(OrderedBannerExecutor.AdvanceTick(state, false, [new SceneTaskUpdate.Complete(new(Scene, 1), 0)], 8));
                Assert.False(state.Event.Exited);
                Assert.Equal(SceneBarrierStatus.Pending, state.Event.Tasks.BarrierStatus);
                Assert.True(Through(state, expected.GetProperty("parentResumedAt").GetInt64()).Event.Exited);
                break;
            }
            case "BOP-015":
            {
                var state = Through(Waiting(), 91);
                Assert.True(state.Event.Tasks.Tasks[0].IsTerminal);
                Assert.False(state.Event.Exited);
                Assert.Equal(SceneBarrierStatus.Pending, state.Event.Tasks.BarrierStatus);
                var tick = OrderedBannerExecutor.AdvanceTick(state, false, [], 1, 0);
                Assert.True(Accepted(tick).Event.Exited);
                Assert.Equal(expected.GetProperty("barrierReleasedAt").GetInt64(), tick.State.Event.Tick);
                Assert.Same(state.Banner, tick.State.Banner);
                Assert.Equal(0, tick.AcceptedEffectTicks);
                break;
            }
            case "BOP-016":
            {
                var state = Through(Waiting(), 91);
                var tick = OrderedBannerExecutor.AdvanceTick(state, false, [new SceneTaskUpdate.Start(SceneTaskFamily.ConditionDisplay, Other)], 1, 1);
                state = Accepted(tick);
                Assert.True(state.Event.Exited);
                Assert.Equal(SceneBarrierStatus.Released, state.Event.Tasks.BarrierStatus);
                Assert.Equal(1, state.Event.Tasks.LiveTasks);
                Assert.Equal(92, state.Event.Tick);
                var release = Array.FindIndex(tick.Trace.ToArray(), t => t.EventTrace?.TaskTrace?.Kind == SceneTaskTraceKind.BarrierReleased);
                var start = Array.FindIndex(tick.Trace.ToArray(), t => t.TaskTrace?.Kind == SceneTaskTraceKind.TaskStarted);
                Assert.True(release >= 0 && start > release);
                break;
            }
            case "BOP-017":
            {
                var state = Fresh(program: Program(Display(), Op(7, ConditionalEventOperation.AddImmediate, 0, 1), Barrier(11), Exit(14)),
                    variables: new(65535, 0, 0, 0));
                Rejected(state, OrderedBannerExecutor.RunUntilBlocked(state, 4, 1), BannerSceneRejection.ArithmeticDomainExceeded);
                Assert.Equal(1, state.Event.Tasks.NextSequence);
                Assert.Null(state.Banner);
                break;
            }
            case "BOP-018":
            {
                var state = Fresh();
                Rejected(state, OrderedBannerExecutor.RunUntilBlocked(state, 1, 1), BannerSceneRejection.InstructionBudgetExceeded);
                Assert.Empty(state.Event.Tasks.Tasks);
                break;
            }
            case "BOP-019":
            {
                var state = Through(Waiting(), 90);
                var facts = ImmutableArray.CreateBuilder<SceneTaskUpdate>();
                for (int i = 0; i < 32; i++) facts.Add(new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Other));
                for (int i = 0; i < 32; i++) facts.Add(new SceneTaskUpdate.Complete(new(Scene, i + 2), 0));
                Rejected(state, OrderedBannerExecutor.AdvanceTick(state, false, facts.ToImmutable(), 0, 64),
                    BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.UpdateBudgetExceeded);
                Assert.Equal(90, state.Event.Tick);
                Assert.Equal(GeneralBannerPhase.ExitYield, state.Banner!.Phase);
                Progress(state, 20, 60, 10);
                var retry = OrderedBannerExecutor.AdvanceTick(state, false, facts.ToImmutable().RemoveAt(63).RemoveAt(31), 0, 64);
                Assert.True(Accepted(retry).Banner!.IsTerminal);
                Assert.Equal(63, retry.AcceptedTaskUpdates);
                break;
            }
            case "BOP-020":
            {
                var ledger = SceneTaskLedger.ApplyUpdates(SceneTaskState.Create(Scene),
                    Enumerable.Repeat<SceneTaskUpdate>(new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Other), 64).ToImmutableArray()).State;
                var state = Accepted(OrderedBannerExecutor.RunUntilBlocked(Fresh(program: Program(Frames(4, 1), Display(7), Barrier(10), Exit(13)), tasks: ledger), 1));
                Rejected(state, OrderedBannerExecutor.AdvanceTick(state, false, [new SceneTaskUpdate.Complete(new(Scene, 1), 0)], 3),
                    BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.LiveTaskLimitExceeded);
                break;
            }
            case "BOP-021":
                foreach (var binding in new PreloadedBannerBinding?[] { null, Binding with { ArtworkId = new("sample:art/wrong") },
                    Binding with { OwnerId = Other }, Binding with { DefinitionId = default } })
                {
                    var state = Fresh().Program.Start(default, SceneTaskState.Create(Scene), Consumer, binding);
                    Rejected(state, OrderedBannerExecutor.RunUntilBlocked(state, 8), BannerSceneRejection.InvalidManagedTaskBinding);
                    Assert.Equal(1, state.Event.Tasks.NextSequence);
                    Assert.Equal(0, state.Event.Tick);
                }
                break;
            case "BOP-022":
                foreach (var state in new[] { Waiting(), Through(Waiting(), 91) })
                    foreach (var fact in new SceneTaskUpdate[] { new SceneTaskUpdate.Phase(state.Banner!.TaskId, 0, SceneTaskPhase.Holding),
                        new SceneTaskUpdate.Complete(state.Banner.TaskId, 0), new SceneTaskUpdate.Cancel(state.Banner.TaskId, 0, SceneTaskCancellationReason.OwnerAcknowledgedCancellation) })
                        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, false, [fact], 8), BannerSceneRejection.ManagedTaskUpdateForbidden);
                break;
            case "BOP-023":
            {
                var state = Accepted(OrderedBannerExecutor.RunUntilBlocked(Fresh(program: Program(Display(), Exit(7))), 2));
                Assert.True(state.Event.Exited);
                var taskId = state.Banner!.TaskId;
                state = Through(state, 5);
                var next = OrderedBannerExecutor.StartNextEvent(state, Program(Barrier(4), Exit(7)), default, new("sample:consumer/b"));
                state = Accepted(next);
                Assert.Equal(5, state.Event.Tick);
                Assert.Equal(taskId, state.Banner!.TaskId);
                state = Accepted(OrderedBannerExecutor.RunUntilBlocked(state, 1));
                state = Through(state, 91);
                Assert.True(state.Banner!.IsTerminal);
                Assert.False(state.Event.Exited);
                Assert.True(Through(state, expected.GetProperty("newConsumerResumedAt").GetInt64()).Event.Exited);
                break;
            }
            case "BOP-024":
            {
                var state = Through(Waiting(), 92);
                state = Accepted(OrderedBannerExecutor.StartNextEvent(state, Fresh().Program, default, new("sample:consumer/b")));
                Rejected(state, OrderedBannerExecutor.RunUntilBlocked(state, 8), BannerSceneRejection.EffectAlreadyAttached);
                Assert.Single(state.Event.Tasks.Tasks);
                break;
            }
            case "BOP-025":
                Replay();
                break;
            case "BOP-026":
            {
                var state = Waiting();
                var tick = OrderedBannerExecutor.AdvanceTick(state, false, [], 0, 1, new(state.Banner!.TaskId, 0));
                state = Accepted(tick);
                Assert.Equal(GeneralBannerPhase.Cancelled, state.Banner!.Phase);
                Assert.Equal(GeneralBannerExitCause.OwnerAcknowledgedCancellation, state.Banner.ExitCause);
                Assert.Equal(SceneTaskPhase.Cancelled, state.Event.Tasks.Tasks[0].Phase);
                Assert.Equal(SceneBarrierStatus.Pending, state.Event.Tasks.BarrierStatus);
                Assert.False(state.Event.Exited);
                Assert.True(Through(state, expected.GetProperty("parentResumedAt").GetInt64()).Event.Exited);
                break;
            }
            case "BOP-027":
            {
                var maxTick = Ledger(SceneTaskState.Create(Scene), tick: long.MaxValue);
                var state = Accepted(OrderedBannerExecutor.RunUntilBlocked(Fresh(tasks: maxTick), 2));
                Rejected(state, OrderedBannerExecutor.AdvanceTick(state, false, [], 1), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.CounterOverflow);
                state = Fresh(tasks: Ledger(SceneTaskState.Create(Scene), sequence: long.MaxValue));
                Rejected(state, OrderedBannerExecutor.RunUntilBlocked(state, 2), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.CounterOverflow);
                break;
            }
            default: throw new InvalidOperationException($"Unimplemented authored expectation {id}");
        }
    }

    private static void Timing(JsonElement contract)
    {
        var expected = contract.GetProperty("expected");
        int hold = contract.GetProperty("given").TryGetProperty("hold", out var h) ? h.GetInt32() : 60;
        int? circleAt = contract.GetProperty("id").GetString() switch
        {
            "BOP-005" => 1, "BOP-006" => 6, "BOP-007" => 21, "BOP-008" => 22,
            "BOP-009" => 82, "BOP-010" => 21, _ => null,
        };
        var state = Waiting(hold);
        long clear = expected.GetProperty("effectClearedAt").GetInt64();
        long resume = expected.GetProperty("parentResumedAt").GetInt64();
        for (long tick = 1; tick <= resume; tick++)
        {
            var result = OrderedBannerExecutor.AdvanceTick(state, tick == circleAt, [], 1);
            state = Accepted(result);
            Assert.Equal(tick, state.Event.Tick);
            Assert.Equal(tick >= clear, state.Banner!.IsTerminal);
            Assert.Equal(tick >= resume, state.Event.Exited);
            if (tick == circleAt && tick < clear && expected.TryGetProperty("extraActiveExitYieldAt", out var skip))
            {
                Assert.Equal(skip.GetInt64(), tick);
                Assert.Equal(GeneralBannerPhase.ExitYield, state.Banner.Phase);
                Assert.False(state.Event.Tasks.Tasks[0].IsTerminal);
            }
            if (tick == 82 && expected.TryGetProperty("progressAt82", out _)) Progress(state, 20, 60, 2);
            if (tick == 21 && expected.TryGetProperty("progressAt21", out _)) Progress(state, 20, 0, 1);
            if (tick == clear)
            {
                Assert.Equal(SceneBarrierStatus.Pending, state.Event.Tasks.BarrierStatus);
                Assert.Equal(1, result.AcceptedTaskUpdates);
                Assert.Equal(1, state.Event.Tasks.Tasks[0].Revision);
                var facts = result.Trace.ToArray();
                int poll = Array.FindIndex(facts, t => t.EventTrace?.TaskTrace?.Kind == SceneTaskTraceKind.BarrierPolled);
                int completed = Array.FindIndex(facts, t => t.TaskTrace?.Kind == SceneTaskTraceKind.TaskCompleted);
                Assert.True(poll >= 0 && completed > poll);
            }
        }
        Assert.Equal(expected.GetProperty("exitCause").GetString(), state.Banner!.ExitCause.ToString());
        if (expected.TryGetProperty("progress", out var progress))
        {
            var values = progress.EnumerateArray().Select(v => v.GetInt32()).ToArray();
            Progress(state, values[0], values[1], values[2]);
        }
    }

    private static void Replay()
    {
        static (string Final, string History) Run(int reads)
        {
            var initial = Fresh();
            var run = OrderedBannerExecutor.RunUntilBlocked(initial, 8);
            var state = Accepted(run);
            var history = new List<string> { JsonSerializer.Serialize(run.Trace) };
            var snapshots = new List<(GeneralBannerState Banner, string Frozen)>();
            for (int tick = 1; tick <= 24; tick++)
            {
                var result = OrderedBannerExecutor.AdvanceTick(state, tick == 22, [], 8);
                state = Accepted(result);
                history.Add(JsonSerializer.Serialize(result.Trace));
                // Simulate a presentation consumer taking zero, one or repeated immutable snapshots.
                for (int i = 0; i < reads; i++) snapshots.Add((state.Banner!, JsonSerializer.Serialize(state.Banner)));
            }
            Assert.True(state.Event.Exited);
            foreach (var (banner, frozen) in snapshots) Assert.Equal(frozen, JsonSerializer.Serialize(banner));
            return (JsonSerializer.Serialize(state), string.Join('\n', history));
        }
        Assert.Equal(Run(1), Run(0));
        Assert.Equal(Run(1), Run(4));
    }

    [Fact]
    public void Forward_branch_can_bypass_admission_without_allocating_a_task()
    {
        var program = Program(Op(4, ConditionalEventOperation.JumpForwardIfZero, target: 10), Display(7), Barrier(10), Exit(13));
        var state = Accepted(OrderedBannerExecutor.RunUntilBlocked(Fresh(program: program), 2, 0));
        Assert.Null(state.Banner);
        Assert.Empty(state.Event.Tasks.Tasks);
        Assert.Equal(1, state.Event.Tasks.NextSequence);
        Assert.True(Through(state, 1).Event.Exited);
        var admitted = Accepted(OrderedBannerExecutor.RunUntilBlocked(Fresh(program: program, variables: new(1, 0, 0, 0)), 3, 1));
        Assert.NotNull(admitted.Banner);
        Assert.False(Through(admitted, 1).Event.Exited);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failure_after_resumed_admission_rolls_back_clock_wait_and_identity(bool arithmetic)
    {
        var program = Program(Frames(4, 1), Display(7), Op(10, ConditionalEventOperation.AddImmediate, 0, 1), Barrier(14), Exit(17));
        var state = Accepted(OrderedBannerExecutor.RunUntilBlocked(Fresh(program: program, variables: new(65535, 0, 0, 0)), 1));
        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, true, [], arithmetic ? 3 : 1),
            arithmetic ? BannerSceneRejection.ArithmeticDomainExceeded : BannerSceneRejection.InstructionBudgetExceeded);
        Assert.Equal(0, state.Event.Tick);
        Assert.Equal(1, state.Event.RemainingWaitTicks);
        Assert.Equal(1, state.Event.Tasks.NextSequence);
        Assert.Empty(state.Event.Tasks.Tasks);
    }

    [Fact]
    public void Generated_start_and_post_VM_facts_share_one_update_budget()
    {
        var program = Program(Frames(4, 1), Display(7), Barrier(10), Exit(13));
        var state = Accepted(OrderedBannerExecutor.RunUntilBlocked(Fresh(program: program), 1));
        ImmutableArray<SceneTaskUpdate> facts = [new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Other)];
        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, false, facts, 2, 1),
            BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.UpdateBudgetExceeded);
        var tick = OrderedBannerExecutor.AdvanceTick(state, true, facts, 2, 2);
        var accepted = Accepted(tick);
        Assert.Equal(2, tick.AcceptedTaskUpdates);
        Assert.Equal(2, tick.InstructionsExecuted);
        Assert.Equal(0, tick.AcceptedEffectTicks);
        Assert.Equal(new(Scene, 1), accepted.Banner!.TaskId);
        Assert.Equal(new(Scene, 2), accepted.Event.Tasks.Tasks[1].Id);
        Progress(accepted, 0, 0, 0);
    }

    [Fact]
    public void Predicted_newborn_managed_identity_is_protected_in_the_same_call()
    {
        var state = Accepted(OrderedBannerExecutor.RunUntilBlocked(Fresh(program: Program(Frames(4, 1), Display(7), Barrier(10), Exit(13))), 1));
        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, false, [new SceneTaskUpdate.Complete(new(Scene, 1), 0)], 2),
            BannerSceneRejection.ManagedTaskUpdateForbidden);
        Assert.Null(state.Banner);
    }

    [Fact]
    public void Failure_after_parent_release_rolls_back_VM_tick_and_trace()
    {
        var state = Through(Waiting(), 91);
        string frozen = JsonSerializer.Serialize(state);
        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, false,
            [new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Other), new SceneTaskUpdate.Complete(new(Scene, 999), 0)], 1),
            BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.UnknownTask);
        Assert.Equal(frozen, JsonSerializer.Serialize(state));
        Assert.False(state.Event.Exited);
        Assert.Equal(SceneBarrierStatus.Pending, state.Event.Tasks.BarrierStatus);
        Assert.True(Accepted(OrderedBannerExecutor.AdvanceTick(state, false, [], 1, 0)).Event.Exited);
    }

    [Fact]
    public void Barrier_resumption_and_following_operations_share_the_instruction_budget()
    {
        var state = Accepted(OrderedBannerExecutor.RunUntilBlocked(Fresh(program: Program(Barrier(4), Op(7, ConditionalEventOperation.AddImmediate, 0, 1), Exit())), 1));
        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, false, [], 1, 0), BannerSceneRejection.InstructionBudgetExceeded);
        Assert.Equal(0, state.Event.Variables.V0);
        var tick = OrderedBannerExecutor.AdvanceTick(state, false, [], 2, 0);
        Assert.True(Accepted(tick).Event.Exited);
        Assert.Equal(1, tick.State.Event.Variables.V0);
        Assert.Equal(2, tick.InstructionsExecuted);
    }

    [Fact]
    public void Tombstones_count_toward_total_limit_even_when_live_capacity_is_available()
    {
        var tasks = Enumerable.Range(1, 256).Select(i => new SceneTask(new(Scene, i), Other,
            SceneTaskFamily.Camera, SceneTaskPhase.Completed, 1)).ToImmutableArray();
        var ledger = Ledger(SceneTaskState.Create(Scene), sequence: 257, tasks: tasks);
        var state = Fresh(tasks: ledger);
        Rejected(state, OrderedBannerExecutor.RunUntilBlocked(state, 2),
            BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.TotalTaskLimitExceeded);
        Assert.Equal(0, state.Event.Tasks.LiveTasks);
    }

    [Fact]
    public void Late_unmanaged_revision_overflow_rolls_back_a_released_parent()
    {
        var task = new SceneTask(new(Scene, 1), Other, SceneTaskFamily.Camera, SceneTaskPhase.Running, long.MaxValue);
        var state = Fresh(program: Program(Barrier(4), Exit(7)),
            tasks: Ledger(SceneTaskState.Create(Scene), sequence: 2, tasks: [task]));
        state = Accepted(OrderedBannerExecutor.RunUntilBlocked(state, 1));
        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, false, [new SceneTaskUpdate.Complete(task.Id, long.MaxValue)], 1),
            BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.CounterOverflow);
        Assert.False(state.Event.Exited);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8193)]
    public void Invalid_instruction_envelopes_reject_without_progress(int budget)
    {
        var state = Fresh();
        Rejected(state, OrderedBannerExecutor.RunUntilBlocked(state, budget), BannerSceneRejection.InvalidInstructionBudget);
        state = Waiting();
        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, false, [], budget), BannerSceneRejection.InvalidInstructionBudget);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65)]
    public void Invalid_update_envelopes_reject_without_progress(int budget)
    {
        var state = Fresh();
        Rejected(state, OrderedBannerExecutor.RunUntilBlocked(state, 2, budget), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.InvalidUpdateBudget);
        state = Waiting();
        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, false, [], 1, budget), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.InvalidUpdateBudget);
    }

    [Fact]
    public void Zero_update_budget_prevents_admission_but_allows_nonterminal_worker_progress()
    {
        var state = Fresh();
        Rejected(state, OrderedBannerExecutor.RunUntilBlocked(state, 2, 0), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.UpdateBudgetExceeded);
        state = Waiting();
        var tick = OrderedBannerExecutor.AdvanceTick(state, false, [], 0, 0);
        Progress(Accepted(tick), 1, 0, 0);
        Assert.Equal(0, tick.AcceptedTaskUpdates);
        state = Through(state, 90);
        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, false, [], 0, 0), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.UpdateBudgetExceeded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Every_tick_requires_an_explicit_Circle_sample_even_without_a_live_effect(bool terminal)
    {
        var state = terminal ? Through(Waiting(), 92) : Accepted(OrderedBannerExecutor.RunUntilBlocked(Fresh(program: Program(Barrier(4), Exit(7))), 1));
        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, null, [], 1), BannerSceneRejection.MissingCircleSample);
    }

    [Theory]
    [InlineData("default", SceneTaskRejection.InvalidUpdateBatch)]
    [InlineData("null", SceneTaskRejection.InvalidUpdate)]
    [InlineData("family", SceneTaskRejection.UnsupportedTaskFamily)]
    [InlineData("size", SceneTaskRejection.UpdateBudgetExceeded)]
    [InlineData("owner", SceneTaskRejection.InvalidId)]
    [InlineData("revision", SceneTaskRejection.InvalidUpdate)]
    public void Malformed_update_envelopes_are_atomic(string fault, SceneTaskRejection reason)
    {
        var state = Waiting();
        ImmutableArray<SceneTaskUpdate> facts = fault switch
        {
            "default" => default, "null" => [null!],
            "family" => [new SceneTaskUpdate.Start((SceneTaskFamily)999, Other)],
            "owner" => [new SceneTaskUpdate.Start(SceneTaskFamily.Camera, default)],
            "revision" => [new SceneTaskUpdate.Complete(new(Scene, 2), -1)],
            _ => Enumerable.Repeat<SceneTaskUpdate>(new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Other), 65).ToImmutableArray(),
        };
        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, true, facts, 0), BannerSceneRejection.TaskLedgerRejected, reason);
    }

    [Theory]
    [InlineData("wrong-scene", SceneTaskRejection.WrongScene)]
    [InlineData("stale", SceneTaskRejection.StaleTaskRevision)]
    [InlineData("unknown", SceneTaskRejection.UnknownTask)]
    public void Semantic_failure_in_post_VM_facts_rolls_back_all_prior_phases(string fault, SceneTaskRejection reason)
    {
        var ledger = SceneTaskLedger.ApplyUpdates(SceneTaskState.Create(Scene), [new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Other)]).State;
        var state = Waiting(tasks: ledger);
        var id = fault == "wrong-scene" ? new SceneTaskId(new("sample:scene/wrong"), 1) : new(Scene, fault == "unknown" ? 100 : 1);
        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, true, [new SceneTaskUpdate.Complete(id, fault == "stale" ? 1 : 0)], 0),
            BannerSceneRejection.TaskLedgerRejected, reason);
        Progress(state, 0, 0, 0);
    }

    [Fact]
    public void Commands_respect_ready_waiting_and_exited_boundaries()
    {
        var state = Fresh();
        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, false, [], 2), BannerSceneRejection.NotWaiting);
        state = Waiting();
        Rejected(state, OrderedBannerExecutor.RunUntilBlocked(state, 2), BannerSceneRejection.AwaitingTick);
        Rejected(state, OrderedBannerExecutor.StartNextEvent(state, Fresh().Program, default, new("sample:consumer/b")), BannerSceneRejection.EventNotExited);
        state = Through(state, 92);
        Rejected(state, OrderedBannerExecutor.RunUntilBlocked(state, 2), BannerSceneRejection.AlreadyExited);
        Rejected(state, OrderedBannerExecutor.StartNextEvent(state, null, default, new("sample:consumer/b")), BannerSceneRejection.InvalidProgram);
        Rejected(state, OrderedBannerExecutor.StartNextEvent(state, Fresh().Program, new(-1, 0, 0, 0), new("sample:consumer/b")), BannerSceneRejection.InvalidVariables);
        Rejected(state, OrderedBannerExecutor.StartNextEvent(state, Fresh().Program, default, Consumer), BannerSceneRejection.InvalidId);
        Rejected(state, OrderedBannerExecutor.StartNextEvent(state, Fresh().Program, default, default), BannerSceneRejection.InvalidId);
    }

    [Fact]
    public void Cancellation_validation_and_budget_failures_leave_the_worker_live()
    {
        var state = Waiting();
        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, false, [], 0, 0, new(state.Banner!.TaskId, 0)),
            BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.UpdateBudgetExceeded);
        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, false, [], 0, cancellation: new(state.Banner.TaskId, 1)),
            BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.StaleTaskRevision);
        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, false, [], 0, cancellation: new(new(new("sample:scene/wrong"), 1), 0)),
            BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.WrongScene);
        state = Through(state, 91);
        Rejected(state, OrderedBannerExecutor.AdvanceTick(state, false, [], 1, cancellation: new(state.Banner!.TaskId, 1)),
            BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.DuplicateTerminalUpdate);
    }

    [Theory]
    [InlineData(-1, 8)]
    [InlineData(256, 8)]
    [InlineData(60, 0)]
    [InlineData(60, 7)]
    [InlineData(60, 9)]
    public void Typed_program_rejects_unsupported_admissions_even_unreachable(int hold, int condition)
        => Assert.Throws<ArgumentException>(() => Program(Exit(1), new(4, new(new(Definition, condition, hold), Artwork, Owner))));

    [Fact]
    public void Typed_program_enforces_one_record_and_normalized_control_flow()
    {
        Assert.Throws<ArgumentException>(() => Program(Display(4), Display(7), Exit()));
        Assert.Throws<ArgumentException>(() => Program(Display(), Op(7, ConditionalEventOperation.JumpForward, target: 4), Exit()));
        Assert.Throws<ArgumentException>(() => Program(Display()));
        Assert.Throws<ArgumentException>(() => Program(Exit(1), new(4, new(new(default, 8, 60), Artwork, Owner))));
        Assert.Throws<ArgumentException>(() => Program(Exit(1), new(4, new(new(Definition, 8, 60), default, Owner))));
        Assert.Throws<ArgumentException>(() => Program(Exit(1), new(4, new(new(Definition, 8, 60), Artwork, default))));
    }

    private static SceneTaskState Ledger(SceneTaskState input, long? tick = null, long? sequence = null,
        ImmutableArray<SceneTask>? tasks = null)
        => (SceneTaskState)Activator.CreateInstance(typeof(SceneTaskState), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [input.SceneRunId, tick ?? input.Tick, sequence ?? input.NextSequence, tasks ?? input.Tasks, input.Barrier], null)!;
}
