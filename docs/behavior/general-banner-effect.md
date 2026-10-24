# Logical general-banner effect — ZOD-076

Date: 2026-10-23. Edition: North American PS1 `SCUS-94221`.
Managed base: `4634a7e` (ZOD-075); working tree was clean at entry.
Read-only reference HEAD verified:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.

FFT Core now drives one condition-8 general banner from explicit logical ticks
and Circle samples. The driver generates its own task-56 termination fact and
publishes effect, task ledger, interpreter and scene clock atomically. It implements
the [selected ZOD-075 contract](../architecture/zod-075-general-banner-effect.md),
including the final active yield and phase-specific skip behavior. It does not
render a banner, load assets or admit original DisplayConditions opcode 78.

## Model and commands

[GeneralBannerState.cs](../../src/Fft.Core/Events/GeneralBannerState.cs) contains the
authored definition, immutable effect state and retained `BannerSceneState`.
`GeneralBannerDefinition(Id, Condition, HoldFrames)` is input data; AttachBanner
validates it before allocation. The selected condition is 8, hold count 0..255,
with fixed source reveal/hide counts 20/10. Stable IDs follow AssetId rules and
the scene's 256-character cap.

The effect retains definition, exact scene/task identity, owner, start/last tick,
phase, completed reveal/hold/hide counts and exit cause. All active subphases
remain ConditionDisplay task class 56 at revision 0. Only driver termination
changes that task to revision 1, preserving its terminal tombstone. Visual phase
changes do not modify ledger revision.

[BannerSceneExecutor](../../src/Fft.Core/Events/BannerSceneExecutor.cs) provides:

- `AttachBanner(state, definition, ownerId, updateBudget)`: at a Ready or Exited
  interpreter boundary, allocate one ConditionDisplay task and initialize an
  already ready Revealing effect at the current scene tick. Consume no tick or VM
  instruction. One retained effect is supported for the scene, including terminal;
  a second attachment rejects rather than replacing it or allocating another ID.
- `RunUntilBlocked(state, instructionBudget)`: delegate interpreter execution
  without advancing the effect. Suspended/exited/budget failures remain typed.
- `AdvanceTick(state, circlePressed, unmanagedUpdates, instructionBudget,
  updateBudget, cancellation)`: require an explicit logical Circle sample, compute
  the next effect state, append any generated terminal acknowledgment after supplied
  unmanaged facts, then run the existing shared scene tick and continuation. Ready
  interpreters must first run to suspension/exit. Exited interpreters still allow
  scene/effect progress. An initialized immutable batch is required, including for
  zero updates; missing Circle is not inferred as false.
- `StartNextEvent(state, program, variables, consumerId)`: only after interpreter
  exit, select a validated program/variables and a different explicit consumer ID.
  Retain the exact effect and ledger, including scene tick, task IDs and counters.
  Run the new Ready interpreter separately; do not consume an effect tick at start.

StartNextEvent does not maintain a history of every consumer ID; the composition
root remains responsible for choosing fresh identities across a live scene history.
Immutable states can be replayed in isolated histories. There is no public snapshot
restore or mid-suspension replacement API.

## Source-derived progress and native cancellation

Each accepted active tick resumes one existing yield. Reveal completes 20 steps,
positive hold H completes H steps, hide completes 10 steps, and ExitYield completes
on one more tick. Natural termination therefore occurs at **H+31 ticks after
readiness**: 31/32/91/286 for H 0/1/60/255. The source loop/yield/input and loading
trace is pinned in ZOD-075; these numbers do not include original asset startup.

Circle is sampled before progress only in Revealing/Holding. It immediately enters
ExitYield without completing that iteration, then terminates on the next tick.
Circle during Hiding or ExitYield has no effect and is not deferred. A skipped
banner ends as Completed with CircleSkipped cause, preserving completed counters.
The sample represents the bit visible in the original event press word, not an
OS key held down; input mapping/filtering and tutorial routing remain separate.

Native cancellation is a distinct explicit command addressed by exact managed
task identity and expected revision. The pure driver has authority to stop its own
logical effect and generate Cancelled/OwnerAcknowledgedCancellation. Valid owner
cancellation takes precedence over normal progress and Circle on that tick,
including a natural completion that would otherwise occur. It consumes no extra
exit yield. This is the previously declared native policy, not an original cancel
opcode or an acknowledgment from a renderer.

Cancelled/Completed effect snapshots freeze at their terminal tick; later scene
ticks emit no repeated terminal task fact. Other class-56 tasks still block E5-56.
Only an explicit VM exit instruction finishes the interpreter. Retained banners
continue progressing after that exit and across an accepted next-event start.

