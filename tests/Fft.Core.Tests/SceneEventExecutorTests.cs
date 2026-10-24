using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Fft.Core.Events;
using Xunit;

namespace Fft.Core.Tests;

public sealed class SceneEventExecutorTests
{
    private static readonly AssetId Scene = new("sample:scene/a");
    private static readonly AssetId Owner = new("sample:owner/a");
    private static readonly AssetId Consumer = new("sample:consumer/a");
    private static readonly AssetId ProgramId = new("sample:event/barrier");
    private static SceneTaskState Empty() => SceneTaskState.Create(Scene);
    private static SceneTaskUpdate.Start Start(SceneTaskFamily family = SceneTaskFamily.Camera) => new(family, Owner);
    private static SceneTaskId Id(long sequence) => new(Scene, sequence);
    private static SceneEventProgram Program(params SceneEventInstruction[] code) => new(ProgramId, code);
    private static SceneEventInstruction Op(int offset, ConditionalEventOperation operation, int? a = null, int? b = null, int? target = null)
        => new(new ConditionalEventInstruction(offset, operation, a, b, target));
    private static SceneEventProgram CameraProgram() => Program(new(4, SceneTaskClass.Camera), Op(7, ConditionalEventOperation.Exit));

    [Theory]
    [InlineData(SceneTaskClass.Camera)]
    [InlineData(SceneTaskClass.DarkScreenTransition)]
    [InlineData(SceneTaskClass.DarkScreenHold)]
    [InlineData(SceneTaskClass.ConditionDisplay)]
    public void Empty_barrier_always_yields_then_resumes_and_exits_in_same_tick_with_exact_trace(SceneTaskClass selector)
    {
        var initial = Program(new(4, selector), Op(7, ConditionalEventOperation.Exit)).Start(new(7,8,2,9), Empty(), Consumer);
        var entered = SceneEventExecutor.RunUntilBlocked(initial, 1);
        Assert.True(entered.Accepted);
        Assert.Equal(1, entered.InstructionsExecuted);
        Assert.Equal(0, entered.TicksConsumed);
        Assert.Equal(SceneEventSuspension.Ready, initial.Suspension);
        Assert.Null(initial.Tasks.Barrier);
        Assert.Equal(SceneEventSuspension.Barrier, entered.State.Suspension);
        Assert.Equal(4, entered.State.WaitingAtOffset);
        Assert.Equal(7, entered.State.Offset);
        Assert.Equal(0, entered.State.Tick);
        var pending = new SceneBarrier(SceneBarrierStatus.Pending, selector, Consumer, 4, ProgramId, 0);
        EqualTrace([
            new(SceneEventTraceKind.BarrierWaitEntered,0,4,new(7,8,2,9),new(7,8,2,9),7,0,false),
            new(SceneEventTraceKind.TaskLedger,0,4,new(7,8,2,9),new(7,8,2,9),7,0,false,
                TaskTrace:new(SceneTaskTraceKind.BarrierEntered,0,null,null,pending,[])),
        ], entered.Trace);
        var tick = SceneEventExecutor.AdvanceTick(entered.State, [], 1);
        Assert.True(tick.Accepted);
        Assert.True(tick.State.Exited);
        Assert.Equal(1, tick.State.Tick);
        Assert.Equal(1, tick.InstructionsExecuted);
        Assert.Equal(0, tick.AcceptedTaskUpdates);
        Assert.Equal(1, tick.TicksConsumed);
        Assert.Null(tick.State.WaitingAtOffset);
        var released = pending with { Status = SceneBarrierStatus.Released };
        EqualTrace([
            new(SceneEventTraceKind.TaskLedger,1,4,new(7,8,2,9),new(7,8,2,9),7,0,false,
                TaskTrace:new(SceneTaskTraceKind.TickAdvanced,1,null,null,null,[])),
            new(SceneEventTraceKind.TaskLedger,1,4,new(7,8,2,9),new(7,8,2,9),7,0,false,
                TaskTrace:new(SceneTaskTraceKind.BarrierPolled,1,null,null,pending,[])),
            new(SceneEventTraceKind.TaskLedger,1,4,new(7,8,2,9),new(7,8,2,9),7,0,false,
                TaskTrace:new(SceneTaskTraceKind.BarrierReleased,1,null,null,released,[])),
            new(SceneEventTraceKind.BarrierWaitResumed,1,4,new(7,8,2,9),new(7,8,2,9),7,0,false),
            new(SceneEventTraceKind.Instruction,1,7,new(7,8,2,9),new(7,8,2,9),7,0,true,ConditionalEventOperation.Exit),
        ], tick.Trace);
    }

