# Explicit-tick event waits (ZOD-071)

Date: 2026-10-23. Edition: North American PS1 `SCUS-94221`.
Read-only reference HEAD, verified for this milestone:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
Managed base: `de040e5` (ZOD-066). Existing bug-catalog changes were preserved.

The new timed synthetic profile adds one capability: non-tutorial, speed-one F1
waits driven by explicit event scheduler ticks. It composes ZOD-065's existing
variable/branch operations with immutable pending-wait state. No clock, renderer,
input polling, RNG, original bytes or C code drives execution.

This is source-derived behavior plus a declared native tick contract and synthetic
regressions. No original interpreter/emulator semantic oracle was run. In
particular, one native tick is a supplied scheduler resumption, not a claim of
one GPU frame, a fixed number of milliseconds or universal PS1 VSync equivalence.
The [original BattleStart investigation](../architecture/zod-066-battle-start-event.md)
still lists the camera/task/banner/mutable-operand/EventEnd prerequisites.

## Source and domain

The pinned
[BATTLE dispatcher](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/event/interpreter.c)
reads F1's two operand bytes through the signed little-endian halfword helper.
Outside tutorial mode it calls `thread_frames_wait(first_operand)`. Tutorial mode
may instead divide a count of at least three by the VSync interval; that mode is
excluded. The
[scheduler helper](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/thread/scheduler.c)
loops `i = 0; i < count; i++`, yielding once each iteration. A positive count
therefore resumes after exactly that many yields. Zero and negative source counts
perform no yields; negative values are rejected as outside this proof domain,
not clamped or reinterpreted as unsigned.

[Event frame processing](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/event/frame.c)
runs around a scheduler yield and increments its frame counter afterward.
[script_event_speed_set](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/scene/menu_sessions.c)
also changes the BATTLE VSync interval. The timed proof fixes non-tutorial speed 1
through its profile; it does not implement speed changes or host pacing. Original
BattleStart's F1 at offset 83 requests 60 yields before its later speed change,
which motivated this small capability. Its other instructions remain unsupported.

Admission limits are explicit: F1 counts 0..32,767, variables 0..3 with values
0..65,535, at most 8,192 normalized operations, instruction budget 0..8,192 per
call, and supplied tick count 1..32,767 per `AdvanceTicks` call. Count and tick
limits have distinct purposes even though their maxima coincide. Instruction
budget is shared across all resumptions within one call, not reset per tick.

## Profiles, preparation and ownership

`PreparedScenarioEvent.Profile` remains `sample:event-profile/conditional-v1`.
It still rejects F1 and projects only to `ConditionalEventProgram`; its serialized
shape, supported operations, synchronous API and existing regressions are unchanged.

The new `PreparedScenarioEvent.TimedProfile` is
`sample:event-profile/non-tutorial-speed1-timed-v1`, admitted only with Battle
scene metadata. It uses the existing strict prepared-event format/schema 1 and
adds semantic `WaitFrames` records, whose sole operand is the signed-halfword count.
`ExecutionProfile` records the explicit selection. Unknown profiles, World context,
negative/oversized counts, extra operands/targets and reachable DB still reject
before runtime projection. Existing label resolution, CFG, byte bounds, provenance,
JSON limits and manifest hash checks apply to both profiles.

[ScenarioEventExporter.CreateSyntheticTimed](../../src/Zodiac.AssetTool.Cli/ScenarioEventExporter.cs)
is the separate offline API for authored blocks. It reads F1 as signed 16-bit;
ordinary `CreateSynthetic` remains closed to F1. No new CLI command or retail
export path is added. Original bytes must not be relabeled synthetic.

[PreparedScenarioEvent.ToTimedExecutionDefinition](../../src/Zodiac.Content/Events/PreparedScenarioEvent.cs)
projects to [TimedEventProgram](../../src/Zodiac.Framework/Events/TimedEventProgram.cs) in
FFT Core. DB and label bytes are not executable. The immutable timed definition
contains either a closed conditional operation or a frame count. It privately
normalizes each wait to fallthrough for existing CFG validation and ordinary
transitions; the placeholder NOP is not an observable timed instruction. The
timed executor handles the wait boundary instead. No change to the synchronous
Core operation enum, executor or snapshot is needed.

Core owns definition/state/transitions/traces; Content owns prepared DTO validation
and projection; AssetTool owns original framing. Core does not reference Content,
and Zodiac gains no FFT-specific time or task behavior. No projects, packages,
battle save DTOs, event persistence, runtime file dependencies or UI change.

## Exact execution contract

`program.Start(variables)` creates a ready immutable state with zero elapsed ticks.
`RunUntilBlocked(state, instructionBudget)` executes until a positive wait or
explicit Exit, consuming no ticks. Every operation, including F1, consumes one
instruction. A zero wait emits `ZeroWait` and falls through in that same call.

