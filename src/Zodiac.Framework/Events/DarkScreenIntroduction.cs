using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Caller-owned proof of prevalidated logical definitions. Device/file readiness belongs to the host.</summary>
public sealed record DarkScreenIntroductionBinding(ReadyDarkScreenBinding Dark,
    PreloadedBannerBinding General, PreloadedBannerBinding Ready);

/// <summary>Nine authored operations; no original event import, external tasks, restart or standalone dark hold timer.</summary>
public sealed class DarkScreenIntroductionProgram
{
    public AssetId Id => Body.Id;
    public StartDarkScreen Dark { get; }
    public DisplayGeneralBanner General { get; }
    public DisplayReady Ready { get; }
    internal SceneEventProgram Body { get; }

    public DarkScreenIntroductionProgram(AssetId id, StartDarkScreen dark, DisplayGeneralBanner general, DisplayReady ready)
    {
        ArgumentNullException.ThrowIfNull(dark);
        ArgumentNullException.ThrowIfNull(general);
        ArgumentNullException.ThrowIfNull(ready);
        if (dark.Definition is null || general.Definition is null || ready.Definition is null ||
            general.Definition.Condition != 8 || ready.Definition.Condition != 0 ||
            general.Definition.HoldFrames is < 0 or > 255 || ready.Definition.HoldFrames is < 0 or > 255 ||
            dark.OwnerId != general.OwnerId || dark.OwnerId != ready.OwnerId)
            throw new ArgumentException("Selected definitions, unsigned holds and one logical owner are required.");
        AssetId[] ids = [id, dark.Definition.Id, general.Definition.Id, ready.Definition.Id,
            general.ArtworkId, ready.ArtworkId, dark.OwnerId];
        if (ids.Any(value => !SceneTaskState.ValidId(value)) || ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Distinct bounded program, definition, artwork and owner IDs are required.");
        (Dark, General, Ready) = (dark, general, ready);
        Body = new(id, [Op(0), new(1, SceneTaskClass.DarkScreenTransition), Op(2),
            new(3, SceneTaskClass.ConditionDisplay), Op(4), new(5, SceneTaskClass.ConditionDisplay),
            Op(6), new(7, SceneTaskClass.DarkScreenTransition),
            new(new ConditionalEventInstruction(8, ConditionalEventOperation.Exit))]);
    }

    private static SceneEventInstruction Op(int offset) => new(new ConditionalEventInstruction(offset, ConditionalEventOperation.Nop));
    public DarkScreenIntroductionState Start(AssetId sceneRun, AssetId consumer, DarkScreenIntroductionBinding? binding)
        => new(this, Body.Start(default, SceneTaskState.Create(sceneRun), consumer), null, null, null, binding);
}

/// <summary>One immutable scene; the battle gate is a pure fact, never a battle command.</summary>
public sealed class DarkScreenIntroductionState
{
    public DarkScreenIntroductionProgram Program { get; }
    public SceneEventState Event { get; }
    public DarkScreenState? Dark { get; }
    public GeneralBannerState? General { get; }
    public ReadyDisplayState? Ready { get; }
    public DarkScreenIntroductionBinding? Binding { get; }
    public bool BattleAvailable => Event.Exited && Dark?.IsTerminal == true && General?.IsTerminal == true &&
        Ready?.IsTerminal == true && Event.Tasks.LiveTasks == 0;