    [Fact]
    public void Frame_wait_and_sequential_barriers_each_require_a_new_tick_while_zero_wait_falls_through()
    {
        var program = Program(new(4,1), new(7,SceneTaskClass.Camera), new(10,0), new(13,1),
            new(16,SceneTaskClass.Camera), Op(19,ConditionalEventOperation.Exit));
        var entered = SceneEventExecutor.RunUntilBlocked(program.Start(default, Empty(), Consumer), 1);
        var first = SceneEventExecutor.AdvanceTick(entered.State, [], 1);
        Assert.Equal(SceneEventSuspension.Barrier, first.State.Suspension);
        Assert.Equal(7, first.State.WaitingAtOffset);
        Assert.Equal(1, first.State.Tasks.Barrier!.EntryTick);
        Assert.DoesNotContain(first.Trace, t => t.TaskTrace?.Kind == SceneTaskTraceKind.BarrierPolled);
        var second = SceneEventExecutor.AdvanceTick(first.State, [], 2);
        Assert.Equal(SceneEventSuspension.Frames, second.State.Suspension);
        Assert.Equal(13, second.State.WaitingAtOffset);
        Assert.Equal(1, second.State.RemainingWaitTicks);
        Assert.Equal(new[] {SceneEventTraceKind.BarrierWaitResumed, SceneEventTraceKind.ZeroWait, SceneEventTraceKind.FrameWaitEntered},
            second.Trace.Where(t => t.TaskTrace is null).Select(t => t.Kind));
        var third = SceneEventExecutor.AdvanceTick(second.State, [], 1);
        Assert.Equal(SceneEventSuspension.Barrier, third.State.Suspension);
        Assert.Equal(16, third.State.WaitingAtOffset);
        Assert.Equal(3, third.State.Tasks.Barrier!.EntryTick);
        Assert.DoesNotContain(third.Trace, t => t.TaskTrace?.Kind == SceneTaskTraceKind.BarrierPolled);
        var fourth = SceneEventExecutor.AdvanceTick(third.State, [], 1);
        Assert.True(fourth.State.Exited);
        Assert.Equal(4, fourth.State.Tick);
        Assert.Equal(0, entered.State.Tick);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(32767)]
    public void Positive_frame_counts_retain_exact_boundary_while_tasks_advance_on_every_tick(int count)
    {
        var program = Program(new(4,count), Op(7,ConditionalEventOperation.Exit));
        var state = SceneEventExecutor.RunUntilBlocked(program.Start(default, Empty(), Consumer), 1).State;
        for (int tick = 1; tick <= count; tick++)
        {
            var result = SceneEventExecutor.AdvanceTick(state, tick == 1 ? [Start()] : [], tick == count ? 1 : 0);
            Assert.True(result.Accepted);
            Assert.Equal(tick, result.State.Tick);
            Assert.Equal(count - tick, result.State.RemainingWaitTicks);
            Assert.Equal(tick == count, result.State.Exited);
            Assert.Equal(1, result.State.Tasks.LiveTasks);
            Assert.DoesNotContain(result.Trace, t => t.TaskTrace?.Kind == SceneTaskTraceKind.BarrierPolled);
            state = result.State;
        }
    }

