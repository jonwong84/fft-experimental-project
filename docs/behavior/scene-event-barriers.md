# Synthetic event task barriers — ZOD-074

Contract selected before implementation, 2026-10-23. Managed base `d64f854`.
Read-only reference HEAD verified:
`11464943bbdbd2a0af022d90cb7f10455cf911df`, edition `SCUS-94221`.

The explicit new profile is
`sample:event-profile/non-tutorial-speed1-task-barrier-v1`. It admits the existing
conditional subset, signed nonnegative F1 waits and unsigned E5 selectors
4/54/55/56, in BATTLE only. Existing profiles remain closed to E5. This is a
synthetic, non-tutorial, speed-one proof, not original BattleStart playback.

## Source and chosen native boundary

[BATTLE dispatch](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/event/interpreter.c)
assembles E5's selector from two unsigned bytes, yields before each scan, and
tests every running task in slots 1..15. F1 uses a signed-halfword operand;
non-tutorial execution passes it directly to
[thread_frames_wait](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/thread/scheduler.c),
which yields once per count. The existing [wait proof](timed-event-waits.md) and
[task contract](../architecture/zod-072-event-task-contract.md) trace callers,
widths, phase changes, WORLD differences and exclusions. Reuse the proved
[scene ledger](scene-task-barriers.md), without introducing PS1 slots into Core.

Native ordering is explicit, not a claim about original per-slot scheduler order:

- `SceneEventProgram.Start(variables, tasks, consumerId)` creates a Ready VM using
  the supplied immutable scene ledger. It rejects an already Pending barrier or
  invalid IDs/variables. Consumer ID is explicit; barrier continuation metadata
  uses the program ID plus its source offset, with the precise next operation
  retained in VM state. A composition root advances one current consumer per scene
  history; immutable snapshots may be forked for isolated replay.
- `RunUntilBlocked(state, instructionBudget)` runs Ready code to F1 suspension,
  E5 suspension or explicit D4 exit, consuming no tick. F1 zero falls through.
  E5 always enters Pending without polling. Calling while suspended rejects;
  calling after exit rejects.
- `AdvanceTick(state, updates, instructionBudget, updateBudget)` advances exactly
  one tick. It first applies the whole ordered task batch and polls an already
  pending barrier through SceneTaskLedger. It then decrements an existing F1 wait
  or resumes a released E5 continuation. Resumption pumps code in the same tick,
  within the per-call instruction budget, to the next suspension or explicit exit.
  A newly entered positive F1 or E5 never reuses that tick. Ready calls reject as
  NotWaiting. Exited calls may advance the scene ledger without executing the VM.

Instruction budget is 0..8192; updates retain the ledger's 0..64 budget and scene
bounds. Invalid instruction budget is checked first, then command state, then the
ledger batch, then continuation execution. Pending ticks with no VM resumption
may use instruction budget zero. A resumed continuation that cannot reach its
next boundary within budget rejects the entire call. Every submitted failure
returns the exact combined input state, no committed traces/instructions/updates/
ticks, and typed VM and nested task rejection diagnostics. No candidate task ID,
completion, poll, release, variable write or clock increment may leak.

The combined trace records interpreter instructions, frame/barrier entry/resume
and nested ledger facts in one sequence. On a tick all ledger facts precede VM
resumption; E5 execution records its instruction boundary followed by BarrierEntered.
Task facts preserve complete before/after records and sorted poll identities.
Callers record explicit commands/budgets for replay; traces are per call.

Scene lifetime is independent of VM lifetime: D4 leaves tasks, tombstones, next
sequence and scene tick intact. To start another event, pass the exited state's
ledger to Start with a new consumer identity and the selected variables/program.
Existing task updates may still target their exact old identities; new starts
cannot reuse sequences. No mid-suspension restart API or implicit cancellation
exists. Full EventEnd, multiple concurrent consumers and scene disposal are separate.

## Implementation and validation

Core owns [SceneEventProgram](../../src/Fft.Core/Events/SceneEventProgram.cs),
[SceneEventState](../../src/Fft.Core/Events/SceneEventState.cs),
[SceneEventExecutor](../../src/Fft.Core/Events/SceneEventExecutor.cs) and
[SceneEventResult](../../src/Fft.Core/Events/SceneEventResult.cs). Conditional
admission and ordinary transitions are composed privately; placeholder NOPs never
appear as executable frame/barrier instructions. The standalone ledger and old
conditional/timed APIs are unchanged. Scene clock, scene identity, consumer,
suspension source offset, continuation, variables and tasks are immutable.