Entering a positive wait emits `WaitEntered` with the full remaining count and
publishes a suspended state. Entry reaches the first yield; it **does not** consume
a caller-supplied tick. State `Offset` is the following operation's continuation,
while `WaitingAtOffset` identifies the wait. Following operations do not execute
while the count remains positive. `IsWaiting`, `RemainingWaitTicks`, variables and
monotonic `ElapsedTicks` are part of the immutable snapshot; suspension is not exit.

`AdvanceTicks(state, ticks, instructionBudget)` requires a pending wait and consumes
exactly the requested positive tick count. Each tick represents returning from
one yield. It decrements the remaining count and increments elapsed ticks once:

- With a positive remainder, emit `WaitTick` and remain suspended. No instruction
  budget is needed for that decrement.
- At zero, emit `WaitResumed`, clear the wait, and immediately execute following
  operations in the **same supplied tick** until the next positive wait or Exit.
  Those operations consume the call's shared instruction budget.
- A newly entered positive wait retains its full count. It cannot reuse the tick
  that resumed the previous wait. Thus two sequential one-tick waits require two
  distinct supplied ticks, even when both are provided in one batch.

Ticks supplied after Exit are not silently discarded: the entire call rejects
with `UnusedTicks`. A ready state rejects ticks with `NotWaiting`; a pending wait
rejects `RunUntilBlocked` with `AwaitingTick`. An exited state rejects both APIs
with `AlreadyExited` for valid arguments. Invalid parameter ranges reject first.
Instruction exhaustion before reaching a publishable wait/exit is an error, not
a successful partial run.

Every failed call returns the **exact input state object**, empty committed trace,
zero consumed instruction/tick counts, typed rejection, diagnostic and failure
offset. This includes failure after a final tick or after earlier waits/writes
within a batch. No tick or candidate variable write leaks through; retrying from
the same input with an adequate budget is deterministic. Successful earlier calls
are already committed boundaries and remain intact.

[TimedEventTrace](../../src/Zodiac.Framework/Events/TimedEventExecutor.cs) records kind,
source offset, ordinary operation (null for wait transitions), variables before/
after, continuation offset, remaining count, absolute logical tick and exit flag.
Each ordinary operation or wait entry/zero wait emits one record; each consumed
tick emits either `WaitTick` or `WaitResumed`. Exit retains its own source offset,
as in the synchronous proof. Splitting an accepted tick batch across calls yields
the same ordered traces and state when budgets are sufficient.

## Independent fixture and checks

[timed-wait-sequence-v1.json](../fixtures/events/timed-wait-sequence-v1.json) was
authored independently of the C# encoder from literal synthetic instructions.
Its [provenance](../fixtures/events/README.md#timed-wait-sequence) records the full
invented block hash, text boundary 42 and offsets. It starts with dirty variables
`(7,8,2,9)`, clears variable 3, then combines arithmetic with waits 1, 0, 60 and 1.
The independently specified boundaries are:

- Entry: wait at 12, continuation 15, remaining 1, tick 0, variable 3 = 1.
- Tick 1: wait at 23, continuation 26, remaining 60, variable 3 = 3. The zero
  wait at 20 consumed an instruction and no tick.
- Tick 60: still at wait 23 with remaining 1 and variable 3 = 3.
- Tick 61: wait at 31, continuation 34, remaining 1, variable 3 = 7.
- Tick 62: explicit Exit at 39, variable 3 = 15. No DB execution.

Core tests cover zero/one/60/max counts, exact boundaries, alias/ordinary operation
composition, replay, sequential waits, separate/shared budgets, malformed admission,
unconsumed ticks, wrong-state commands, post-exit rejection and atomic failures.
Content tests compare literal before/after/resume traces and fresh-definition batch
replay, strict profile admission and hash-verified file loading. CLI tests verify
signed raw/processed decoding, the independent fixture hash, existing-profile
rejection and D0-to-D4 landing on a wait. E5 barriers, speed changes, malformed
bytes and reachable EventEnd remain rejected.

Validation from the repository root:

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
```

Locked restore and zero-warning/error Release build passed; all **1,105 tests**
passed: Core 353, Content 350, CLI 116, AssetTool 164, Engine 24 and Client 98.
The 48 new cases comprise Core 21, Content 14 and CLI 13. No original file,
emulator, C executable, clock or graphics device is required by those tests.

ZOD-072 now [defines the scene task-lifetime/barrier contract](../architecture/zod-072-event-task-contract.md),
and ZOD-073 now [implements its pure ledger/single-consumer proof](scene-task-barriers.md).
ZOD-074 now [integrates E5 in a separate synthetic profile](scene-event-barriers.md);
this timed API/profile remains unchanged. Original camera workers, banners, operand rewriting, speed changes,
EventEnd and original timing observations remain open. General scheduling and
original BattleStart playback are not established by this frame-wait proof.