    internal DarkScreenIntroductionState(DarkScreenIntroductionProgram program, SceneEventState sceneEvent,
        DarkScreenState? dark, GeneralBannerState? general, ReadyDisplayState? ready, DarkScreenIntroductionBinding? binding)
        => (Program, Event, Dark, General, Ready, Binding) = (program, sceneEvent, dark, general, ready, binding);
    internal DarkScreenIntroductionState With(SceneEventState sceneEvent, DarkScreenState? dark,
        GeneralBannerState? general, ReadyDisplayState? ready) => new(Program, sceneEvent, dark, general, ready, Binding);
}

public enum DarkScreenIntroductionTraceKind
{
    Event, Ledger, DarkBirth, DarkAdvance, GeneralBirth, GeneralAdvance,
    ReadyBirth, ReadyDispatcher, ReadyChild, DarkRelease, BattleAvailable
}
public sealed record DarkScreenIntroductionTrace(DarkScreenIntroductionTraceKind Kind, long Tick,
    SceneEventTrace? Event = null, SceneTaskTrace? Task = null, DarkScreenState? Dark = null,
    GeneralBannerState? General = null, ReadyDisplayState? Ready = null, bool? Circle = null);
public enum DarkScreenIntroductionRejection
{
    None, InvalidInstructionBudget, InvalidUpdateBudget, InvalidManagedBinding, MissingCircleSample,
    NotWaiting, AwaitingTick, AlreadyExited, InstructionBudgetExceeded, EventRejected, TaskLedgerRejected, InvalidCompletion
}
public sealed record DarkScreenIntroductionResult(DarkScreenIntroductionState State,
    ImmutableArray<DarkScreenIntroductionTrace> Trace, DarkScreenIntroductionRejection Rejection,
    SceneEventRejection EventRejection, SceneTaskRejection TaskRejection, string? Diagnostic,
    int InstructionsExecuted, int AcceptedTaskUpdates, int TicksConsumed, bool BattleBecameAvailable)
{
    public bool Accepted => Rejection == DarkScreenIntroductionRejection.None;
}

/// <summary>Atomic event-before-worker composition with one clock and cumulative budgets. No external update API.</summary>
public static class DarkScreenIntroductionExecutor
{
    public static DarkScreenIntroductionResult RunUntilBlocked(DarkScreenIntroductionState state,
        int instructionBudget = 8192, int updateBudget = 64) => Execute(state, null, false, instructionBudget, updateBudget);
    public static DarkScreenIntroductionResult AdvanceTick(DarkScreenIntroductionState state, bool? circle,
        int instructionBudget = 8192, int updateBudget = 64) => Execute(state, circle, true, instructionBudget, updateBudget);

