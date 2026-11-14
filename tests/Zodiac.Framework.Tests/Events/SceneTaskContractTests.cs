using System.Collections.Immutable;
using System.Text.Json;
using Zodiac.Framework.Events;
using Xunit;

namespace Zodiac.Framework.Tests;

/// <summary>Inputs come from the preimplementation ZOD-072 specification. Full snapshots and traces below are authored expectations.</summary>
public sealed class SceneTaskContractTests
{
    private static readonly AssetId Scene = new("sample:scene/a");
    private static readonly AssetId Consumer = new("sample:consumer/event-a");
    private static readonly AssetId Continuation = new("sample:continuation/after-barrier");

    public static IEnumerable<object[]> Cases()
    {
        using var document = Load();
        foreach (var item in document.RootElement.GetProperty("cases").EnumerateArray())
            yield return [item.GetProperty("id").GetString()!];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Independent_specification_has_exact_snapshots_ordered_traces_and_replay(string id)
    {
        using var document = Load();
        var root = document.RootElement;
        Assert.True(root.GetProperty("specificationOnly").GetBoolean()); // Preserve historical evidence classification.
        Assert.Equal("11464943bbdbd2a0af022d90cb7f10455cf911df", root.GetProperty("referenceRevision").GetString());
        Assert.Equal(17, root.GetProperty("cases").GetArrayLength());
        var specification = root.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("id").GetString() == id);
        var expected = Expectations(id);
        var first = Execute(specification, expected);
        var replay = Execute(specification, expected);
        Assert.Equal(first, replay);
    }

    private static string Execute(JsonElement specification, Expected[] expected)
    {
        var state = SceneTaskState.Create(Scene);
        foreach (var seed in specification.GetProperty("initialTasks").EnumerateArray())
        {
            var family = Enum.Parse<SceneTaskFamily>(seed.GetProperty("family").GetString()!);
            var started = SceneTaskLedger.ApplyUpdates(state, [new SceneTaskUpdate.Start(family, new(seed.GetProperty("owner").GetString()!))]);
            Assert.True(started.Accepted);
            state = started.State;
            long sequence = seed.GetProperty("sequence").GetInt64();
            Assert.Equal(sequence, state.Tasks[^1].Id.Sequence);
            var phase = Enum.Parse<SceneTaskPhase>(seed.GetProperty("phase").GetString()!);
            if (phase == SceneTaskPhase.Holding)
                state = SceneTaskLedger.ApplyUpdates(state, [new SceneTaskUpdate.Phase(new(Scene, sequence), 0, phase)]).State;
            if (phase == SceneTaskPhase.Completed)
                state = SceneTaskLedger.ApplyUpdates(state, [new SceneTaskUpdate.Complete(new(Scene, sequence), 0)]).State;
            Assert.Equal(phase, state.Tasks[^1].Phase);
            Assert.Equal(seed.GetProperty("revision").GetInt64(), state.Tasks[^1].Revision);
        }
        var history = new List<string>();
        var commands = specification.GetProperty("commands");
        Assert.Equal(expected.Length, commands.GetArrayLength());
        int index = 0;
        foreach (var command in commands.EnumerateArray())
        {
            var before = state;
            string frozen = JsonSerializer.Serialize(before);
            var result = command.GetProperty("command").GetString() switch
            {
                "BeginBarrier" => SceneTaskLedger.BeginBarrier(state,
                    Enum.Parse<SceneTaskClass>(command.GetProperty("selector").GetString()!),
                    new(command.GetProperty("consumer").GetString()!), command.GetProperty("sourceOffset").GetInt32(), Continuation),
                "AdvanceTick" => SceneTaskLedger.AdvanceTick(state,
                    command.GetProperty("updates").EnumerateArray().Select(ParseUpdate).ToImmutableArray(),
                    command.GetProperty("updateBudget").GetInt32()),
                _ => throw new InvalidDataException("Unexpected specification command."),
            };
            Assert.Equal(frozen, JsonSerializer.Serialize(before));
            var target = expected[index++];
            Assert.Equal(target.Rejection, result.Rejection);
            Assert.Equal(target.Rejection == SceneTaskRejection.None, result.Accepted);
            Assert.Equal(target.FailureIndex, result.FailureUpdateIndex);
            Assert.Equal(target.Updates, result.AcceptedUpdates);
            Assert.Equal(target.Ticks, result.TicksConsumed);
            Assert.Equal(Scene, result.State.SceneRunId);
            Assert.Equal(target.Tick, result.State.Tick);
            Assert.Equal(target.Barrier, result.State.Barrier);
            Assert.Equal(target.Barrier.Status, result.State.BarrierStatus);
            Assert.Equal(target.Tasks, result.State.Tasks.ToArray());
            Assert.Equal(target.Tasks.Length + 1, result.State.NextSequence);
            Assert.Equal(target.Tasks.Length, result.State.TotalStarted);
            Assert.Equal(target.Tasks.Count(t => t.Phase is not (SceneTaskPhase.Completed or SceneTaskPhase.Cancelled)), result.State.LiveTasks);
            AssertTrace(target.Trace, result.Trace);
            if (!result.Accepted)
            {
                Assert.Same(before, result.State);
                Assert.NotNull(result.Diagnostic);
                Assert.Empty(result.Trace);
            }
            else Assert.Null(result.Diagnostic);
            state = result.State;
            history.Add(JsonSerializer.Serialize(result)); // Includes every state/trace/result field for replay comparison.
        }
        return string.Join('\n', history);
    }