## Ownership, atomicity and traces

The adapter validates scene/task identity, owner, family, active phase/revision,
terminal outcome and clock agreement. Active effect LastProcessedTick must equal
scene tick; terminal snapshots may retain an earlier terminal tick. Supplied
phase/complete/cancel facts may never address the managed identity, including its
tombstone. Other tasks remain governed by the existing ledger checks.

Supplied unmanaged facts keep their order; generated termination is appended.
Together they share the 0..64 update budget and existing live/start/tombstone
limits. A supplied Start at live capacity rejects even if the appended completion
would free a slot later. No reordering, eviction or candidate ID leakage is allowed.
Instruction budget remains 0..8192 and scene ticks use checked signed-64 addition.

Validation order for AdvanceTick is instruction/update budgets, batch envelope/
size, explicit input, VM boundary, attached effect/binding, supplied null/managed
references, cancellation, checked tick, then candidate effect and the underlying
event/ledger transaction. Per-task payload/scene/revision/resource validation
continues in the ledger. Other commands validate their selected definition/program
and boundary before committing. Null state is programmer misuse and throws;
submitted command errors are [typed results](../../src/Fft.Core/Events/BannerSceneResult.cs),
with nested task reasons and applicable update index/VM offset.

Every rejection returns the exact input wrapper with empty trace and zero accepted
effect ticks, task updates, instructions and ticks. A continuation budget/arithmetic
failure rolls back generated completion, barrier release, candidate effect progress,
variable writes and allocation together. This also holds when F1 resumption and
the last hide step coincide; a newly entered E5 still needs a fresh tick.

Trace ordering is:

1. Attach: TaskStarted, then EffectAttached.
2. Tick: nested TickAdvanced, EffectAdvanced, ordered supplied task changes,
   generated task termination, barrier poll/release and VM continuation.
3. Terminal tick: EffectObserved replaces EffectAdvanced and retains the same
   terminal snapshot. `AcceptedEffectTicks` is zero, while `TicksConsumed` is one.
4. Run: existing event facts wrapped without changing their contract.
5. StartNextEvent: EventStarted with the selected immutable interpreter snapshot.

Effect entries record before/after snapshots, Circle/cancellation inputs and
budgets. Attach records the definition/owner/allocated identity; Run records its
instruction budget. Record full explicit commands and selected definitions for
replay. All traces are per-call; none accumulate in scene state. Renderer resource
cleanup remains separate from logical completion authority.

## Independent evidence and validation

[GeneralBannerContractTests](../../tests/Fft.Core.Tests/GeneralBannerContractTests.cs)
execute all 24 preimplementation
[authored cases](../evidence/events/general-banner-effect-contract-v1.json).
Inputs come from that historical specification. Full effect/ledger/VM checkpoint
snapshots, ordered trace fields, exact rejection/accounting, input immutability
and identical replay are checked. The source JSON retains `executed: false` as
its original evidence classification; the regressions are the separate executed
implementation evidence. No original/emulator oracle is claimed.

[BannerSceneBoundaryTests](../../tests/Fft.Core.Tests/BannerSceneBoundaryTests.cs)
add 47 cases: input/ID/budget/command gates, retained active/terminal lifecycle,
managed-update authority, cancellation, shared acknowledgment budgets, live and
tombstone capacity, failed allocation/skip rollback, mixed F1/E5 timing and checked
clock/binding failures. Counter and inconsistent-binding cases use test-only
reflection into internal constructors rather than adding a restoration API.
[SceneScenarioEventExporterTests](../../tests/Fft.AssetTool.Cli.Tests/SceneScenarioEventExporterTests.cs)
add an opcode-78 rejection case covering all three old export methods/profiles.

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
```

Locked restore and the zero-warning/error Release build passed. All **1,302 tests**
passed: Core 508, Content 373, CLI 135, AssetTool 164, Engine 24 and Client 98.
The 72 new cases are Core 71 (24 contract +47 boundary) and CLI 1. The only project
edit copies the authored evidence into test output; production references, package
locks, old APIs/profiles and saves are unchanged. No original media, C runtime,
emulator, GPU, wall-clock time or input device is needed by these tests.

ZOD-077 traces banner-0 assets and the preparation/readiness boundary before a
presentation or opcode adapter. Original quad/color/image values, loading latency,
buffer-parity uncertainty, other condition workers, EventEnd and original
BattleStart playback remain open. H+31 establishes only the selected logical
post-readiness lifecycle.