    private static DarkScreenIntroductionResult Execute(DarkScreenIntroductionState state, bool? circle,
        bool advance, int instructions, int updates)
    {
        ArgumentNullException.ThrowIfNull(state);
        DarkScreenIntroductionResult Fail(DarkScreenIntroductionRejection reason, string message,
            SceneEventRejection ev = SceneEventRejection.None, SceneTaskRejection task = SceneTaskRejection.None)
            => new(state, [], reason, ev, task, message, 0, 0, 0, false);
        if (instructions is < 0 or > 8192) return Fail(DarkScreenIntroductionRejection.InvalidInstructionBudget, "Instruction budget must be in 0..8192.");
        if (updates is < 0 or > 64) return Fail(DarkScreenIntroductionRejection.InvalidUpdateBudget, "Update budget must be in 0..64.", task: SceneTaskRejection.InvalidUpdateBudget);
        if (!Valid(state)) return Fail(DarkScreenIntroductionRejection.InvalidManagedBinding, "Definitions, readiness, fixed program, workers, ledger and clock must agree.");
        if (state.Event.Exited) return Fail(DarkScreenIntroductionRejection.AlreadyExited, "The introduction has exited.");
        if (advance && circle is null) return Fail(DarkScreenIntroductionRejection.MissingCircleSample, "An explicit Circle sample is required.");
        if (advance == (state.Event.Suspension == SceneEventSuspension.Ready))
            return Fail(advance ? DarkScreenIntroductionRejection.NotWaiting : DarkScreenIntroductionRejection.AwaitingTick,
                "Use the command appropriate to the interpreter suspension.");

        var current = state;
        var trace = ImmutableArray.CreateBuilder<DarkScreenIntroductionTrace>();
        int usedInstructions = 0, usedUpdates = 0;
        void EventFacts(IEnumerable<SceneEventTrace> facts) => trace.AddRange(facts.Select(f =>
            new DarkScreenIntroductionTrace(DarkScreenIntroductionTraceKind.Event, f.Tick, Event: f)));
        SceneTaskResult Publish(SceneTaskUpdate fact)
        {
            var result = SceneTaskLedger.ApplyPostVmUpdates(current.Event.Tasks, [fact], updates - usedUpdates);
            if (result.Accepted)
            {
                usedUpdates += result.AcceptedUpdates;
                current = current.With(BannerSceneExecutor.WithTasks(current.Event, result.State), current.Dark, current.General, current.Ready);
                trace.AddRange(result.Trace.Select(f => new DarkScreenIntroductionTrace(DarkScreenIntroductionTraceKind.Ledger, f.Tick, Task: f)));
            }
            return result;
        }
        DarkScreenIntroductionResult LedgerFailure(SceneTaskResult result) =>
            Fail(DarkScreenIntroductionRejection.TaskLedgerRejected, result.Diagnostic!, task: result.Rejection);

        if (advance)
        {
            // Poll only the preceding snapshot's facts. No candidate birth, signal or completion can affect this poll.
            var resumed = SceneEventExecutor.ResumeTick(current.Event, [], 0);
            if (!resumed.Accepted) return Fail(DarkScreenIntroductionRejection.EventRejected, resumed.Diagnostic!, resumed.Rejection, resumed.TaskRejection);
            current = current.With(resumed.State, current.Dark, current.General, current.Ready);
            EventFacts(resumed.Trace);
        }
        while (!current.Event.Exited && current.Event.Suspension == SceneEventSuspension.Ready)
        {
            if (usedInstructions == instructions)
                return Fail(DarkScreenIntroductionRejection.InstructionBudgetExceeded, "Cumulative budget exhausted before suspension or exit.", SceneEventRejection.InstructionBudgetExceeded);
            int offset = current.Event.Offset;
            if (offset is 0 or 2 or 4)
            {
                bool dark = offset == 0, general = offset == 2;
                if (dark ? current.Dark is not null : general ? current.General is not null || current.Dark?.Phase != DarkScreenPhase.Holding
                    : current.Ready is not null || current.General?.IsTerminal != true || current.Dark?.Phase != DarkScreenPhase.Holding)
                    return Fail(DarkScreenIntroductionRejection.InvalidManagedBinding, "A worker can start only once at its fixed admission point.");
                var started = Publish(new SceneTaskUpdate.Start(dark ? SceneTaskFamily.DarkScreen : SceneTaskFamily.ConditionDisplay, current.Program.Dark.OwnerId));
                if (!started.Accepted) return LedgerFailure(started);
                var id = current.Event.Tasks.Tasks[^1].Id;
                current = dark
                    ? current.With(current.Event, DarkScreenState.Start(current.Program.Dark.Definition, id, current.Program.Dark.OwnerId, current.Event.Tick), current.General, current.Ready)
                    : general ? current.With(current.Event, current.Dark, new(current.Program.General.Definition, id, current.Program.General.OwnerId,
                        current.Event.Tick, current.Event.Tick, GeneralBannerPhase.Revealing, 0, 0, 0, null), current.Ready)
                    : current.With(current.Event, current.Dark, current.General,
                        ReadyDisplayState.Initialize(current.Program.Ready.Definition, id, current.Program.Ready.OwnerId, current.Event.Tick));
                trace.Add(new(dark ? DarkScreenIntroductionTraceKind.DarkBirth : general ? DarkScreenIntroductionTraceKind.GeneralBirth : DarkScreenIntroductionTraceKind.ReadyBirth,
                    current.Event.Tick, Dark: dark ? current.Dark : null, General: general ? current.General : null, Ready: offset == 4 ? current.Ready : null));
            }
            if (offset == 6)
            {
                if (current.Dark?.Phase != DarkScreenPhase.Holding || current.Ready?.IsTerminal != true)
                    return Fail(DarkScreenIntroductionRejection.InvalidCompletion, "Ready must clear before releasing the retained holding worker.");
                var signal = Publish(new SceneTaskUpdate.Phase(current.Dark.TaskId, 1, SceneTaskPhase.Leaving));
                if (!signal.Accepted) return LedgerFailure(signal);
                current = current.With(current.Event, current.Dark.SignalRelease(), current.General, current.Ready);
                trace.Add(new(DarkScreenIntroductionTraceKind.DarkRelease, current.Event.Tick, Dark: current.Dark));
            }
            var stepped = SceneEventExecutor.StepInstruction(current.Event);
            if (!stepped.Accepted) return Fail(DarkScreenIntroductionRejection.EventRejected, stepped.Diagnostic!, stepped.Rejection, stepped.TaskRejection);
            current = current.With(stepped.State, current.Dark, current.General, current.Ready);
            usedInstructions++; EventFacts(stepped.Trace);
        }
        // Birth snapshots are already at their first yield. Resume only workers attached before this command.
        if (advance && state.Dark is { IsTerminal: false })
        {
            var before = current.Dark!; // Includes the candidate event's same-tick release signal.
            var after = before.Advance(current.Event.Tick);
            current = current.With(current.Event, after, current.General, current.Ready);
            trace.Add(new(DarkScreenIntroductionTraceKind.DarkAdvance, current.Event.Tick, Dark: after));
            if (before.Phase != after.Phase)
            {
                var task = current.Event.Tasks.Tasks[0];
                SceneTaskUpdate update = after.IsTerminal ? new SceneTaskUpdate.Complete(task.Id, task.Revision)
                    : new SceneTaskUpdate.Phase(task.Id, task.Revision, SceneTaskPhase.Holding);
                var published = Publish(update);
                if (!published.Accepted) return LedgerFailure(published);
            }
        }
        if (advance && state.General is { IsTerminal: false } generalBefore)
        {
            var after = generalBefore.Advance(current.Event.Tick, circle!.Value, false);
            current = current.With(current.Event, current.Dark, after, current.Ready);
            trace.Add(new(DarkScreenIntroductionTraceKind.GeneralAdvance, current.Event.Tick, General: after,
                Circle: generalBefore.Phase is GeneralBannerPhase.Revealing or GeneralBannerPhase.Holding ? circle : null));
            if (after.IsTerminal)
            {
                var published = Publish(new SceneTaskUpdate.Complete(after.TaskId, 0));
                if (!published.Accepted) return LedgerFailure(published);
            }
        }
        if (advance && state.Ready is { IsTerminal: false } readyBefore)
        {
            var dispatcher = readyBefore.ResumeDispatcher(current.Event.Tick, circle!.Value);
            trace.Add(new(DarkScreenIntroductionTraceKind.ReadyDispatcher, current.Event.Tick, Ready: dispatcher,
                Circle: readyBefore.DispatcherPhase == ReadyDispatcherPhase.WaitingInput ? circle : null));
            var child = dispatcher.ResumeChild(current.Event.Tick);
            current = current.With(current.Event, current.Dark, current.General, child);
            trace.Add(new(DarkScreenIntroductionTraceKind.ReadyChild, current.Event.Tick, Ready: child));
            if (child.IsTerminal)
            {
                var published = Publish(new SceneTaskUpdate.Complete(child.TaskId, 0));
                if (!published.Accepted) return LedgerFailure(published);
            }
        }
        if (current.Event.Exited && !current.BattleAvailable)
            return Fail(DarkScreenIntroductionRejection.InvalidCompletion, "Synthetic exit requires all three retained workers terminal and no live tasks.");
        if (current.BattleAvailable) trace.Add(new(DarkScreenIntroductionTraceKind.BattleAvailable, current.Event.Tick));
        return new(current, trace.ToImmutable(), DarkScreenIntroductionRejection.None, SceneEventRejection.None,
            SceneTaskRejection.None, null, usedInstructions, usedUpdates, advance ? 1 : 0, current.BattleAvailable);
    }