    private static SceneTaskUpdate ParseUpdate(JsonElement update)
    {
        string kind = update.GetProperty("update").GetString()!;
        if (kind == "Start") return new SceneTaskUpdate.Start(Enum.Parse<SceneTaskFamily>(update.GetProperty("family").GetString()!), new(update.GetProperty("owner").GetString()!));
        var id = new SceneTaskId(update.TryGetProperty("sceneRunId", out var scene) ? new(scene.GetString()!) : Scene,
            update.GetProperty("sequence").GetInt64());
        long revision = update.GetProperty("expectedRevision").GetInt64();
        return kind switch
        {
            "Complete" => new SceneTaskUpdate.Complete(id, revision),
            "Phase" => new SceneTaskUpdate.Phase(id, revision, Enum.Parse<SceneTaskPhase>(update.GetProperty("target").GetString()!)),
            "Cancel" => new SceneTaskUpdate.Cancel(id, revision, Enum.Parse<SceneTaskCancellationReason>(update.GetProperty("reason").GetString()!)),
            _ => throw new InvalidDataException("Unexpected update."),
        };
    }

    private static Expected[] Expectations(string id)
    {
        var camera = T(1, SceneTaskFamily.Camera, SceneTaskPhase.Running, 0);
        var cameraDone = T(1, SceneTaskFamily.Camera, SceneTaskPhase.Completed, 1);
        var second = T(2, SceneTaskFamily.Camera, SceneTaskPhase.Running, 0, "sample:owner/b");
        var secondDone = T(2, SceneTaskFamily.Camera, SceneTaskPhase.Completed, 1, "sample:owner/b");
        var condition = T(1, SceneTaskFamily.ConditionDisplay, SceneTaskPhase.Running, 0);
        var entering = T(1, SceneTaskFamily.DarkScreen, SceneTaskPhase.Entering, 0);
        var holding = T(1, SceneTaskFamily.DarkScreen, SceneTaskPhase.Holding, 1);
        var leaving = T(1, SceneTaskFamily.DarkScreen, SceneTaskPhase.Leaving, 2);
        var darkDone = T(1, SceneTaskFamily.DarkScreen, SceneTaskPhase.Completed, 3);
        var cancelled = T(1, SceneTaskFamily.Camera, SceneTaskPhase.Cancelled, 1) with
            { CancellationReason = SceneTaskCancellationReason.OwnerAcknowledgedCancellation };
        var camBarrier = B(SceneTaskClass.Camera);
        var transition = B(SceneTaskClass.DarkScreenTransition);
        var holdBarrier = B(SceneTaskClass.DarkScreenHold, 1);
        var lastTransition = B(SceneTaskClass.DarkScreenTransition, 2);
        return id switch
        {
            "BAR-EMPTY-001" => [Enter(camBarrier, []), Poll(1, camBarrier, [], [], [], true)],
            "BAR-MULTI-002" => [Enter(camBarrier, [camera, second]),
                Poll(1, camBarrier, [cameraDone, second], [Change(SceneTaskTraceKind.TaskCompleted, 1, camera, cameraDone)], [2], false),
                Poll(2, camBarrier, [cameraDone, secondDone], [Change(SceneTaskTraceKind.TaskCompleted, 2, second, secondDone)], [], true)],
            "BAR-NONMATCH-003" => [Enter(camBarrier, [condition]), Poll(1, camBarrier, [condition], [], [], true)],
            "BAR-LIVE-004" => [Enter(camBarrier, [camera]),
                Poll(1, camBarrier, [cameraDone, second], [Change(SceneTaskTraceKind.TaskCompleted, 1, camera, cameraDone),
                    Change(SceneTaskTraceKind.TaskStarted, 1, null, second)], [2], false),
                Poll(2, camBarrier, [cameraDone, secondDone], [Change(SceneTaskTraceKind.TaskCompleted, 2, second, secondDone)], [], true)],
            "BAR-DARK-005" => [Enter(transition, [entering]),
                Poll(1, transition, [holding], [Change(SceneTaskTraceKind.TaskPhaseChanged, 1, entering, holding)], [], true),
                Enter(holdBarrier, [holding]),
                Poll(2, holdBarrier, [leaving], [Change(SceneTaskTraceKind.TaskPhaseChanged, 2, holding, leaving)], [], true),
                Enter(lastTransition, [leaving]),
                Poll(3, lastTransition, [darkDone], [Change(SceneTaskTraceKind.TaskCompleted, 3, leaving, darkDone)], [], true)],
            "BAR-CANCEL-006" => [Enter(camBarrier, [camera]),
                Poll(1, camBarrier, [cancelled], [Change(SceneTaskTraceKind.TaskCancelled, 1, camera, cancelled)], [], true)],
            "BAR-PRECOMPLETED-007" => [Enter(camBarrier, [cameraDone]), Poll(1, camBarrier, [cameraDone], [], [], true)],
            "BAR-DUPLICATE-008" => [Enter(camBarrier, [cameraDone]), Reject(camBarrier, [cameraDone], SceneTaskRejection.DuplicateTerminalUpdate, 0)],
            "BAR-STALE-009" => [Enter(B(SceneTaskClass.DarkScreenHold), [holding]),
                Reject(B(SceneTaskClass.DarkScreenHold), [holding], SceneTaskRejection.StaleTaskRevision, 0)],
            "BAR-FOREIGN-010" => [Enter(camBarrier, [camera]), Reject(camBarrier, [camera], SceneTaskRejection.WrongScene, 0)],
            "BAR-UNKNOWN-011" => [Enter(camBarrier, [camera]), Reject(camBarrier, [camera], SceneTaskRejection.UnknownTask, 0)],
            "BAR-ATOMIC-012" => [Enter(camBarrier, [camera]), Reject(camBarrier, [camera], SceneTaskRejection.StaleTaskRevision, 1)],
            "BAR-UPDATE-BUDGET-013" => [Enter(camBarrier, [camera]), Reject(camBarrier, [camera], SceneTaskRejection.UpdateBudgetExceeded)],
            "BAR-PHASE-014" => [Enter(camBarrier, [camera]), Reject(camBarrier, [camera], SceneTaskRejection.IllegalTaskTransition, 0)],
            "BAR-SEQUENTIAL-015" => [Enter(camBarrier, []), Poll(1, camBarrier, [], [], [], true),
                Enter(B(SceneTaskClass.Camera, 1), []), Poll(2, B(SceneTaskClass.Camera, 1), [], [], [], true)],
            "BAR-PENDING-016" => [Enter(camBarrier, [camera]), Reject(camBarrier, [camera], SceneTaskRejection.BarrierAlreadyPending)],
            "BAR-ORDERED-PHASES-017" => [Enter(transition, [holding]), Poll(1, transition, [darkDone],
                [Change(SceneTaskTraceKind.TaskPhaseChanged, 1, holding, leaving), Change(SceneTaskTraceKind.TaskCompleted, 1, leaving, darkDone)], [], true)],
            _ => throw new InvalidDataException("Specification has no independently authored full expectation."),
        };
    }

