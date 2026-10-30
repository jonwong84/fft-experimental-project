# Scene task ledger and barrier proof — ZOD-073

Date: 2026-10-23. Edition: North American PS1 `SCUS-94221`.
Read-only reference HEAD verified for this milestone:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
Managed base: `56b94e4` (ZOD-072); working tree was clean at entry.

`Zodiac.Framework.Events` now implements the bounded BATTLE scene ledger and single barrier
consumer selected by [ZOD-072](../architecture/zod-072-event-task-contract.md).
This executes authored logical task facts; it does not execute E5, start original
camera/banner/fade workers or play an original event. The source predicate and
worker-family evidence remain pinned in that investigation. Native identities,
revisions, budgets and tick/update order are declared policies, not a reproduction
of instruction-level PS1 scheduling.

## State and commands

[SceneTaskState](../../src/Zodiac.Framework/Events/SceneTaskState.cs) is immutable. Create it
with an explicit namespaced `AssetId` identifying this scene run. IDs follow the
existing lowercase `namespace:path` rules with a 256-character cap. A composition
root must choose a fresh scene ID for each new live attempt; isolated replay uses
the recorded ID. No GUID, clock, runtime worker, callback or I/O participates.

Each task has `(SceneRunId, Sequence)`, an owner, immutable family, current phase,
revision and optional cancellation reason. Start assigns the next sequence and
revision zero; every accepted later change increments revision. Sequences are
never reused, including after completion or cancellation. Terminal tasks remain
as tombstones for the whole scene run. `Tasks` preserves accepted start order;
`TotalStarted`, `LiveTasks` and `NextSequence` expose the bounded accounting.

The selected families and their active classes are:

- Camera: Running (4), then Completed.
- ConditionDisplay: Running (56), then Completed.
- DarkScreen: Entering (54), Holding (55), Leaving (54), then Completed.

Any active phase may instead become Cancelled with
`OwnerAcknowledgedCancellation`. This is an acknowledged terminal fact, not a
request that assumes an actual worker has stopped. Completed and Cancelled have
no active class and remain distinct outcomes. A dark-screen hold cannot complete
before passing through Leaving. Suspend/resume, native NONE startup intervals,
revival, reverse transitions and family changes are unsupported.

[SceneTaskLedger](../../src/Zodiac.Framework/Events/SceneTaskLedger.cs) provides three pure
commands, returning [SceneTaskResult](../../src/Zodiac.Framework/Events/SceneTaskResult.cs):

1. `ApplyUpdates(state, updates, updateBudget)` applies an immutable ordered batch
   without ticking or polling. It accepts Ready or Released state; Pending rejects
   with AwaitingTick, including an empty batch.
2. `BeginBarrier(state, selector, consumerId, sourceOffset, continuationId)` selects
   Camera, DarkScreenTransition, DarkScreenHold or ConditionDisplay and enters
   Pending at the current tick. It never polls, even if every task is terminal or
   no task exists. A second pending barrier rejects. Diagnostic consumer/offset/
   continuation metadata persists through polling and release.
3. `AdvanceTick(state, updates, updateBudget)` consumes exactly one explicit tick,
   applies the entire ordered batch, then polls a pending barrier against the
   current active class of every task in the scene, across all owners. No matches
   changes Pending to Released. Ready/Released calls update the clock and tasks
   without polling any prior barrier.

Updates are `SceneTaskUpdate.Start`, `Phase`, `Complete` and `Cancel`. Referenced
updates require exact identity and expected revision. A batch can reference a task
started earlier in that batch, and can make successive legal changes with advancing
revisions. Identical Start payloads intentionally create distinct instances.

For example, with two Camera tasks owned by different events, completing one on
tick 1 leaves a Camera barrier pending; completing the other on tick 2 releases it.
Completing the old match and starting a new match in the same tick leaves it
pending. DarkScreen can release a class-54 barrier by entering Holding while the
same instance remains live as class 55. These are live class predicates, not joins
on a captured instance set or first-slot selection.

Released barriers do not re-block when matching work starts later. Beginning a new
barrier creates a new mandatory tick boundary, even if it has the same selector.
Completing all tasks does not execute D4/DB, finish an event or discard the scene.
There is no VM/event-exit state in this proof. A future interpreter must retain
the scene ledger across event restarts and publish combined changes atomically.