    [Fact]
    public void Current_multi_owner_tasks_not_entry_snapshot_determine_barrier_progress()
    {
        var tasks = SceneTaskLedger.ApplyUpdates(Empty(), [Start(), new SceneTaskUpdate.Start(SceneTaskFamily.Camera,new("sample:owner/b"))]).State;
        var pending = SceneEventExecutor.RunUntilBlocked(CameraProgram().Start(default,tasks,Consumer),1).State;
        var first = SceneEventExecutor.AdvanceTick(pending, [new SceneTaskUpdate.Complete(Id(1),0), Start()],0);
        Assert.True(first.Accepted);
        Assert.Equal(new long[] {2,3}, first.Trace[^1].TaskTrace!.MatchingTasks.Select(t => t.Sequence));
        Assert.Equal(SceneEventSuspension.Barrier, first.State.Suspension);
        Assert.Equal(0, first.InstructionsExecuted);
        var final = SceneEventExecutor.AdvanceTick(first.State,[new SceneTaskUpdate.Complete(Id(3),0),new SceneTaskUpdate.Complete(Id(2),0)],1);
        Assert.True(final.State.Exited);
        Assert.Equal(3, final.State.Tasks.TotalStarted);
        Assert.Equal(4, final.State.Tasks.NextSequence);
        Assert.Equal(0, final.State.Tasks.LiveTasks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Resumed_budget_failure_rolls_back_completion_release_allocation_and_candidate_variable_writes(int budget)
    {
        var tasks = SceneTaskLedger.ApplyUpdates(Empty(), [Start()]).State;
        var program = Program(new(4,SceneTaskClass.Camera), Op(7,ConditionalEventOperation.ZeroVariable,3), Op(10,ConditionalEventOperation.Exit));
        var pending = SceneEventExecutor.RunUntilBlocked(program.Start(new(0,0,0,9),tasks,Consumer),1).State;
        string frozen = JsonSerializer.Serialize(pending);
        var failed = SceneEventExecutor.AdvanceTick(pending,[new SceneTaskUpdate.Complete(Id(1),0),Start(SceneTaskFamily.ConditionDisplay)],budget);
        Rejected(pending, failed, SceneEventRejection.InstructionBudgetExceeded);
        Assert.Equal(budget == 0 ? 7 : 10, failed.FailureOffset);
        Assert.Equal(frozen,JsonSerializer.Serialize(pending));
        var accepted = SceneEventExecutor.AdvanceTick(pending,[new SceneTaskUpdate.Complete(Id(1),0),Start(SceneTaskFamily.ConditionDisplay)],2);
        Assert.True(accepted.State.Exited);
        Assert.Equal(0, accepted.State.Variables.V3);
        Assert.Equal(Id(2),accepted.State.Tasks.Tasks[^1].Id);
        Assert.Equal(1,accepted.State.Tasks.LiveTasks); // Interpreter exit does not cancel other work.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Arithmetic_failure_after_frame_or_barrier_rolls_back_both_domains(bool barrier)
    {
        var tasks = SceneTaskLedger.ApplyUpdates(Empty(), [Start()]).State;
        var program = Program(barrier ? new(4,SceneTaskClass.Camera) : new(4,1),
            Op(7,ConditionalEventOperation.ZeroVariable,1), Op(10,ConditionalEventOperation.AddImmediate,3,1), Op(15,ConditionalEventOperation.Exit));
        var input = SceneEventExecutor.RunUntilBlocked(program.Start(new(0,8,0,65535),tasks,Consumer),1).State;
        string frozen = JsonSerializer.Serialize(input);
        var failed = SceneEventExecutor.AdvanceTick(input,[new SceneTaskUpdate.Complete(Id(1),0),Start(SceneTaskFamily.ConditionDisplay)],3);
        Rejected(input, failed, SceneEventRejection.ArithmeticDomainExceeded);
        Assert.Equal(10, failed.FailureOffset);
        Assert.Equal(frozen,JsonSerializer.Serialize(input));
        Assert.Equal(8,input.Variables.V1);
        Assert.Equal(SceneTaskPhase.Running,input.Tasks.Tasks[0].Phase);
    }

    [Fact]
    public void Ready_pump_failure_does_not_publish_prior_writes_or_enter_a_barrier()
    {
        var program = Program(Op(4,ConditionalEventOperation.ZeroVariable,3),new(7,SceneTaskClass.Camera),Op(10,ConditionalEventOperation.Exit));
        var input = program.Start(new(0,0,0,9), Empty(), Consumer);
        Rejected(input,SceneEventExecutor.RunUntilBlocked(input,1),SceneEventRejection.InstructionBudgetExceeded);
        Assert.Null(input.Tasks.Barrier);
        Assert.Equal(9,input.Variables.V3);
    }

    [Theory]
    [InlineData("unknown",SceneTaskRejection.UnknownTask)]
    [InlineData("foreign",SceneTaskRejection.WrongScene)]
    [InlineData("stale",SceneTaskRejection.StaleTaskRevision)]
    [InlineData("illegal",SceneTaskRejection.IllegalTaskTransition)]
    [InlineData("null",SceneTaskRejection.InvalidUpdate)]
    public void Bad_task_update_preserves_vm_and_ledger_with_nested_typed_diagnostic(string problem, SceneTaskRejection reason)
    {
        var tasks = SceneTaskLedger.ApplyUpdates(Empty(), [Start()]).State;
        var input = SceneEventExecutor.RunUntilBlocked(CameraProgram().Start(default,tasks,Consumer),1).State;
        SceneTaskUpdate bad = problem switch
        {
            "unknown" => new SceneTaskUpdate.Complete(Id(99),0),
            "foreign" => new SceneTaskUpdate.Complete(new(new("sample:scene/b"),1),0),
            "stale" => new SceneTaskUpdate.Complete(Id(1),9),
            "illegal" => new SceneTaskUpdate.Phase(Id(1),0,SceneTaskPhase.Holding),
            _ => null!,
        };
        string frozen = JsonSerializer.Serialize(input);
        var failed = SceneEventExecutor.AdvanceTick(input,[Start(),bad],1);
        Rejected(input, failed, SceneEventRejection.TaskLedgerRejected,reason,1);
        Assert.Equal(4,failed.FailureOffset);
        Assert.Equal(frozen,JsonSerializer.Serialize(input));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8193)]
    public void Instruction_budget_validation_precedes_command_state_and_task_envelope(int budget)
    {
        var input = CameraProgram().Start(default,Empty(),Consumer);
        Rejected(input,SceneEventExecutor.RunUntilBlocked(input,budget),SceneEventRejection.InvalidInstructionBudget);
        Rejected(input,SceneEventExecutor.AdvanceTick(input,default,budget),SceneEventRejection.InvalidInstructionBudget);
        var waiting = SceneEventExecutor.RunUntilBlocked(input,1).State;
        Rejected(waiting,SceneEventExecutor.AdvanceTick(waiting,[],budget),SceneEventRejection.InvalidInstructionBudget);
    }

    [Fact]
    public void Acknowledged_cancellation_unblocks_class_without_rewriting_task_outcome_as_success()
    {
        var tasks = SceneTaskLedger.ApplyUpdates(Empty(), [Start()]).State;
        var waiting = SceneEventExecutor.RunUntilBlocked(CameraProgram().Start(default,tasks,Consumer),1).State;
        var cancelled = SceneEventExecutor.AdvanceTick(waiting,
            [new SceneTaskUpdate.Cancel(Id(1),0,SceneTaskCancellationReason.OwnerAcknowledgedCancellation)],1);
        Assert.True(cancelled.Accepted);
        Assert.True(cancelled.State.Exited); // Explicit D4 executes after release; the cancellation is not D4.
        Assert.Equal(SceneTaskPhase.Cancelled,cancelled.State.Tasks.Tasks[0].Phase);
        Assert.Equal(SceneTaskCancellationReason.OwnerAcknowledgedCancellation,cancelled.State.Tasks.Tasks[0].CancellationReason);
        Assert.Equal(new[]{SceneTaskTraceKind.TickAdvanced,SceneTaskTraceKind.TaskCancelled,
            SceneTaskTraceKind.BarrierPolled,SceneTaskTraceKind.BarrierReleased},
            cancelled.Trace.Where(t=>t.TaskTrace is not null).Select(t=>t.TaskTrace!.Kind));
        Assert.Equal(SceneEventTraceKind.Instruction,cancelled.Trace[^1].Kind);
        Assert.Equal(ConditionalEventOperation.Exit,cancelled.Trace[^1].Operation);
    }

    [Fact]
    public void Wrong_command_boundaries_and_update_budget_reject_atomically()
    {
        var initial = CameraProgram().Start(default, Empty(), Consumer);
        Rejected(initial,SceneEventExecutor.AdvanceTick(initial,[Start()],1),SceneEventRejection.NotWaiting);
        var waiting = SceneEventExecutor.RunUntilBlocked(initial,1).State;
        Rejected(waiting,SceneEventExecutor.RunUntilBlocked(waiting,1),SceneEventRejection.AwaitingTick);
        Rejected(waiting,SceneEventExecutor.AdvanceTick(waiting,default,1),SceneEventRejection.TaskLedgerRejected,SceneTaskRejection.InvalidUpdateBatch);
        Rejected(waiting,SceneEventExecutor.AdvanceTick(waiting,[],1,-1),SceneEventRejection.TaskLedgerRejected,SceneTaskRejection.InvalidUpdateBudget);
        Rejected(waiting,SceneEventExecutor.AdvanceTick(waiting,[Start()],1,0),SceneEventRejection.TaskLedgerRejected,SceneTaskRejection.UpdateBudgetExceeded);
        var exited = SceneEventExecutor.AdvanceTick(waiting,[],1).State;
        Rejected(exited,SceneEventExecutor.RunUntilBlocked(exited,1),SceneEventRejection.AlreadyExited);
        Rejected(exited,SceneEventExecutor.AdvanceTick(exited,[new SceneTaskUpdate.Complete(Id(99),0)],0),SceneEventRejection.TaskLedgerRejected,SceneTaskRejection.UnknownTask,0);
    }

    [Fact]
    public void Scene_tasks_survive_exit_later_ticks_and_restart_with_fresh_consumer_without_identity_reuse()
    {
        var tasks = SceneTaskLedger.ApplyUpdates(Empty(), [Start(),Start(SceneTaskFamily.DarkScreen)]).State;
        var waiting = SceneEventExecutor.RunUntilBlocked(CameraProgram().Start(default,tasks,Consumer),1).State;
        var exited = SceneEventExecutor.AdvanceTick(waiting,[new SceneTaskUpdate.Complete(Id(1),0)],1).State;
        Assert.True(exited.Exited);
        Assert.Equal(SceneTaskPhase.Entering,exited.Tasks.Tasks[1].Phase);
        var later = SceneEventExecutor.AdvanceTick(exited,[new SceneTaskUpdate.Phase(Id(2),0,SceneTaskPhase.Holding),Start()],0);
        Assert.True(later.Accepted);
        Assert.True(later.State.Exited);
        Assert.Equal(2,later.State.Tick);
        Assert.Equal(0,later.InstructionsExecuted);
        Assert.All(later.Trace,t => Assert.Equal(SceneEventTraceKind.TaskLedger,t.Kind));
        Assert.Equal(Id(3),later.State.Tasks.Tasks[^1].Id);
        var nextConsumer = new AssetId("sample:consumer/b");
        var nextProgram = new SceneEventProgram(new("sample:event/next"),[new(4,SceneTaskClass.DarkScreenHold),Op(7,ConditionalEventOperation.Exit)]);
        var restarted = nextProgram.Start(new(1,2,3,4),later.State.Tasks,nextConsumer);
        Assert.Same(later.State.Tasks,restarted.Tasks);
        Assert.Equal(2,restarted.Tick);
        var nextWait = SceneEventExecutor.RunUntilBlocked(restarted,1).State;
        Assert.Equal(nextConsumer,nextWait.Tasks.Barrier!.ConsumerId);
        Assert.Equal(nextProgram.Id,nextWait.Tasks.Barrier.ContinuationId);
        Assert.Equal(2,nextWait.Tasks.Barrier.EntryTick);
        Rejected(nextWait,SceneEventExecutor.AdvanceTick(nextWait,[new SceneTaskUpdate.Complete(Id(1),0)],1),
            SceneEventRejection.TaskLedgerRejected,SceneTaskRejection.DuplicateTerminalUpdate,0);
        var nextExit = SceneEventExecutor.AdvanceTick(nextWait,[new SceneTaskUpdate.Phase(Id(2),1,SceneTaskPhase.Leaving)],1);
        Assert.True(nextExit.State.Exited);
        Assert.Equal(4,nextExit.State.Tasks.NextSequence);
        Assert.Equal(2,nextExit.State.Tasks.LiveTasks);
        Assert.Equal(new(1,2,3,4),nextExit.State.Variables);
    }

    [Fact]
    public void Forward_branch_can_skip_frame_and_task_waits_without_ticking_or_polling()
    {
        var program = Program(Op(4,ConditionalEventOperation.JumpForwardIfZero,target:13),new(7,SceneTaskClass.Camera),new(10,1),Op(13,ConditionalEventOperation.Exit));
        var result = SceneEventExecutor.RunUntilBlocked(program.Start(default, Empty(), Consumer),2);
        Assert.True(result.State.Exited);
        Assert.Equal(0,result.State.Tick);
        Assert.Null(result.State.Tasks.Barrier);
        Assert.Equal(new[]{4,13},result.Trace.Select(t => t.Offset));
        Assert.Equal(SceneEventSuspension.Barrier,SceneEventExecutor.RunUntilBlocked(program.Start(new(1,0,0,0),Empty(),Consumer),2).State.Suspension);
    }

    [Fact]
    public void Admission_is_closed_and_start_cannot_replace_a_pending_consumer()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneEventInstruction(4,(SceneTaskClass)65535));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneEventInstruction(4,-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneEventInstruction(4,32768));
        Assert.Throws<ArgumentNullException>(() => new SceneEventInstruction(null!));
        Assert.Throws<ArgumentException>(() => Program());
        Assert.Throws<ArgumentException>(() => Program(new SceneEventInstruction(4,SceneTaskClass.Camera)));
        Assert.Throws<ArgumentException>(() => Program(Op(4,(ConditionalEventOperation)99)));
        Assert.Throws<ArgumentException>(() => Program(Op(4,ConditionalEventOperation.Exit),new(4,1)));
        Assert.Throws<ArgumentException>(() => Program(Op(4,ConditionalEventOperation.JumpForward,target:4),Op(7,ConditionalEventOperation.Exit)));
        Assert.Throws<ArgumentException>(() => CameraProgram().Start(default,Empty(),default));
        Assert.Throws<ArgumentOutOfRangeException>(() => CameraProgram().Start(new(-1,0,0,0),Empty(),Consumer));
        var pending = SceneTaskLedger.BeginBarrier(Empty(),SceneTaskClass.Camera,Consumer,4,ProgramId).State;
        Assert.Throws<ArgumentException>(() => CameraProgram().Start(default,pending,new("sample:consumer/b")));
        Assert.Throws<ArgumentException>(() => new SceneEventProgram(new("x:"+new string('a',255)),[Op(4,ConditionalEventOperation.Exit)]));
    }