    private static bool Valid(DarkScreenIntroductionState s)
    {
        static bool Binding(PreloadedBannerBinding? b, AssetId definition, AssetId art, AssetId owner)
            => b is not null && b.DefinitionId == definition && b.ArtworkId == art && b.OwnerId == owner;
        if (s.Binding?.Dark is not { } bound || bound.DefinitionId != s.Program.Dark.Definition.Id || bound.OwnerId != s.Program.Dark.OwnerId ||
            !Binding(s.Binding.General, s.Program.General.Definition.Id, s.Program.General.ArtworkId, s.Program.General.OwnerId) ||
            !Binding(s.Binding.Ready, s.Program.Ready.Definition.Id, s.Program.Ready.ArtworkId, s.Program.Ready.OwnerId) ||
            !ReferenceEquals(s.Event.Program, s.Program.Body) || !ReferenceEquals(s.Event.Inner.Program, s.Program.Body.Body) ||
            s.Event.Variables != default || !SceneTaskState.ValidId(s.Event.ConsumerId) ||
            !SceneTaskState.ValidId(s.Event.Tasks.SceneRunId) || s.Event.Tick < 0 || s.Event.RemainingWaitTicks != 0 ||
            s.Event.Tasks.TotalStarted != (s.Dark is null ? 0 : s.General is null ? 1 : s.Ready is null ? 2 : 3) ||
            s.Event.Tasks.NextSequence != s.Event.Tasks.TotalStarted + 1L) return false;
        if (s.Dark is null) return s.General is null && s.Ready is null && s.Event.Tick == 0 && !s.Event.Exited &&
            s.Event.Offset == 0 && s.Event.Suspension == SceneEventSuspension.Ready && s.Event.WaitingAtOffset is null && s.Event.Tasks.Barrier is null;
        bool Clock(long start, long last, bool terminal) => start >= 0 && start <= last && last <= s.Event.Tick && (terminal || last == s.Event.Tick);
        bool Task(SceneTaskId id, int sequence, SceneTaskFamily family, SceneTaskPhase phase, long revision) =>
            id == new SceneTaskId(s.Event.Tasks.SceneRunId, sequence) && s.Event.Tasks.Tasks[sequence-1] is var task &&
            task.Id == id && task.OwnerId == s.Program.Dark.OwnerId && task.Family == family && task.Phase == phase &&
            task.Revision == revision && task.CancellationReason is null;
        var dark = s.Dark;
        var (darkPhase, darkRevision) = dark.Phase switch
        {
            DarkScreenPhase.Entering => (SceneTaskPhase.Entering, 0L), DarkScreenPhase.Holding => (SceneTaskPhase.Holding, 1L),
            DarkScreenPhase.Leaving => (SceneTaskPhase.Leaving, 2L), _ => (SceneTaskPhase.Completed, 3L),
        };
        if (!Enum.IsDefined(dark.Phase) || !ReferenceEquals(dark.Definition, s.Program.Dark.Definition) || dark.OwnerId != s.Program.Dark.OwnerId ||
            dark.StartTick != 0 || !Clock(dark.StartTick, dark.LastProcessedTick, dark.IsTerminal) || dark.ReleaseYieldPending ||
            !Task(dark.TaskId, 1, SceneTaskFamily.DarkScreen, darkPhase, darkRevision)) return false;
        if (s.General is { } general && (general.Definition != s.Program.General.Definition || general.OwnerId != s.Program.General.OwnerId ||
            !Enum.IsDefined(general.Phase) || general.Phase == GeneralBannerPhase.Cancelled ||
            general.StartTick != s.Program.Dark.Definition.Tiles.Max(t => t.InitialDelay) + 15 ||
            !Clock(general.StartTick, general.LastProcessedTick, general.IsTerminal) ||
            general.RevealCompleted is < 0 or > 20 || general.HoldCompleted < 0 || general.HoldCompleted > general.Definition.HoldFrames || general.HideCompleted is < 0 or > 10 ||
            (general.Phase is GeneralBannerPhase.Completed or GeneralBannerPhase.ExitYield
                ? general.ExitCause is not (GeneralBannerExitCause.Natural or GeneralBannerExitCause.CircleSkipped) : general.ExitCause is not null) ||
            !Task(general.TaskId, 2, SceneTaskFamily.ConditionDisplay, general.IsTerminal ? SceneTaskPhase.Completed : SceneTaskPhase.Running, general.IsTerminal ? 1 : 0))) return false;
        if (s.Ready is { } ready && (s.General?.IsTerminal != true || ready.Definition != s.Program.Ready.Definition || ready.OwnerId != s.Program.Ready.OwnerId ||
            !Enum.IsDefined(ready.DispatcherPhase) || !Enum.IsDefined(ready.ChildPhase) || ready.DispatcherPhase == ReadyDispatcherPhase.Cancelled || ready.ChildPhase == ReadyChildPhase.Cancelled ||
            ready.StartTick <= s.General.LastProcessedTick || ready.StartTick - s.General.LastProcessedTick != 1 ||
            !Clock(ready.StartTick, ready.LastProcessedTick, ready.IsTerminal) || !ReadyLifecycle(ready) ||
            !Task(ready.TaskId, 3, SceneTaskFamily.ConditionDisplay, ready.IsTerminal ? SceneTaskPhase.Completed : SceneTaskPhase.Running, ready.IsTerminal ? 1 : 0))) return false;
        var barrier = s.Event.Tasks.Barrier;
        if (barrier is null || barrier.ConsumerId != s.Event.ConsumerId || barrier.ContinuationId != s.Program.Id ||
            barrier.EntryTick < 0 || barrier.EntryTick > s.Event.Tick) return false;
        if (s.Event.Exited) return s.Event.Offset == 8 && s.Event.Suspension == SceneEventSuspension.Ready && s.Event.WaitingAtOffset is null &&
            barrier.SourceOffset == 7 && barrier.Status == SceneBarrierStatus.Released && barrier.Selector == SceneTaskClass.DarkScreenTransition && s.BattleAvailable;
        if (s.Event.Suspension != SceneEventSuspension.Barrier || barrier.Status != SceneBarrierStatus.Pending || s.Event.WaitingAtOffset != barrier.SourceOffset ||
            s.Event.Offset != barrier.SourceOffset + 1) return false;
        return barrier.SourceOffset switch
        {
            1 => barrier.Selector == SceneTaskClass.DarkScreenTransition && s.General is null && s.Ready is null && dark.Phase is DarkScreenPhase.Entering or DarkScreenPhase.Holding,
            3 => barrier.Selector == SceneTaskClass.ConditionDisplay && s.General is not null && s.Ready is null && dark.Phase == DarkScreenPhase.Holding,
            5 => barrier.Selector == SceneTaskClass.ConditionDisplay && s.General?.IsTerminal == true && s.Ready is not null && dark.Phase == DarkScreenPhase.Holding,
            7 => barrier.Selector == SceneTaskClass.DarkScreenTransition && s.General?.IsTerminal == true && s.Ready?.IsTerminal == true && dark.Phase is DarkScreenPhase.Leaving or DarkScreenPhase.Completed,
            _ => false,
        };
    }

