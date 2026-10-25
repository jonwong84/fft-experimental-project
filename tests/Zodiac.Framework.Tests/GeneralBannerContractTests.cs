using System.Collections.Immutable;
using System.Text.Json;
using Zodiac.Framework.Events;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class GeneralBannerContractTests
{
    private static readonly AssetId Scene = new("sample:scene/banner-a");
    private static readonly AssetId Owner = new("sample:owner/a");
    private static readonly AssetId Consumer = new("sample:consumer/a");
    private static readonly AssetId DefinitionId = new("sample:effect/general-banner-v1");
    private static readonly AssetId ProgramId = new("sample:event/banner-v1");

    public static IEnumerable<object[]> Cases()
    {
        using var document = Load();
        foreach (var item in document.RootElement.GetProperty("cases").EnumerateArray())
            yield return [item.GetProperty("id").GetString()!];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Preimplementation_cases_have_full_snapshot_trace_failure_and_replay_checks(string id)
    {
        using var document = Load();
        Assert.Equal(24, document.RootElement.GetProperty("cases").GetArrayLength());
        Assert.True(document.RootElement.GetProperty("specificationOnly").GetBoolean());
        Assert.False(document.RootElement.GetProperty("executed").GetBoolean()); // Historical artifact remains classified as authored specification.
        Assert.Equal("11464943bbdbd2a0af022d90cb7f10455cf911df", document.RootElement.GetProperty("referenceRevision").GetString());
        var specification = document.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("id").GetString() == id);
        Assert.Equal(Execute(specification), Execute(specification));
    }

    private static string Execute(JsonElement specification)
    {
        if (specification.TryGetProperty("expectedRejection", out var rejection)) return ExecuteFailure(specification, rejection);
        string id = specification.GetProperty("id").GetString()!;
        int hold = specification.GetProperty("holdFrames").GetInt32();
        bool firstExited = specification.TryGetProperty("initialProgram", out _);
        var state = Setup(hold, firstExited);
        var history = new List<string> { Dump(state) };
        var checkpoints = specification.GetProperty("expected").EnumerateArray().ToArray();
        int seen = Checkpoints(state, null, null, checkpoints, specification, "Setup");
        foreach (var command in specification.GetProperty("history").EnumerateArray())
        {
            string kind = command.GetProperty("command").GetString()!;
            if (kind == "StartNextEvent")
            {
                var input = state;
                string frozen = Dump(input);
                var next = BannerSceneExecutor.StartNextEvent(state, Program(new("sample:event/banner-next")), default,
                    new(command.GetProperty("consumer").GetString()!));
                Assert.True(next.Accepted, next.Diagnostic);
                Assert.Same(input.Banner, next.State.Banner);
                Assert.Same(input.Event.Tasks, next.State.Event.Tasks);
                Assert.Equal(frozen, Dump(input));
                Assert.Equal(new BannerSceneTrace(BannerSceneTraceKind.EventStarted, input.Event.Tick,
                    Before: input.Banner, After: input.Banner, StartedEvent: next.State.Event), Assert.Single(next.Trace));
                state = next.State;
                history.Add(Dump(next));
            }
            else if (kind == "RunUntilBlocked")
            {
                var before = state;
                var result = BannerSceneExecutor.RunUntilBlocked(state, command.GetProperty("instructionBudget").GetInt32());
                Assert.True(result.Accepted, result.Diagnostic);
                Assert.Same(before.Banner, result.State.Banner);
                Assert.Equal(0, result.AcceptedEffectTicks);
                Assert.Equal(0, result.TicksConsumed);
                Assert.Equal(1, result.InstructionsExecuted);
                state = result.State;
                AssertRunTrace(before, result);
                seen += Checkpoints(state, result, command, checkpoints, specification, "Run");
                history.Add(Dump(result));
            }
            else
            {
                Assert.Equal("AdvanceTickRange", kind);
                int first = command.GetProperty("first").GetInt32(), last = command.GetProperty("last").GetInt32();
                for (int tick = first; tick <= last; tick++)
                {
                    var input = state;
                    string frozen = Dump(input);
                    Assert.Equal(tick - 1, input.Event.Tick);
                    var result = Advance(input, command);
                    Assert.True(result.Accepted, result.Diagnostic);
                    Assert.Equal(frozen, Dump(input));
                    Assert.Equal(1, result.TicksConsumed);
                    Assert.Equal(input.Banner!.IsTerminal ? 0 : 1, result.AcceptedEffectTicks);
                    Assert.Null(result.Diagnostic);
                    Assert.Null(result.FailureOffset);
                    Assert.Null(result.FailureUpdateIndex);
                    Assert.Equal(SceneTaskRejection.None, result.TaskRejection);
                    state = result.State;
                    seen += Checkpoints(state, result, command, checkpoints, specification, "Tick");
                    Assert.Equal(SceneTaskTraceKind.TickAdvanced, result.Trace[0].EventTrace!.TaskTrace!.Kind);
                    Assert.Same(input.Banner, result.Trace[1].Before);
                    Assert.Same(state.Banner, result.Trace[1].After);
                    Assert.Equal(command.GetProperty("circlePressed").GetBoolean(), result.Trace[1].CirclePressed);
                    history.Add(Dump(result));
                }
            }
        }
        Assert.Equal(checkpoints.Length, seen);
        return string.Join('\n', history);
    }

    private static int Checkpoints(BannerSceneState state, BannerSceneResult? result, JsonElement? command,
        JsonElement[] checkpoints, JsonElement specification, string stage)
    {
        int seen = 0;
        foreach (var target in checkpoints.Where(p => p.GetProperty("tick").GetInt64() == state.Event.Tick))
        {
            if (target.TryGetProperty("after", out var after))
            {
                if (after.GetString() == "first five ticks" && stage != "Tick") continue;
                if (after.GetString() == "next event RunUntilBlocked" && stage != "Run") continue;
            }
            CheckState(state, target, specification.GetProperty("holdFrames").GetInt32(),
                specification.GetProperty("id").GetString() == "BANNER-MULTI");
            if (result is not null && stage == "Tick") AssertTickTrace(result, command!.Value);
            seen++;
        }
        return seen;
    }

    private static void CheckState(BannerSceneState state, JsonElement expected, int hold, bool multi = false, int variable3 = 0)
    {
        long tick = expected.GetProperty("tick").GetInt64();
        var banner = Assert.IsType<GeneralBannerState>(state.Banner);
        Assert.Equal(new GeneralBannerDefinition(DefinitionId, 8, hold), banner.Definition);
        Assert.Equal(new(Scene, 1), banner.TaskId);
        Assert.Equal(Owner, banner.OwnerId);
        Assert.Equal(0, banner.StartTick);
        Assert.Equal(expected.TryGetProperty("effectLastProcessedTick", out var lastTick) ? lastTick.GetInt64() : tick, banner.LastProcessedTick);
        Assert.Equal(Enum.Parse<GeneralBannerPhase>(expected.GetProperty("effectPhase").GetString()!), banner.Phase);
        Assert.Equal(expected.GetProperty("revealCompleted").GetInt32(), banner.RevealCompleted);
        Assert.Equal(expected.GetProperty("holdCompleted").GetInt32(), banner.HoldCompleted);
        Assert.Equal(expected.GetProperty("hideCompleted").GetInt32(), banner.HideCompleted);
        var cause = expected.GetProperty("exitCause");
        Assert.Equal(cause.ValueKind == JsonValueKind.Null ? null : Enum.Parse<GeneralBannerExitCause>(cause.GetString()!), banner.ExitCause);
        var taskPhase = Enum.Parse<SceneTaskPhase>(expected.GetProperty("managedTaskPhase").GetString()!);
        Assert.Equal(taskPhase is SceneTaskPhase.Completed or SceneTaskPhase.Cancelled, banner.IsTerminal);
        var task = new SceneTask(new(Scene, 1), Owner, SceneTaskFamily.ConditionDisplay, taskPhase,
            expected.GetProperty("managedTaskRevision").GetInt64(), taskPhase == SceneTaskPhase.Cancelled ? SceneTaskCancellationReason.OwnerAcknowledgedCancellation : null);
        SceneTask[] tasks = multi ? [task, new(new(Scene, 2), new("sample:owner/b"), SceneTaskFamily.ConditionDisplay,
            tick == 32 ? SceneTaskPhase.Completed : SceneTaskPhase.Running, tick == 32 ? 1 : 0)] : [task];
        Assert.Equal(tasks, state.Event.Tasks.Tasks.ToArray());
        var active = expected.GetProperty("managedTaskClass");
        Assert.Equal(active.ValueKind == JsonValueKind.Null ? null : (SceneTaskClass)active.GetInt32(), state.Event.Tasks.Tasks[0].ActiveClass);
        Assert.Equal(tasks.Length, state.Event.Tasks.TotalStarted);
        Assert.Equal(tasks.Length + 1, state.Event.Tasks.NextSequence);
        Assert.Equal(tasks.Count(t => !t.IsTerminal), state.Event.Tasks.LiveTasks);
        Assert.Equal(Scene, state.Event.Tasks.SceneRunId);
        Assert.Equal(tick, state.Event.Tick);
        Assert.Equal(new(0, 0, 0, variable3), state.Event.Variables);
        Assert.Equal(0, state.Event.RemainingWaitTicks);
        string vm = expected.GetProperty("vm").GetString()!;
        Assert.Equal(vm == "Exited", state.Event.Exited);
        Assert.Equal(vm == "Exited" ? SceneEventSuspension.Ready : SceneEventSuspension.Barrier, state.Event.Suspension);
        bool firstExited = vm == "Exited" && expected.GetProperty("effectPhase").GetString() == "Revealing";
        Assert.Equal(firstExited ? 4 : 7, state.Event.Offset);
        Assert.Equal(vm == "Exited" ? null : (int?)4, state.Event.WaitingAtOffset);
        bool restarted = state.Event.ConsumerId == new AssetId("sample:consumer/b");
        Assert.Equal(restarted ? new AssetId("sample:event/banner-next") : ProgramId, state.Event.Program.Id);
        Assert.Equal(restarted ? new AssetId("sample:consumer/b") : Consumer, state.Event.ConsumerId);
        var barrier = firstExited ? null : new SceneBarrier(vm == "Exited" ? SceneBarrierStatus.Released : SceneBarrierStatus.Pending,
            SceneTaskClass.ConditionDisplay, state.Event.ConsumerId, 4, state.Event.Program.Id, restarted ? 5 : 0);
        Assert.Equal(barrier, state.Event.Tasks.Barrier);
        Assert.Equal(firstExited ? SceneBarrierStatus.Ready : barrier!.Status, state.Event.Tasks.BarrierStatus);
    }

    private static void AssertTickTrace(BannerSceneResult result, JsonElement command)
    {
        // The checkpoint already validates every output-state field against the authored JSON.
        // Trace expectations bind those independently checked values to the frozen input and literal source ordering.
        var after = result.State.Banner!;
        var before = result.Trace[1].Before!;
        long tick = result.State.Event.Tick;
        bool previouslyExited = result.State.Event.Offset == 4;
        bool multi = result.State.Event.Tasks.TotalStarted == 2;
        int updates = command.TryGetProperty("unmanagedUpdates", out var supplied) ? supplied.GetArrayLength() : 0;
        bool terminalNow = !before.IsTerminal && after.IsTerminal;
        int instructions = result.State.Event.Exited && !previouslyExited ? 1 : 0;
        Assert.Equal(updates + (terminalNow ? 1 : 0), result.AcceptedTaskUpdates);
        Assert.Equal(instructions, result.InstructionsExecuted);
        var values = new ConditionalEventVariables(0, 0, 0, 0);
        int offset = 4, next = previouslyExited ? 4 : 7;
        var pending = new SceneBarrier(SceneBarrierStatus.Pending, SceneTaskClass.ConditionDisplay,
            result.State.Event.ConsumerId, 4, result.State.Event.Program.Id, result.State.Event.ConsumerId == Consumer ? 0 : 5);
        BannerSceneTrace Wrap(SceneTaskTrace fact) => new(BannerSceneTraceKind.Event, tick,
            EventTrace: new(SceneEventTraceKind.TaskLedger, tick, offset, values, values, next, 0, previouslyExited, TaskTrace: fact),
            InstructionBudget: 1, UpdateBudget: 64);
        var expected = new List<BannerSceneTrace>
        {
            Wrap(new(SceneTaskTraceKind.TickAdvanced, tick, null, null, null, [])),
            new(before.IsTerminal ? BannerSceneTraceKind.EffectObserved : BannerSceneTraceKind.EffectAdvanced,
                tick, Before: before, After: after, CirclePressed: command.GetProperty("circlePressed").GetBoolean(),
                Cancellation: Cancel(command), InstructionBudget: 1, UpdateBudget: 64),
        };
        if (updates != 0)
        {
            var other = new SceneTask(new(Scene, 2), new("sample:owner/b"), SceneTaskFamily.ConditionDisplay, SceneTaskPhase.Running, 0);
            expected.Add(supplied[0].GetProperty("update").GetString() == "Start"
                ? Wrap(new(SceneTaskTraceKind.TaskStarted, tick, null, other, null, []))
                : Wrap(new(SceneTaskTraceKind.TaskCompleted, tick, other, other with { Phase = SceneTaskPhase.Completed, Revision = 1 }, null, [])));
        }
        if (terminalNow)
        {
            var running = new SceneTask(new(Scene, 1), Owner, SceneTaskFamily.ConditionDisplay, SceneTaskPhase.Running, 0);
            expected.Add(Wrap(new(after.Phase == GeneralBannerPhase.Cancelled ? SceneTaskTraceKind.TaskCancelled : SceneTaskTraceKind.TaskCompleted,
                tick, running, result.State.Event.Tasks.Tasks[0], null, [])));
        }
        if (!previouslyExited)
        {
            ImmutableArray<SceneTaskId> matches = result.State.Event.Exited ? [] : multi
                ? after.IsTerminal ? [new(Scene, 2)] : [new(Scene, 1), new(Scene, 2)] : [new(Scene, 1)];
            expected.Add(Wrap(new(SceneTaskTraceKind.BarrierPolled, tick, null, null, pending, matches)));
            if (matches.IsEmpty)
            {
                expected.Add(Wrap(new(SceneTaskTraceKind.BarrierReleased, tick, null, null, pending with { Status = SceneBarrierStatus.Released }, [])));
                expected.Add(new(BannerSceneTraceKind.Event, tick, EventTrace: new(SceneEventTraceKind.BarrierWaitResumed, tick, 4, values, values, 7, 0, false), InstructionBudget: 1, UpdateBudget: 64));
                expected.Add(new(BannerSceneTraceKind.Event, tick, EventTrace: new(SceneEventTraceKind.Instruction, tick, 7, values, values, 7, 0, true, ConditionalEventOperation.Exit), InstructionBudget: 1, UpdateBudget: 64));
            }
        }
        Assert.Equal(Dump(expected), Dump(result.Trace));
    }

    private static string ExecuteFailure(JsonElement specification, JsonElement rejection)
    {
        var setup = specification.GetProperty("setup");
        int hold = setup.ValueKind == JsonValueKind.String ? 0 : setup.GetProperty("holdFrames").GetInt32();
        bool arithmetic = specification.GetProperty("id").GetString() == "BANNER-CONT-ARITH";
        var state = setup.ValueKind == JsonValueKind.String ? Empty() : Setup(hold, arithmetic: arithmetic);
        if (setup.ValueKind == JsonValueKind.Object)
            for (int tick = 1; tick <= setup.GetProperty("throughTick").GetInt32(); tick++)
            {
                var progressed = BannerSceneExecutor.AdvanceTick(state, false, [], 1);
                Assert.True(progressed.Accepted, progressed.Diagnostic);
                state = progressed.State;
            }
        if (specification.TryGetProperty("preserved", out var preserved)) CheckState(state, preserved, hold, variable3: arithmetic ? 65535 : 0);
        string frozen = Dump(state);
        var command = specification.GetProperty("command");
        var result = command.GetProperty("command").GetString() == "AttachBanner"
            ? BannerSceneExecutor.AttachBanner(state, new(DefinitionId, command.GetProperty("condition").GetInt32(), command.GetProperty("holdFrames").GetInt32()), Owner)
            : Advance(state, command);
        Assert.False(result.Accepted);
        Assert.Equal(Enum.Parse<BannerSceneRejection>(rejection.GetProperty("reason").GetString()!), result.Rejection);
        Assert.Equal(rejection.TryGetProperty("taskReason", out var taskReason) ? Enum.Parse<SceneTaskRejection>(taskReason.GetString()!) : SceneTaskRejection.None, result.TaskRejection);
        Assert.Same(state, result.State);
        Assert.Equal(frozen, Dump(state));
        Assert.NotNull(result.Diagnostic);
        Assert.Empty(result.Trace);
        Assert.Equal(0, result.AcceptedEffectTicks);
        Assert.Equal(0, result.AcceptedTaskUpdates);
        Assert.Equal(0, result.InstructionsExecuted);
        Assert.Equal(0, result.TicksConsumed);
        if (specification.TryGetProperty("failureOffset", out var offset)) Assert.Equal(offset.GetInt32(), result.FailureOffset);
        if (specification.TryGetProperty("nextSequence", out var sequence)) Assert.Equal(sequence.GetInt64(), result.State.Event.Tasks.NextSequence);
        return Dump(result);
    }

    private static BannerSceneState Setup(int hold, bool exited = false, bool arithmetic = false)
    {
        var state = Empty(exited, arithmetic);
        string frozen = Dump(state);
        var attached = BannerSceneExecutor.AttachBanner(state, new(DefinitionId, 8, hold), Owner);
        Assert.True(attached.Accepted);
        Assert.Equal(frozen, Dump(state));
        Assert.Equal(1, attached.AcceptedTaskUpdates);
        Assert.Equal(0, attached.TicksConsumed);
        Assert.Equal(0, attached.AcceptedEffectTicks);
        Assert.Equal(0, attached.InstructionsExecuted);
        var task = new SceneTask(new(Scene, 1), Owner, SceneTaskFamily.ConditionDisplay, SceneTaskPhase.Running, 0);
        Assert.Equal(Dump(new BannerSceneTrace[]
        {
            new(BannerSceneTraceKind.TaskLedger, 0, TaskTrace: new(SceneTaskTraceKind.TaskStarted, 0, null, task, null, []), UpdateBudget: 64),
            new(BannerSceneTraceKind.EffectAttached, 0, After: attached.State.Banner, UpdateBudget: 64),
        }), Dump(attached.Trace));
        var run = BannerSceneExecutor.RunUntilBlocked(attached.State, 1);
        Assert.True(run.Accepted);
        Assert.Same(attached.State.Banner, run.State.Banner);
        if (!exited) AssertRunTrace(attached.State, run);
        return run.State;
    }

    private static void AssertRunTrace(BannerSceneState before, BannerSceneResult result)
    {
        var values = before.Event.Variables;
        var pending = new SceneBarrier(SceneBarrierStatus.Pending, SceneTaskClass.ConditionDisplay, before.Event.ConsumerId, 4, before.Event.Program.Id, before.Event.Tick);
        Assert.Equal(Dump(new BannerSceneTrace[]
        {
            new(BannerSceneTraceKind.Event, before.Event.Tick, EventTrace: new(SceneEventTraceKind.BarrierWaitEntered, before.Event.Tick, 4, values, values, 7, 0, false), InstructionBudget: 1),
            new(BannerSceneTraceKind.Event, before.Event.Tick, EventTrace: new(SceneEventTraceKind.TaskLedger, before.Event.Tick, 4, values, values, 7, 0, false,
                TaskTrace: new(SceneTaskTraceKind.BarrierEntered, before.Event.Tick, null, null, pending, [])), InstructionBudget: 1),
        }), Dump(result.Trace));
    }

    private static BannerSceneState Empty(bool exited = false, bool arithmetic = false)
        => BannerSceneState.Create(Program(ProgramId, exited, arithmetic).Start(new(0, 0, 0, arithmetic ? 65535 : 0), SceneTaskState.Create(Scene), Consumer));
    private static SceneEventProgram Program(AssetId id, bool exited = false, bool arithmetic = false)
        => new(id, exited ? [new(new ConditionalEventInstruction(4, ConditionalEventOperation.Exit))] : arithmetic
            ? [new(4, SceneTaskClass.ConditionDisplay), new(new ConditionalEventInstruction(7, ConditionalEventOperation.AddImmediate, 3, 1)), new(new ConditionalEventInstruction(12, ConditionalEventOperation.Exit))]
            : [new(4, SceneTaskClass.ConditionDisplay), new(new ConditionalEventInstruction(7, ConditionalEventOperation.Exit))]);
    private static BannerSceneResult Advance(BannerSceneState state, JsonElement command)
        => BannerSceneExecutor.AdvanceTick(state, command.GetProperty("circlePressed").GetBoolean(), Updates(command),
            command.TryGetProperty("instructionBudget", out var budget) ? budget.GetInt32() : 1,
            command.TryGetProperty("updateBudget", out var updates) ? updates.GetInt32() : 64, Cancel(command));
    private static ImmutableArray<SceneTaskUpdate> Updates(JsonElement command)
        => command.TryGetProperty("unmanagedUpdates", out var updates) ? updates.EnumerateArray().Select<JsonElement, SceneTaskUpdate>(u =>
            u.GetProperty("update").GetString() == "Start" ? new SceneTaskUpdate.Start(Enum.Parse<SceneTaskFamily>(u.GetProperty("family").GetString()!), new(u.GetProperty("owner").GetString()!))
            : new SceneTaskUpdate.Complete(new(Scene, u.GetProperty("sequence").GetInt64()), u.GetProperty("expectedRevision").GetInt64())).ToImmutableArray() : [];
    private static BannerCancellation? Cancel(JsonElement command)
        => command.TryGetProperty("cancelManaged", out var c) ? new(new(c.TryGetProperty("sceneRunId", out var scene) ? new(scene.GetString()!) : Scene,
            c.GetProperty("sequence").GetInt64()), c.GetProperty("expectedRevision").GetInt64()) : null;
    private static string Dump(object? value) => JsonSerializer.Serialize(value);
    private static JsonDocument Load() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "general-banner-effect-contract-v1.json")));
}