    [Fact]
    public void Clock_exhaustion_from_scene_ledger_preserves_suspended_vm()
    {
        var tasks = (SceneTaskState)Activator.CreateInstance(typeof(SceneTaskState),BindingFlags.Instance|BindingFlags.NonPublic,
            null,[Scene,long.MaxValue,1L,ImmutableArray<SceneTask>.Empty,null],null)!;
        var pending = SceneEventExecutor.RunUntilBlocked(CameraProgram().Start(default,tasks,Consumer),1).State;
        Rejected(pending,SceneEventExecutor.AdvanceTick(pending,[Start()],1),SceneEventRejection.TaskLedgerRejected,SceneTaskRejection.CounterOverflow);
        Assert.Equal(long.MaxValue,pending.Tick);
    }

    private static void Rejected(SceneEventState input, SceneEventResult result, SceneEventRejection reason,
        SceneTaskRejection taskReason = SceneTaskRejection.None, int? index = null)
    {
        Assert.False(result.Accepted);
        Assert.Same(input,result.State);
        Assert.Equal(reason,result.Rejection);
        Assert.Equal(taskReason,result.TaskRejection);
        Assert.Equal(index,result.FailureUpdateIndex);
        Assert.NotNull(result.Diagnostic);
        Assert.Empty(result.Trace);
        Assert.Equal(0,result.InstructionsExecuted);
        Assert.Equal(0,result.AcceptedTaskUpdates);
        Assert.Equal(0,result.TicksConsumed);
    }

    private static void EqualTrace(SceneEventTrace[] expected, ImmutableArray<SceneEventTrace> actual)
        => Assert.Equal(JsonSerializer.Serialize(expected),JsonSerializer.Serialize(actual));
}
