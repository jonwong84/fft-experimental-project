using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Zodiac.Framework.Events;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class ReadySceneContractTests
{
    private static readonly AssetId Scene = new("sample:scene/ready-a");
    private static readonly AssetId Owner = new("sample:owner/ready-a");
    private static readonly AssetId Other = new("sample:owner/other");
    private static readonly AssetId Consumer = new("sample:consumer/ready-a");
    private static readonly AssetId Definition = new("sample:ready/definition-a");
    private static readonly AssetId Art = new("sample:art/ready-image0-palette2");
    private static readonly PreloadedBannerBinding Binding = new(Definition, Art, Owner);
    private static ReadySceneInstruction Op(int offset, ConditionalEventOperation op, int? a = null, int? b = null, int? target = null)
        => new(new SceneEventInstruction(new ConditionalEventInstruction(offset, op, a, b, target)));
    private static ReadySceneInstruction Display(int offset = 4, int hold = 60)
        => new(offset, new(new(Definition, 0, hold), Art, Owner));
    private static ReadySceneInstruction Barrier(int offset = 7) => new(new SceneEventInstruction(offset, SceneTaskClass.ConditionDisplay));
    private static ReadySceneInstruction Frames(int offset, int ticks) => new(new SceneEventInstruction(offset, ticks));
    private static ReadySceneInstruction Exit(int offset = 10) => Op(offset, ConditionalEventOperation.Exit);
    private static ReadySceneProgram Program(params ReadySceneInstruction[] instructions) => new(new("sample:event/ready"), instructions);
    private static ReadySceneState Fresh(int hold = 60, ReadySceneProgram? program = null, SceneTaskState? tasks = null,
        ConditionalEventVariables variables = default)
        => (program ?? Program(Display(hold: hold), Barrier(), Exit(), Op(12, ConditionalEventOperation.Nop)))
            .Start(variables, tasks ?? SceneTaskState.Create(Scene), Consumer, Binding);
    private static ReadySceneState Accepted(ReadySceneResult result)
    {
        Assert.True(result.Accepted, $"{result.Rejection}/{result.TaskRejection}: {result.Diagnostic}");
        return result.State;
    }
    private static ReadySceneState Waiting(int hold = 60, SceneTaskState? tasks = null)
        => Accepted(ReadySceneExecutor.RunUntilBlocked(Fresh(hold, tasks: tasks), 2, 1));
    private static ReadySceneState Through(ReadySceneState state, long tick, int? circleAt = null)
    {
        while (state.Event.Tick < tick)
            state = Accepted(ReadySceneExecutor.AdvanceTick(state, state.Event.Tick + 1 == circleAt, [], 8));
        return state;
    }
    private static void Rejected(ReadySceneState original, ReadySceneResult result, BannerSceneRejection reason,
        SceneTaskRejection taskReason = SceneTaskRejection.None)
    {
        Assert.Same(original, result.State);
        Assert.Equal(reason, result.Rejection);
        Assert.Equal(taskReason, result.TaskRejection);
        Assert.False(result.Accepted);
        Assert.NotNull(result.Diagnostic);
        Assert.Empty(result.Trace);
        Assert.Equal((0, 0, 0, 0), (result.TicksConsumed, result.InstructionsExecuted, result.AcceptedTaskUpdates, result.AcceptedEffectTicks));
    }
    private static SceneTaskState Ledger(long tick, long sequence, ImmutableArray<SceneTask> tasks, SceneBarrier? barrier = null)
        => (SceneTaskState)Activator.CreateInstance(typeof(SceneTaskState), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [Scene, tick, sequence, tasks, barrier], null)!;
    private static ReadySceneState ReplaceLedger(ReadySceneState state, SceneTaskState tasks)
    {
        var inner = typeof(SceneEventState).GetProperty("Inner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(state.Event);
        var sceneEvent = (SceneEventState)Activator.CreateInstance(typeof(SceneEventState), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [state.Event.Program, inner, tasks, state.Event.ConsumerId, state.Event.Suspension,
                state.Event.RemainingWaitTicks, state.Event.WaitingAtOffset], null)!;
        return (ReadySceneState)Activator.CreateInstance(typeof(ReadySceneState), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [state.Program, sceneEvent, state.Ready, state.Binding], null)!;
    }
    private static string Snapshot(object value) => JsonSerializer.Serialize(value);

    public static IEnumerable<object[]> ContractCases()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ready-display-contract-v1.json")));
        Assert.Equal("11464943bbdbd2a0af022d90cb7f10455cf911df", doc.RootElement.GetProperty("referenceRevision").GetString());
        var cases = doc.RootElement.GetProperty("cases").EnumerateArray().Where(c => c.GetProperty("layer").GetString() == "Core (ZOD-084)").ToArray();
        Assert.Equal(Enumerable.Range(1, 28).Select(i => $"RDY-{i:000}"), cases.Select(c => c.GetProperty("id").GetString()));
        foreach (var c in cases) yield return [c.GetProperty("id").GetString()!, c.Clone()];
    }

    [Theory, MemberData(nameof(ContractCases))]
    public void Independently_authored_Core_expectations_execute(string id, JsonElement contract)
    {
        var expected = contract.GetProperty("expected");
        switch (id)
        {
            case "RDY-001": case "RDY-002": case "RDY-003": case "RDY-004":
            case "RDY-005": case "RDY-006": case "RDY-007": case "RDY-008":
            case "RDY-009": case "RDY-010": case "RDY-011": case "RDY-012": case "RDY-013":
                Timing(id, contract); break;
            case "RDY-014":
            {
                var state = Through(Waiting(0), 8);
                Assert.True(state.Ready!.ChildIsTerminal);
                Assert.False(state.Ready.IsTerminal);
                Assert.Equal(expected.GetProperty("namedManagedTasks").GetInt32(), state.Event.Tasks.Tasks.Length);
                Assert.Equal(expected.GetProperty("dispatcherTaskClass").GetInt32(), (int)state.Event.Tasks.Tasks[0].ActiveClass!);
                Assert.Equal(expected.GetProperty("dispatcherRevisionAt8").GetInt64(), state.Event.Tasks.Tasks[0].Revision);
                Assert.Equal(expected.GetProperty("barrierAt8").GetString(), state.Event.Tasks.BarrierStatus.ToString());
                Assert.Equal(state.Ready.TaskId, state.Ready.ChildId.DispatcherTaskId);
                break;
            }
            case "RDY-015":
            {
                var state = Through(Waiting(0), 9);
                Assert.Equal(expected.GetProperty("terminalRevisionAt9").GetInt64(), state.Event.Tasks.Tasks[0].Revision);
                Assert.Equal(expected.GetProperty("barrierAt9").GetString(), state.Event.Tasks.BarrierStatus.ToString());
                Assert.True(Through(state, expected.GetProperty("barrierReleasedAt").GetInt64()).Event.Exited);
                break;
            }
            case "RDY-016":
            {
                var tasks = SceneTaskLedger.ApplyUpdates(SceneTaskState.Create(Scene), [new SceneTaskUpdate.Start(SceneTaskFamily.ConditionDisplay, Other)]).State;
                var state = Through(Waiting(tasks: tasks), 69);
                Assert.True(state.Ready!.IsTerminal);
                state = Accepted(ReadySceneExecutor.AdvanceTick(state, false, [new SceneTaskUpdate.Complete(tasks.Tasks[0].Id, 0)], 8));
                Assert.False(state.Event.Exited);
                Assert.True(Through(state, expected.GetProperty("parentResumedAt").GetInt64()).Event.Exited);
                break;
            }
            case "RDY-017":
            {
                var state = Through(Waiting(), 69);
                state = Accepted(ReadySceneExecutor.AdvanceTick(state, false, [new SceneTaskUpdate.Start(SceneTaskFamily.ConditionDisplay, Other)], 8));
                Assert.Equal(expected.GetProperty("parentExitedAt").GetInt64(), state.Event.Tick);
                Assert.True(state.Event.Exited);
                Assert.False(state.Event.Tasks.Tasks[^1].IsTerminal);
                break;
            }
            case "RDY-018":
            {
                var state = Accepted(ReadySceneExecutor.RunUntilBlocked(Fresh(program: Program(Frames(1, 1), Display(4, 0), Barrier(), Exit())), 1));
                var birth = ReadySceneExecutor.AdvanceTick(state, true, [], 2, 1);
                state = Accepted(birth);
                Assert.Equal(0, birth.AcceptedEffectTicks);
                Assert.Equal(expected.GetProperty("startTick").GetInt64(), state.Ready!.StartTick);
                Assert.Equal(expected.GetProperty("scaleAt1").GetInt32(), state.Ready.DrawScale);
                state = Through(state, 2);
                Assert.Equal(expected.GetProperty("scaleAt2").GetInt32(), state.Ready!.DrawScale);
                state = Through(state, expected.GetProperty("childClearedAt").GetInt64());
                Assert.True(state.Ready!.ChildIsTerminal);
                Assert.False(state.Ready.IsTerminal);
                state = Through(state, expected.GetProperty("dispatcherClearedAt").GetInt64());
                Assert.True(state.Ready!.IsTerminal);
                Assert.False(state.Event.Exited);
                Assert.True(Through(state, expected.GetProperty("parentResumedAt").GetInt64()).Event.Exited);
                break;
            }
            case "RDY-019":
                foreach (var binding in new PreloadedBannerBinding?[] { null, Binding with { DefinitionId = Other }, Binding with { ArtworkId = Other }, Binding with { OwnerId = Other } })
                {
                    var state = Program(Display(), Barrier(), Exit()).Start(default, SceneTaskState.Create(Scene), Consumer, binding);
                    Rejected(state, ReadySceneExecutor.RunUntilBlocked(state, 8), BannerSceneRejection.InvalidManagedTaskBinding);
                    Assert.Empty(state.Event.Tasks.Tasks);
                }
                break;
            case "RDY-020":
                foreach (int tick in new[] { 68, 69 })
                {
                    var state = Through(Waiting(), tick);
                    foreach (var update in new SceneTaskUpdate[] { new SceneTaskUpdate.Phase(state.Ready!.TaskId, 0, SceneTaskPhase.Holding),
                        new SceneTaskUpdate.Complete(state.Ready.TaskId, 0), new SceneTaskUpdate.Cancel(state.Ready.TaskId, 0, SceneTaskCancellationReason.OwnerAcknowledgedCancellation) })
                        Rejected(state, ReadySceneExecutor.AdvanceTick(state, false, [update], 8), BannerSceneRejection.ManagedTaskUpdateForbidden);
                }
                break;
            case "RDY-021":
            {
                var state = Fresh(program: Program(Display(), Op(5, ConditionalEventOperation.AddImmediate, 0, 1), Barrier(), Exit()), variables: new(65535, 0, 0, 0));
                Rejected(state, ReadySceneExecutor.RunUntilBlocked(state, 8), BannerSceneRejection.ArithmeticDomainExceeded);
                state = Fresh();
                Rejected(state, ReadySceneExecutor.RunUntilBlocked(state, 1), BannerSceneRejection.InstructionBudgetExceeded);
                break;
            }
            case "RDY-022":
            {
                var state = Through(Waiting(), 68);
                var updates = Enumerable.Range(0, 32).SelectMany(i => new SceneTaskUpdate[] {
                    new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Other), new SceneTaskUpdate.Complete(new(Scene, i + 2), 0) }).ToImmutableArray();
                var before = Snapshot(state);
                Rejected(state, ReadySceneExecutor.AdvanceTick(state, false, updates, 8), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.UpdateBudgetExceeded);
                Assert.Equal(before, Snapshot(state));
                break;
            }
            case "RDY-023":
            {
                var state = Accepted(ReadySceneExecutor.RunUntilBlocked(Fresh(0, Program(Display(hold: 0), Exit())), 2));
                var ready = state.Ready;
                state = Accepted(ReadySceneExecutor.StartNextEvent(state, Program(Barrier(), Exit()), default, Other));
                Assert.Same(ready, state.Ready);
                Assert.Equal(0, state.Event.Tick);
                state = Accepted(ReadySceneExecutor.RunUntilBlocked(state, 2));
                state = Through(state, 10);
                Assert.Equal(ready!.TaskId, state.Ready!.TaskId);
                state = Accepted(ReadySceneExecutor.StartNextEvent(state, Program(Display(hold: 0), Exit()), default, Consumer));
                Rejected(state, ReadySceneExecutor.RunUntilBlocked(state, 2), BannerSceneRejection.EffectAlreadyAttached);
                break;
            }
            case "RDY-024":
                Assert.Equal(Replay(0), Replay(1)); Assert.Equal(Replay(1), Replay(9)); break;
            case "RDY-025":
                OverflowAndCapacity(); break;
            case "RDY-026":
            {
                var state = Waiting();
                state = Accepted(ReadySceneExecutor.AdvanceTick(state, false, [], 8, cancellation: new(state.Ready!.TaskId, 0)));
                Assert.Equal(ReadyChildPhase.Cancelled, state.Ready!.ChildPhase);
                Assert.Equal(ReadyDispatcherPhase.Cancelled, state.Ready.DispatcherPhase);
                Assert.Equal(expected.GetProperty("releaseCause").GetString(), state.Ready.ExitCause.ToString());
                Assert.False(state.Event.Exited);
                Assert.True(Through(state, expected.GetProperty("parentResumedAt").GetInt64()).Event.Exited);
                break;
            }
            case "RDY-027":
            {
                var state = Waiting();
                Rejected(state, ReadySceneExecutor.AdvanceTick(state, null, [], 8), BannerSceneRejection.MissingCircleSample);
                Assert.Equal(new[] { "AdvanceTick", "RunUntilBlocked", "StartNextEvent" }, typeof(ReadySceneExecutor).GetMethods(BindingFlags.Public | BindingFlags.Static).Select(m => m.Name).Order());
                Assert.DoesNotContain(typeof(ReadySceneExecutor).GetMethods().SelectMany(m => m.GetParameters()), p => typeof(Delegate).IsAssignableFrom(p.ParameterType));
                break;
            }
            case "RDY-028":
            {
                var birth = ReadySceneExecutor.RunUntilBlocked(Fresh(1), 2, 1);
                Ordered(birth, expected.GetProperty("birthOrder"));
                var release = ReadySceneExecutor.AdvanceTick(Accepted(birth), false, [], 8);
                Ordered(release, expected.GetProperty("releaseOrder"));
                var child = ReadySceneExecutor.AdvanceTick(Through(Accepted(release), 8), false, [], 8);
                Ordered(child, expected.GetProperty("childClearOrder"));
                var parent = ReadySceneExecutor.AdvanceTick(Accepted(child), false, [], 8);
                Ordered(parent, expected.GetProperty("dispatcherClearOrder"));
                break;
            }
            default: throw new InvalidOperationException($"Unimplemented contract {id}");
        }
    }

    private static void Timing(string id, JsonElement contract)
    {
        int hold = contract.GetProperty("given").GetProperty("hold").GetInt32();
        int? circle = id switch { "RDY-006" => 1, "RDY-007" => 5, "RDY-008" => 60, "RDY-010" => 1, "RDY-011" => 2, _ => null };
        var expected = contract.GetProperty("expected");
        var states = new List<ReadySceneState> { Waiting(hold) };
        while (!states[^1].Event.Exited) states.Add(Through(states[^1], states[^1].Event.Tick + 1, circle));
        if (expected.TryGetProperty("initialScale", out var initial)) Assert.Equal(initial.GetInt32(), states[0].Ready!.DrawScale);
        if (expected.TryGetProperty("initialPhase", out var phase)) Assert.Equal(phase.GetString(), states[0].Ready!.ChildPhase.ToString());
        if (expected.TryGetProperty("inputSamplesDuringWait", out var samples)) Assert.Equal(samples.GetInt32(), states[^1].Ready!.WaitSamplesTaken);
        if (expected.TryGetProperty("scales", out var scales)) Assert.Equal(scales.EnumerateArray().Select(s => (int?)s.GetInt32()), states.Take(11).Select(s => s.Ready!.DrawScale));
        foreach (var property in expected.EnumerateObject().Where(p => p.Name.StartsWith("scaleAt", StringComparison.Ordinal)))
            Assert.Equal(property.Value.GetInt32(), states[int.Parse(property.Name[7..])].Ready!.DrawScale);
        if (expected.TryGetProperty("firstFullHoldDrawAt", out var full))
            Assert.Equal(full.GetInt64(), states.First(s => s.Ready!.DrawScale == 128).Event.Tick);
        if (expected.TryGetProperty("releaseAt", out var release)) Assert.Equal(release.GetInt64(), states[^1].Ready!.ReleaseTick);
        if (expected.TryGetProperty("releaseCause", out var cause)) Assert.Equal(cause.GetString(), states[^1].Ready!.ExitCause.ToString());
        foreach (var (key, tick) in new[] { ("childClearedAt", states.First(s => s.Ready!.ChildIsTerminal).Event.Tick),
            ("dispatcherClearedAt", states.First(s => s.Ready!.IsTerminal).Event.Tick), ("parentResumedAt", states[^1].Event.Tick) })
            if (expected.TryGetProperty(key, out var value)) Assert.Equal(value.GetInt64(), tick);
        Assert.Equal(0, states[0].Ready!.WaitSamplesTaken);
        if (id == "RDY-012") Assert.DoesNotContain(states.Take(5), s => s.Ready!.DrawScale == 120);
        if (id == "RDY-013")
        {
            int last = expected.GetProperty("lastFadeDrawAt").GetInt32();
            Assert.Equal(expected.GetProperty("lastFadeScale").GetInt32(), states[last].Ready!.DrawScale);
            Assert.All(states.Skip(expected.GetProperty("noDrawFrom").GetInt32()), s => Assert.Null(s.Ready!.DrawScale));
            Assert.False(states[8].Ready!.ChildIsTerminal);
            Assert.False(states[9].Ready!.IsTerminal);
        }
    }

    private static string Replay(int reads)
    {
        var state = Fresh();
        var results = new List<string>();
        var run = ReadySceneExecutor.RunUntilBlocked(state, 2, 1);
        state = Accepted(run);
        results.Add(Snapshot(run));
        var initial = state.Ready!;
        string before = Snapshot(initial);
        while (!state.Event.Exited)
        {
            for (int i = 0; i < reads; i++) _ = Snapshot(state.Ready!);
            run = ReadySceneExecutor.AdvanceTick(state, state.Event.Tick == 4, [], 8);
            state = Accepted(run);
            results.Add(Snapshot(run));
        }
        Assert.Equal(before, Snapshot(initial));
        var terminal = state.Ready;
        state = Through(state, state.Event.Tick + 3);
        Assert.Same(terminal, state.Ready);
        return string.Join('\n', results);
    }
    private static void Ordered(ReadySceneResult result, JsonElement expected)
    {
        Assert.True(result.Accepted, result.Diagnostic);
        var names = result.Trace.Select(t => t.Kind == ReadySceneTraceKind.Event && t.EventTrace?.TaskTrace?.Kind == SceneTaskTraceKind.BarrierPolled
            ? "EventPoll" : t.Kind.ToString()).ToArray();
        int previous = -1;
        foreach (var name in expected.EnumerateArray())
        {
            int index = Array.IndexOf(names, name.GetString(), previous + 1);
            Assert.True(index > previous, $"Missing ordered {name}: {string.Join(',', names)}");
            previous = index;
        }
    }
    private static void OverflowAndCapacity()
    {
        var state = Fresh(tasks: Ledger(0, long.MaxValue, []));
        Rejected(state, ReadySceneExecutor.RunUntilBlocked(state, 2), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.CounterOverflow);
        state = Accepted(ReadySceneExecutor.RunUntilBlocked(Fresh(program: Program(Frames(1, 1), Exit())), 1));
        state = ReplaceLedger(state, Ledger(long.MaxValue, 1, []));
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, false, [], 8), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.CounterOverflow);
        var tasks = Enumerable.Range(1, 64).Select(i => new SceneTask(new(Scene, i), Other, SceneTaskFamily.Camera, SceneTaskPhase.Running, 0)).ToImmutableArray();
        state = Fresh(tasks: Ledger(0, 65, tasks));
        Rejected(state, ReadySceneExecutor.RunUntilBlocked(state, 2), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.LiveTaskLimitExceeded);
        tasks = Enumerable.Range(1, 256).Select(i => new SceneTask(new(Scene, i), Other, SceneTaskFamily.Camera, SceneTaskPhase.Completed, 1)).ToImmutableArray();
        state = Fresh(tasks: Ledger(0, 257, tasks));
        Rejected(state, ReadySceneExecutor.RunUntilBlocked(state, 2), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.TotalTaskLimitExceeded);
        var bad = new SceneTask(new(Scene, 1), Other, SceneTaskFamily.Camera, SceneTaskPhase.Running, long.MaxValue);
        state = Waiting(tasks: Ledger(0, 2, [bad]));
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, false, [new SceneTaskUpdate.Complete(bad.Id, long.MaxValue)], 8), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.CounterOverflow);
    }

    [Theory]
    [InlineData(-1, 0), InlineData(256, 0), InlineData(60, 1), InlineData(60, 8)]
    public void Unsupported_definitions_are_rejected_even_unreachable(int hold, int condition)
        => Assert.Throws<ArgumentException>(() => Program(Exit(1), new ReadySceneInstruction(4, new(new(Definition, condition, hold), Art, Owner))));

    [Fact]
    public void Duplicate_admissions_are_rejected_and_branch_skips_allocate_nothing()
    {
        Assert.Throws<ArgumentException>(() => Program(Display(), Exit(5), Display(7)));
        var state = Fresh(program: Program(Op(1, ConditionalEventOperation.JumpForward, target: 10), Display(), Exit()));
        state = Accepted(ReadySceneExecutor.RunUntilBlocked(state, 2));
        Assert.Null(state.Ready);
        Assert.Empty(state.Event.Tasks.Tasks);
    }

    [Theory]
    [InlineData(-1), InlineData(8193)]
    public void Instruction_budget_domain_is_checked(int budget)
    {
        var state = Fresh();
        Rejected(state, ReadySceneExecutor.RunUntilBlocked(state, budget), BannerSceneRejection.InvalidInstructionBudget);
    }
    [Theory]
    [InlineData(-1), InlineData(65)]
    public void Update_budget_domain_is_checked(int budget)
    {
        var state = Waiting();
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, false, [], 8, budget), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.InvalidUpdateBudget);
    }
    [Fact]
    public void Zero_budgets_reject_start_and_terminal_but_allow_nonterminal_ticks()
    {
        var state = Fresh();
        Rejected(state, ReadySceneExecutor.RunUntilBlocked(state, 0), BannerSceneRejection.InstructionBudgetExceeded);
        Rejected(state, ReadySceneExecutor.RunUntilBlocked(state, 2, 0), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.UpdateBudgetExceeded);
        state = Waiting(0);
        state = Accepted(ReadySceneExecutor.AdvanceTick(state, false, [], 0, 0));
        state = Through(state, 8);
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, false, [], 0, 0), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.UpdateBudgetExceeded);
        state = Through(state, 9);
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, false, [], 0), BannerSceneRejection.InstructionBudgetExceeded);
    }
    [Fact]
    public void Uninitialized_null_unknown_and_wrong_scene_updates_are_atomic()
    {
        var state = Waiting();
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, false, default, 8), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.InvalidUpdateBatch);
        foreach (var (update, reason) in new (SceneTaskUpdate, SceneTaskRejection)[] {
            (null!, SceneTaskRejection.InvalidUpdate), (new SceneTaskUpdate.Complete(new(Scene, 99), 0), SceneTaskRejection.UnknownTask),
            (new SceneTaskUpdate.Complete(new(Other, 1), 0), SceneTaskRejection.WrongScene) })
            Rejected(state, ReadySceneExecutor.AdvanceTick(state, false, [update], 8), BannerSceneRejection.TaskLedgerRejected, reason);
    }
    [Fact]
    public void Resumed_birth_rolls_back_for_predicted_identity_or_cumulative_budget_failure()
    {
        var state = Accepted(ReadySceneExecutor.RunUntilBlocked(Fresh(program: Program(Frames(1, 1), Display(), Barrier(), Exit())), 1));
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, true, [new SceneTaskUpdate.Complete(new(Scene, 1), 0)], 2), BannerSceneRejection.ManagedTaskUpdateForbidden);
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, true, [new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Other)], 2, 1), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.UpdateBudgetExceeded);
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, true, [], 1), BannerSceneRejection.InstructionBudgetExceeded);
        var result = ReadySceneExecutor.AdvanceTick(state, true, [], 2);
        Assert.Equal(48, Accepted(result).Ready!.DrawScale);
        Assert.Equal(0, result.AcceptedEffectTicks);
        Assert.Equal((2, 1, 1), (result.InstructionsExecuted, result.AcceptedTaskUpdates, result.TicksConsumed));
    }
    [Fact]
    public void Late_failure_after_event_release_rolls_back_entire_tick()
    {
        var state = Through(Waiting(0), 9);
        var before = Snapshot(state);
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, false, [new SceneTaskUpdate.Complete(new(Scene, 99), 0)], 8), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.UnknownTask);
        Assert.Equal(before, Snapshot(state));
    }
    [Theory]
    [InlineData(0), InlineData(1), InlineData(2), InlineData(3), InlineData(4)]
    public void Cancellation_rejects_malformed_wrong_scene_unknown_and_stale(int variant)
    {
        var state = Waiting();
        var (cancel, reason) = variant switch {
            0 => (new ReadyCancellation(new(default, 1), 0), SceneTaskRejection.InvalidId),
            1 => (new ReadyCancellation(new(Scene, 0), 0), SceneTaskRejection.InvalidUpdate),
            2 => (new ReadyCancellation(new(Other, 1), 0), SceneTaskRejection.WrongScene),
            3 => (new ReadyCancellation(new(Scene, 99), 0), SceneTaskRejection.UnknownTask),
            _ => (new ReadyCancellation(state.Ready!.TaskId, 1), SceneTaskRejection.StaleTaskRevision) };
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, false, [], 8, cancellation: cancel), BannerSceneRejection.TaskLedgerRejected, reason);
    }
    [Fact]
    public void Cancellation_preserves_completed_child_and_rejects_terminal_dispatcher()
    {
        var state = Through(Waiting(0), 8);
        state = Accepted(ReadySceneExecutor.AdvanceTick(state, false, [], 8, cancellation: new(state.Ready!.TaskId, 0)));
        Assert.Equal(ReadyChildPhase.Completed, state.Ready!.ChildPhase);
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, false, [], 8, cancellation: new(state.Ready.TaskId, 1)), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.DuplicateTerminalUpdate);
    }
    [Fact]
    public void Public_command_boundaries_and_restart_failures_are_explicit()
    {
        var state = Fresh();
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, false, [], 8), BannerSceneRejection.NotWaiting);
        Rejected(state, ReadySceneExecutor.StartNextEvent(state, Program(Exit()), default, Other), BannerSceneRejection.EventNotExited);
        state = Waiting(0);
        Rejected(state, ReadySceneExecutor.RunUntilBlocked(state, 8), BannerSceneRejection.AwaitingTick);
        state = Through(state, 10);
        Rejected(state, ReadySceneExecutor.RunUntilBlocked(state, 8), BannerSceneRejection.AlreadyExited);
        Rejected(state, ReadySceneExecutor.StartNextEvent(state, null, default, Other), BannerSceneRejection.InvalidProgram);
        Rejected(state, ReadySceneExecutor.StartNextEvent(state, Program(Exit()), new(-1, 0, 0, 0), Other), BannerSceneRejection.InvalidVariables);
        Rejected(state, ReadySceneExecutor.StartNextEvent(state, Program(Exit()), default, Consumer), BannerSceneRejection.InvalidId);
    }
    [Fact]
    public void Admission_capacity_is_checked_before_later_facts_can_free_a_slot()
    {
        var tasks = Enumerable.Range(1, 64).Select(i => new SceneTask(new(Scene, i), Other, SceneTaskFamily.Camera, SceneTaskPhase.Running, 0)).ToImmutableArray();
        var state = Accepted(ReadySceneExecutor.RunUntilBlocked(Fresh(program: Program(Frames(1, 1), Display(), Barrier(), Exit()), tasks: Ledger(0, 65, tasks)), 1));
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, false, [new SceneTaskUpdate.Complete(tasks[0].Id, 0)], 8),
            BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.LiveTaskLimitExceeded);
    }
    [Fact]
    public void Resumed_admission_arithmetic_failure_and_late_cancellation_failure_roll_back()
    {
        var state = Accepted(ReadySceneExecutor.RunUntilBlocked(Fresh(program: Program(Frames(1, 1), Display(),
            Op(5, ConditionalEventOperation.AddImmediate, 0, 1), Barrier(), Exit()), variables: new(65535, 0, 0, 0)), 1));
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, true, [], 8), BannerSceneRejection.ArithmeticDomainExceeded);
        state = Waiting();
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, false, [], 8, 0, new(state.Ready!.TaskId, 0)),
            BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.UpdateBudgetExceeded);
    }
    [Fact]
    public void Cancellation_cannot_address_other_tasks_or_an_unborn_aggregate()
    {
        var tasks = SceneTaskLedger.ApplyUpdates(SceneTaskState.Create(Scene), [new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Other)]).State;
        var state = Waiting(tasks: tasks);
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, false, [], 8, cancellation: new(tasks.Tasks[0].Id, 0)), BannerSceneRejection.InvalidManagedTaskBinding);
        state = Accepted(ReadySceneExecutor.RunUntilBlocked(Fresh(program: Program(Frames(1, 1), Display(), Barrier(), Exit())), 1));
        Rejected(state, ReadySceneExecutor.AdvanceTick(state, false, [], 8, cancellation: new(new(Scene, 1), 0)), BannerSceneRejection.EffectNotAttached);
    }
    [Fact]
    public void Invalid_logical_ids_and_unknown_operations_are_rejected_before_execution()
    {
        foreach (var display in new[] { new DisplayReady(new(default, 0, 60), Art, Owner),
            new DisplayReady(new(Definition, 0, 60), default, Owner), new DisplayReady(new(Definition, 0, 60), Art, default) })
            Assert.Throws<ArgumentException>(() => Program(Exit(1), new ReadySceneInstruction(4, display)));
        Assert.Throws<ArgumentException>(() => Program(Exit(1), Op(4, (ConditionalEventOperation)999)));
    }
    [Fact]
    public void Binding_checks_reject_corrupted_clock_owner_family_and_revision()
    {
        var state = Waiting();
        var task = state.Event.Tasks.Tasks[0];
        foreach (var changed in new[] { task with { OwnerId = Other }, task with { Family = SceneTaskFamily.Camera }, task with { Revision = 1 } })
        {
            var bad = ReplaceLedger(state, Ledger(0, 2, [changed], state.Event.Tasks.Barrier));
            Rejected(bad, ReadySceneExecutor.AdvanceTick(bad, false, [], 8), BannerSceneRejection.InvalidManagedTaskBinding);
        }
        var clock = ReplaceLedger(state, Ledger(1, 2, [task], state.Event.Tasks.Barrier));
        Rejected(clock, ReadySceneExecutor.AdvanceTick(clock, false, [], 8), BannerSceneRejection.InvalidManagedTaskBinding);
    }
}