    // Check accepted-snapshot coherence; the existing worker still owns all transition arithmetic.
    private static bool ReadyLifecycle(ReadyDisplayState r)
    {
        if (r.WaitSamplesTaken < 0 || r.WaitSamplesTaken > r.Definition.HoldFrames) return false;
        if (r.DispatcherPhase == ReadyDispatcherPhase.WaitingInput)
            return r.Definition.HoldFrames > 0 && r.WaitSamplesTaken < r.Definition.HoldFrames && r.ReleaseTick is null && r.ExitCause is null &&
                (r.ChildPhase == ReadyChildPhase.FadeIn && r.DrawScale is >= 48 and <= 120 && (r.DrawScale.Value - 48) % 8 == 0 ||
                 r.ChildPhase == ReadyChildPhase.Holding && r.DrawScale == 128) && r.FadeOutDraws == 0;
        if (r.ReleaseTick is not { } release || release < r.StartTick || release > r.LastProcessedTick ||
            r.ExitCause is not (ReadyExitCause.Natural or ReadyExitCause.CircleSkipped) ||
            r.DispatcherPhase == ReadyDispatcherPhase.Completed && r.ChildPhase != ReadyChildPhase.Completed) return false;
        return r.ChildPhase switch
        {
            ReadyChildPhase.FadeOut => r.FadeOutDraws is >= 1 and <= 7 && r.DrawScale == 128 - 12 * (r.FadeOutDraws - 1),
            ReadyChildPhase.ExitYield or ReadyChildPhase.Completed => r.FadeOutDraws == 7 && r.DrawScale is null,
            _ => false,
        };
    }
}