[PreparedScenarioEvent](../../src/Fft.Content/Events/PreparedScenarioEvent.cs)
retains schema 1 and its strict envelope, adding `WaitForTaskClass` only for the
new profile and `ToSceneExecutionDefinition()` as an explicit projection. Both
timed profiles admit F1; conditional still rejects it, and only the new profile
admits E5. Every instruction is validated even when unreachable. WORLD, unknown
selectors, missing/extra operands, unsupported operations and reachable DB reject.
Projection between different execution profiles rejects rather than dropping a wait.

The named offline bridge
`ScenarioEventExporter.CreateSyntheticTaskBarriers(inspection, id)` re-inspects
retained bytes, decodes E5 as little-endian **unsigned** 16-bit and F1 as signed
16-bit, then applies the new gate. Old export methods remain closed to E5. No
new CLI command or original-event export exists; synthetic provenance cannot be
used to claim an original observation. Runtime Core has no Content, importer,
rendering, filesystem or reference-C dependency.

## Independent fixture

[task-barrier-sequence-v1.json](../fixtures/events/task-barrier-sequence-v1.json)
is authored metadata independent of the managed encoder. Its
[provenance](../fixtures/events/README.md#task-barrier-sequence) gives literal
invented bytes, hash and offsets. It has 14 structural records including DB at 50,
13 normalized operations and text at 51. Initial variables are `(7,8,2,9)`;
setup starts Camera instances 1/2 under different owners, DarkScreen 3 and
ConditionDisplay 4. The independently specified boundaries are:

- Entry tick 0: clear variable 3; E5 Camera at 7 suspends, continuation 10.
- Tick 1: complete Camera 1 and start Camera 5; still pending on instances 2/5.
- Tick 2: complete 2/5; resume, add 1, enter F1 at 15 with one tick remaining.
- Tick 3: F1 resumes and enters E5 transition 54 at 18; no poll of that new barrier.
- Tick 4: DarkScreen 3 becomes Holding (revision 1, class 55); release 54,
  add 2, enter E5 hold 55 at 26.
- Tick 5: the same instance becomes Leaving (revision 2, class 54); release 55,
  add 4, enter E5 transition 54 at 34.
- Tick 6: complete DarkScreen 3 (revision 3); release 54, execute F1 zero at 37,
  then enter E5 ConditionDisplay 56 at 40.
- Tick 7: complete ConditionDisplay 4; resume, add 8, execute D4 at 48.
  Variable 3 is 15; all five task tombstones remain, next sequence is 6.

[PreparedSceneEventTests](../../tests/Fft.Content.Tests/PreparedSceneEventTests.cs)
assert every combined snapshot and ordered trace field against independently
authored expectations, then replay the full explicit command history against a
fresh definition. Hash-verified loose-file loading, corruption/ID mismatch and
execution after deleting prepared files are also checked.
[SceneEventExecutorTests](../../tests/Fft.Core.Tests/SceneEventExecutorTests.cs)
cover each empty selector's mandatory yield, multi-owner/new matches, mixed and
sequential waits, 1/60/32767-frame boundaries, cancellation, exact traces, forward
branches, scene retention/restart and typed atomic failures. Resumption failures
roll back candidate task completion/allocation, releases, ticks and variable writes.
[SceneScenarioEventExporterTests](../../tests/Fft.AssetTool.Cli.Tests/SceneScenarioEventExporterTests.cs)
cover raw/processed agreement, independent hash/offsets, unsigned high-byte
rejection, old-profile isolation, unsupported unreachable code, signed waits,
malformed records and D0-to-D4 landing after the marker onto a barrier.

Validation from the repository root:

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
```

Locked restore and Release build passed with zero warnings/errors. All **1,230
tests** passed: Core 437, Content 373, CLI 134, AssetTool 164, Engine 24 and Client
98. The 68 new cases comprise Core 27, Content 23 and CLI 18. Production project
references, package locks, battle save formats and old execution APIs/profiles are
unchanged. No GPU, emulator, original file or C runtime is needed by the tests.

Original task completion is still supplied as explicit authored logical facts.
ZOD-075 now [selects condition 8's post-readiness logical banner](../architecture/zod-075-general-banner-effect.md),
with phase/input/exit-yield expectations and atomic ownership policy; ZOD-076
implements the first driver/adapter. No completion authority changes in ZOD-075.
Original camera/banner/
fade algorithms, opcode-77 slot selection, EventEnd, WORLD, tutorial/speed changes,
concurrent consumers and original scheduling/comparison evidence remain open.