    private sealed record Expected(long Tick, SceneBarrier Barrier, SceneTask[] Tasks, SceneTaskTrace[] Trace,
        int Updates = 0, int Ticks = 0, SceneTaskRejection Rejection = SceneTaskRejection.None, int? FailureIndex = null);
    private static SceneTask T(long sequence, SceneTaskFamily family, SceneTaskPhase phase, long revision, string owner = "sample:owner/a")
        => new(new(Scene, sequence), new(owner), family, phase, revision);
    private static SceneBarrier B(SceneTaskClass selector, long entryTick = 0)
        => new(SceneBarrierStatus.Pending, selector, Consumer, 4, Continuation, entryTick);
    private static SceneTaskTrace Change(SceneTaskTraceKind kind, long tick, SceneTask? before, SceneTask after)
        => new(kind, tick, before, after, null, []);
    private static Expected Enter(SceneBarrier barrier, SceneTask[] tasks)
        => new(barrier.EntryTick, barrier, tasks, [new(SceneTaskTraceKind.BarrierEntered, barrier.EntryTick, null, null, barrier, [])]);
    private static Expected Reject(SceneBarrier barrier, SceneTask[] tasks, SceneTaskRejection reason, int? index = null)
        => new(0, barrier, tasks, [], Rejection: reason, FailureIndex: index);
    private static Expected Poll(long tick, SceneBarrier pending, SceneTask[] tasks, SceneTaskTrace[] changes, long[] matches, bool released)
    {
        SceneTaskTrace[] trace = [new(SceneTaskTraceKind.TickAdvanced, tick, null, null, null, []), ..changes,
            new(SceneTaskTraceKind.BarrierPolled, tick, null, null, pending, matches.Select(s => new SceneTaskId(Scene, s)).ToImmutableArray())];
        var barrier = released ? pending with { Status = SceneBarrierStatus.Released } : pending;
        if (released) trace = [..trace, new(SceneTaskTraceKind.BarrierReleased, tick, null, null, barrier, [])];
        return new(tick, barrier, tasks, trace, changes.Length, 1);
    }

    internal static void AssertTrace(IEnumerable<SceneTaskTrace> expected, IEnumerable<SceneTaskTrace> actual)
    {
        Assert.Equal(expected.Count(), actual.Count());
        foreach (var (a, b) in expected.Zip(actual))
        {
            Assert.Equal(a.Kind, b.Kind);
            Assert.Equal(a.Tick, b.Tick);
            Assert.Equal(a.Before, b.Before);
            Assert.Equal(a.After, b.After);
            Assert.Equal(a.Barrier, b.Barrier);
            Assert.Equal(a.MatchingTasks.ToArray(), b.MatchingTasks.ToArray());
        }
    }
    private static JsonDocument Load() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "task-barrier-contract-v1.json")));
}