## Ordering, failure and limits

ApplyUpdates emits task changes in supplied order. BeginBarrier emits only
BarrierEntered. AdvanceTick emits TickAdvanced, ordered task changes, BarrierPolled
with matching identities sorted by sequence, then BarrierReleased if empty. Task
trace entries contain before/after records; barrier trace entries retain consumer,
source offset, continuation and entry tick. Traces are per call, not stored in the
scene snapshot. Record the accepted command history, including explicit inputs
and tick boundaries, to replay it. The API does not accept a serialized fixture or
provide public snapshot restoration.

The limits are 64 live tasks, 256 total starts including tombstones, 64 updates per
command, IDs up to 256 characters, and exactly one tick per AdvanceTick. A caller
may choose an update budget of 0..64. These are native proof limits, not PS1 slot
counts. Live capacity is checked after each candidate update: complete-before-start
can free capacity, while start-before-complete at capacity rejects. No eviction or
forced completion occurs. Tick, sequence and revision increments use checked
signed-64 arithmetic.

The whole batch's envelope, budget, IDs and supported payload shapes are checked
before task lookup. Referenced updates then check scene, existence, nonterminal
phase, expected revision, and legal transition in that order. Consequently duplicate
terminal acknowledgment wins over a stale revision on that tombstone, and stale
active revisions win over an illegal transition. Diagnostics have typed rejection
codes, text and an update index when applicable. Null state is programmer misuse
and throws; invalid initial scene IDs throw at Create, as existing AssetId creation
does. Submitted command failures are typed results.

Every rejection returns the exact input snapshot, empty committed trace, zero
accepted updates and zero consumed ticks. Candidate identities, phase changes,
revisions, clock changes and barrier polls cannot leak from a rejected command.
Pending work can remain pending indefinitely; each tick still has bounded work.

Production completion facts must later come from a deterministic FFT effect model
or recorded logical facts. Rendering may consume state and change pacing; GPU
completion, draw counts, elapsed time and callback arrival must not determine the
logical completion tick. No production completion adapter exists in this slice.

## Evidence and regression results

[SceneTaskContractTests](../../tests/Zodiac.Framework.Tests/SceneTaskContractTests.cs) loads
the exact 17 preimplementation [ZOD-072 cases](../evidence/events/task-barrier-contract-v1.json).
Setup uses public accepted updates rather than a restore API. Independently authored
C# expectations assert complete snapshots, every ordered trace field, rejection
identity/index/accounting and replay of the whole command history. The original JSON
remains a historical, specification-only artifact with `executed: false`; these
tests are the separate implementation evidence, not an original-game oracle.

[SceneTaskLedgerTests](../../tests/Zodiac.Framework.Tests/SceneTaskLedgerTests.cs) adds 40
cases covering every active class, completion/cancellation, dark-screen transition
guards, terminal duplicates, same-batch starts/revisions, multi-owner live polling,
released/ready boundaries, malformed/default payloads, IDs and selectors, budget
limits, capacity ordering, 256 retained tombstones, failed allocation reuse,
immutable inputs and sustained pending work. Counter exhaustion uses test-only
reflection into the internal constructor; finite start/phase budgets make sequence
and revision overflow unreachable through valid public histories. No public restore
or arbitrary-counter API was added for testing.

Validation from the repository root:

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
```

Locked restore and the Release build passed with zero warnings/errors. All **1,162
tests** passed: Core 410, Content 350, CLI 116, AssetTool 164, Engine 24 and Client
98. The 57 new cases are Core-only: 17 specification regressions and 40 additional
phase/resource/failure checks. Existing conditional/timed event regressions and
their unsupported-opcode admission checks also passed. No original media, C
executable, GPU or emulator is required by these tests. Package locks, production
project references, prepared profiles and save schemas are unchanged.

ZOD-074 now [integrates E5 in an explicit synthetic profile](scene-event-barriers.md),
including atomic ledger/VM state and mixed frame-wait/barrier ordering. The ledger
and its standalone API remain unchanged. Original
workers, opcode-77 first-slot behavior, concurrent interpreter consumers, WORLD
scanning, EventEnd and original scheduling observations remain open.
