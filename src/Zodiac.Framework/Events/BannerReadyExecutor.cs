using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>One atomic clock/consumer/ledger; no renderer facts, external workers, restarts or concurrent displays.</summary>
public static class BannerReadyExecutor
{
    public static BannerReadyResult RunUntilBlocked(BannerReadyState state, int instructionBudget = 8192, int updateBudget = 64)
        => Execute(state, null, false, instructionBudget, updateBudget);
    public static BannerReadyResult AdvanceTick(BannerReadyState state, bool? circle, int instructionBudget = 8192, int updateBudget = 64)
        => Execute(state, circle, true, instructionBudget, updateBudget);
    private static BannerReadyResult Execute(BannerReadyState state, bool? circle, bool advance, int instructions, int updates)
    {
        ArgumentNullException.ThrowIfNull(state);
        BannerReadyResult Fail(BannerSceneRejection reason, string message, SceneTaskRejection task = SceneTaskRejection.None)
            => new(state, [], reason, task, message, 0, 0);
        if (instructions is < 0 or > 8192) return Fail(BannerSceneRejection.InvalidInstructionBudget, "Instruction budget must be in 0..8192.");
        if (updates is < 0 or > 64) return Fail(BannerSceneRejection.TaskLedgerRejected, "Update budget must be in 0..64.", SceneTaskRejection.InvalidUpdateBudget);
        if (!Valid(state)) return Fail(BannerSceneRejection.InvalidManagedTaskBinding, "Both preloaded bindings, exclusive workers and retained ledger must agree.");
        if (state.Event.Exited) return Fail(BannerSceneRejection.AlreadyExited, "Sequence has exited.");
        if (advance && circle is null) return Fail(BannerSceneRejection.MissingCircleSample, "An explicit Circle sample is required.");
        if (advance == (state.Event.Suspension == SceneEventSuspension.Ready))
            return Fail(advance ? BannerSceneRejection.NotWaiting : BannerSceneRejection.AwaitingTick, "Use the command appropriate to the interpreter suspension.");
        var current = state; int usedInstructions = 0, usedUpdates = 0;
        var trace = ImmutableArray.CreateBuilder<BannerReadyTrace>();
        void EventFacts(IEnumerable<SceneEventTrace> facts) => trace.AddRange(facts.Select(f => new BannerReadyTrace(BannerReadyTraceKind.Event, f.Tick, Event: f)));
        if (advance)
        {
            // Poll yesterday's facts before either worker resumes or a successor is admitted.
            var resumed = SceneEventExecutor.ResumeTick(current.Event, [], 0);
            if (!resumed.Accepted) return Fail(BannerSceneRejection.TaskLedgerRejected, resumed.Diagnostic!, resumed.TaskRejection);
            current = current.With(resumed.State, current.General, current.Ready); EventFacts(resumed.Trace);
        }
        while (!current.Event.Exited && current.Event.Suspension == SceneEventSuspension.Ready)
        {
            if (usedInstructions == instructions) return Fail(BannerSceneRejection.InstructionBudgetExceeded, "Budget exhausted before suspension/exit.");
            int offset = current.Event.Offset;
            if (offset is 0 or 2)
            {
                bool general = offset == 0;
                if (general ? current.General is not null : current.Ready is not null || current.General?.IsTerminal != true)
                    return Fail(BannerSceneRejection.EffectAlreadyAttached, "A display can start once, after its predecessor clears.");
                var started = SceneTaskLedger.ApplyPostVmUpdates(current.Event.Tasks,
                    [new SceneTaskUpdate.Start(SceneTaskFamily.ConditionDisplay, current.Program.General.OwnerId)], updates - usedUpdates);
                if (!started.Accepted) return Fail(BannerSceneRejection.TaskLedgerRejected, started.Diagnostic!, started.Rejection);
                usedUpdates += started.AcceptedUpdates;
                trace.AddRange(started.Trace.Select(f => new BannerReadyTrace(BannerReadyTraceKind.Ledger, f.Tick, Task: f)));
                var sceneEvent = BannerSceneExecutor.WithTasks(current.Event, started.State); var id = started.State.Tasks[^1].Id;
                current = general
                    ? current.With(sceneEvent, new(current.Program.General.Definition, id, current.Program.General.OwnerId,
                        current.Event.Tick, current.Event.Tick, GeneralBannerPhase.Revealing, 0, 0, 0, null), current.Ready)
                    : current.With(sceneEvent, current.General, ReadyDisplayState.Initialize(current.Program.Ready.Definition, id,
                        current.Program.Ready.OwnerId, current.Event.Tick));
                trace.Add(new(general ? BannerReadyTraceKind.GeneralBirth : BannerReadyTraceKind.ReadyBirth, current.Event.Tick,
                    General: current.General, Ready: current.Ready));
            }
            var step = SceneEventExecutor.StepInstruction(current.Event);
            if (!step.Accepted) return Fail(BannerSceneRejection.TaskLedgerRejected, step.Diagnostic!, step.TaskRejection);
            current = current.With(step.State, current.General, current.Ready); usedInstructions++; EventFacts(step.Trace);
        }
        // Only workers present before the call resume. A newborn is already at its first yield.
        if (advance && state.General is { IsTerminal: false } before)
        {
            var after = before.Advance(current.Event.Tick, circle!.Value, false);
            current = current.With(current.Event, after, current.Ready);
            trace.Add(new(BannerReadyTraceKind.GeneralAdvance, current.Event.Tick, General: after, Circle: circle));
            if (after.IsTerminal && !Complete(after.TaskId)) return Fail(BannerSceneRejection.TaskLedgerRejected, "Terminal update exceeds budget.", SceneTaskRejection.UpdateBudgetExceeded);
        }
        if (advance && state.Ready is { IsTerminal: false } ready)
        {
            var dispatcher = ready.ResumeDispatcher(current.Event.Tick, circle!.Value);
            trace.Add(new(BannerReadyTraceKind.ReadyDispatcher, current.Event.Tick, Ready: dispatcher,
                Circle: ready.DispatcherPhase == ReadyDispatcherPhase.WaitingInput ? circle : null));
            var child = dispatcher.ResumeChild(current.Event.Tick);
            current = current.With(current.Event, current.General, child);
            trace.Add(new(BannerReadyTraceKind.ReadyChild, current.Event.Tick, Ready: child));
            if (child.IsTerminal && !Complete(child.TaskId)) return Fail(BannerSceneRejection.TaskLedgerRejected, "Terminal update exceeds budget.", SceneTaskRejection.UpdateBudgetExceeded);
        }
        return new(current, trace.ToImmutable(), BannerSceneRejection.None, SceneTaskRejection.None, null, usedInstructions, usedUpdates);

        bool Complete(SceneTaskId id)
        {
            var completed = SceneTaskLedger.ApplyPostVmUpdates(current.Event.Tasks, [new SceneTaskUpdate.Complete(id, 0)], updates - usedUpdates);
            if (!completed.Accepted) return false;
            usedUpdates += completed.AcceptedUpdates;
            current = current.With(BannerSceneExecutor.WithTasks(current.Event, completed.State), current.General, current.Ready);
            trace.AddRange(completed.Trace.Select(f => new BannerReadyTrace(BannerReadyTraceKind.Ledger, f.Tick, Task: f))); return true;
        }
    }
    private static bool Valid(BannerReadyState s)
    {
        static bool Binding(PreloadedBannerBinding? b, AssetId definition, AssetId art, AssetId owner)
            => b is not null && b.DefinitionId == definition && b.ArtworkId == art && b.OwnerId == owner;
        if (s.Binding is null || !Binding(s.Binding.General, s.Program.General.Definition.Id, s.Program.General.ArtworkId, s.Program.General.OwnerId) ||
            !Binding(s.Binding.Ready, s.Program.Ready.Definition.Id, s.Program.Ready.ArtworkId, s.Program.Ready.OwnerId) ||
            !ReferenceEquals(s.Event.Program, s.Program.Body) || s.General is { } general &&
                (general.Definition != s.Program.General.Definition || general.OwnerId != s.Program.General.OwnerId) ||
            !BannerSceneExecutor.ValidBinding(new(s.Event, s.General)) ||
            s.General is { IsTerminal: false } && s.Ready is not null || s.Event.Tasks.TotalStarted != (s.General is null ? 0 : s.Ready is null ? 1 : 2)) return false;
        if (s.Ready is not { } ready) return true;
        var task = s.Event.Tasks.Tasks.FirstOrDefault(t => t.Id == ready.TaskId);
        return s.General?.IsTerminal == true && ready.Definition == s.Program.Ready.Definition && ready.OwnerId == s.Program.Ready.OwnerId &&
            ready.LastProcessedTick <= s.Event.Tick && (ready.IsTerminal || ready.LastProcessedTick == s.Event.Tick) &&
            task is not null && task.OwnerId == ready.OwnerId && task.Family == SceneTaskFamily.ConditionDisplay &&
            ready.DispatcherPhase != ReadyDispatcherPhase.Cancelled && ready.ChildPhase != ReadyChildPhase.Cancelled &&
            task.CancellationReason is null && task.Phase == (ready.IsTerminal ? SceneTaskPhase.Completed : SceneTaskPhase.Running) && task.Revision == (ready.IsTerminal ? 1 : 0);
    }
}
