# ZOD-072 — Scene task lifetimes and barrier contract

Date: 2026-10-23. Edition: North American PS1 `SCUS-94221`.
Read-only reference HEAD, verified during this investigation:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
Managed base: `fea60ef` (ZOD-071); working tree was clean at entry.

**Prove a pure scene task ledger and one barrier consumer before integrating E5
into an event interpreter.** A barrier waits for the absence of a current task
class, not for one named instance or a successful event outcome. The first proof
should use explicit logical task updates and scheduler ticks, with no rendering
workers or runtime callbacks. Keep conditional/timed event profiles unchanged.

This document selects a bounded native contract from source study. No new runtime
types, opcode capabilities, parser, scheduler or UI are implemented. The
[authored expectations](../evidence/events/task-barrier-contract-v1.json) are
**not executed tests**. They distinguish source-derived predicates from proposed
native identity, ordering and failure policies. No original script/media is copied.

## Source findings that constrain the contract

[BATTLE E5 dispatch](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/event/interpreter.c)
assembles a little-endian unsigned 16-bit task selector from two operand bytes,
then yields **before every scan**, including the first. It scans slots 1..15,
testing `is_running != 0 && task_id == selector`. The
[thread record](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/thread.h)
stores both running flag and task ID as signed 32-bit values. Do not narrow task
storage to a byte or sign-extend an E5 selector merely because other operands
use `script_s16_read`.

Consequences of the actual predicate:

- An empty match still costs one yield. Seeing no matching task at entry does not
  justify immediate completion.
- Multiple matching instances block until none match. A barrier is not a join on
  the first worker, nor a captured set of workers present when E5 was entered.
- A new matching worker or phase change before a later scan can keep it blocked.
  A worker that completes before a scan need not be observed running by that scan.
- The scan has scene scope and no ownership filter. Work started by another event
  or subsystem can match; task ownership is useful metadata, not a barrier filter.
- The inline scan does not exclude its caller's slot. In contrast,
  [control.c::thread_task_find](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/control.c)
  excludes the caller and returns the first matching slot. Replacing E5 with that
  helper without reviewing scope would change the general semantics.
- [WORLD E5](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/event/interpreter.c)
  scans slots 1..16. The first proposed proof is BATTLE-only; neither scene's fixed
  slot count becomes a generic managed collection limit.

[thread_start](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/thread/scheduler.c)
sets running to 1 but task ID to NONE; the worker tags itself afterward. Exit
clears running and task ID, then yields. Suspend clears running without ending
the context, and resume restores it. Native slots are reused. Therefore a task ID
is a mutable classification, not a permanent identity; absence from E5's scan is
not proof of successful work, resource disposal or irreversible termination.

## Selected worker families and phases

Only the classes relevant to the
[BattleStart trace](zod-066-battle-start-event.md) are proposed for the first proof:

- **Camera family:** Running maps to task class Camera (source 4). Both
  `camera_thread` in
  [scene_commands.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/scene_commands.c)
  and [camera_fusion_thread](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/camera_fusion.c)
  tag themselves 4 and eventually exit. A barrier does not distinguish their
  interpolation algorithms. Those algorithms remain unimplemented.
- **DarkScreen family:** Entering and Leaving map to DarkScreenTransition (54);
  Holding maps to DarkScreenHold (55). The
  [overlay bridge](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/scene/menu_overlays.c)
  starts the transition. The
  [REQUIRE worker](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/entry_dark_screen.c)
  changes its own task to 55 after entry, retains the same live instance while
  holding, and begins release after the controller stores 54. `RemoveDarkScreen`
  signals only the first matching holding slot through `thread_task_find`; it
  does not erase all dark-screen workers. Instance-addressed native proof updates
  do not yet implement that original first-slot selection rule or opcode 77.
- **ConditionDisplay family:** Running maps to ConditionDisplay (56). The
  [REQUIRE dispatcher](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/entry_conditions.c)
  keeps this class while running a banner or awaiting nested child work, and exits
  afterward. A nested worker ending alone is not a completion acknowledgment for
  its still-running parent. Input skip and graphics timing remain separate work.

In the proposed model, Completed and Cancelled are terminal phases with no active
barrier class. Camera/ConditionDisplay allow Running -> Completed. DarkScreen
allows Entering -> Holding -> Leaving -> Completed. Any nonterminal phase may be
acknowledged Cancelled. No reverse transitions, family changes, suspend/resume,
NONE startup interval, stop-request handshake or task revival are admitted initially.
Those are explicit proof exclusions, not claims that retail lacks those states.

## Proposed native state and ownership

`Fft.Core.Events` should own a pure immutable scene task ledger and barrier state.
The proposed fields are:

- **SceneRunId:** caller-supplied stable namespaced ID for a particular scene run.
  The composition root must choose a fresh ID for a new live attempt; replay uses
  the recorded ID in an isolated history. Core does not generate GUIDs or consult
  time. An event restart inside a live scene retains its ledger and sequence
  counter; it cannot reset them and reuse task identities while old messages can
  arrive. A new scene run requires a fresh SceneRunId.
- **Task identity:** `(SceneRunId, sequence)`, allocated monotonically in accepted
  start-command order. Sequences are never reused during that scene run.
- **Task record:** immutable family and owner ID; current phase, revision and
  terminal outcome. Owner is diagnostic/control metadata and is never used to
  narrow a scene barrier. Every accepted phase/terminal change increments revision.
- **Counters/ledger:** next sequence and all started records, including terminal
  tombstones. Preserve tombstones until the scene run is discarded so a late or
  duplicate acknowledgment cannot accidentally target a replacement worker.
- **Single barrier consumer:** Ready, Pending or Released; selected task class,
  diagnostic consumer/source offset, entry tick and continuation identity. No VM
  or event-exit flag is part of this first proof. Source identities are retained
  for diagnostics; they are not PS1 slot indexes.
- **Logical tick:** checked signed-64 counter, initially zero. Any overflow rejects
  atomically. Traces are returned per call rather than accumulated in the snapshot.

The ledger belongs to the **scene**, independently of a particular interpreter's
lifetime. Completing a task does not finish an event; completing all tasks does
not execute D4/DB; releasing a barrier does not complete the scene. D4 and full
EventEnd integration stay outside this slice. No automatic cancellation, implicit
task success, or resource cleanup is inferred from a barrier release.

Content may later validate selectors and project definitions inward to Core.
It does not become a Core dependency. AssetTool retains original bytes/unsigned
E5 decoding. Zodiac gains no FFT task classes, PS1 slots or lifecycle behavior.

## Commands, polling and tick order

These are selected semantics for the future proof, not implemented API names:

1. **ApplyUpdates** at a Ready/Released boundary accepts an ordered bounded batch
   of logical task facts. Start allocates an ID and revision 0; Camera/Condition
   start Running and DarkScreen starts Entering. Accepted task starts are already
   classified in the proof; original NONE-to-class publication is not reproduced.
   This command consumes no tick and never evaluates a barrier. While Pending,
   task updates must instead be supplied through AdvanceTick.
2. **BeginBarrier** selects one of Camera, DarkScreenTransition, DarkScreenHold or
   ConditionDisplay and publishes Pending at the current tick. It consumes no tick
   and performs no match test. Beginning another barrier while one is Pending
   rejects. Beginning one after Released always creates a fresh required yield.
3. **AdvanceTick** accepts exactly one logical tick and an ordered update batch.
   It validates and applies candidate updates first, then tests any pending barrier
   against the candidate live classes. Multiple matches across all owners matter.
   No matches publishes Released; otherwise it remains Pending. With Ready/Released
   state it updates task facts and tick only, without rechecking a past barrier.

Two sequential empty barriers therefore require two distinct AdvanceTick calls.
Updates occurring after a released poll do not retroactively re-block that barrier.
A future interpreter adapter may resume its continuation in the same tick after
Released, as ZOD-071 does, but a newly entered barrier must wait for another tick.
That adapter must publish ledger and VM changes atomically; it is not part of
the first child. The old timed API and its batch-tick behavior remain unchanged.

Phase changes and terminal acknowledgments name exact task identity and expected
revision. Complete is accepted only from the permitted completion phase; a hold
cannot be silently turned into completion. Cancel means **acknowledged terminal
cancellation**, initially with reason `OwnerAcknowledgedCancellation`, rather than
a request that pretends an
actual worker has already stopped. No worker or resource adapter exists here.

AdvanceTick's committed trace order is TickAdvanced, task updates in supplied
order, BarrierPolled (matching IDs sorted by sequence), then BarrierReleased if
empty. ApplyUpdates emits only ordered task changes; BeginBarrier emits only
BarrierEntered. Supplied order determines IDs and revisions; do not sort update
commands to manufacture determinism or poll between individual updates. Replay
records the complete accepted commands, identities, revisions and tick boundaries.

This ordering is a declared native boundary, not an assertion of instruction-level
equivalence with retail slot scheduling. Production task completion must come from
a deterministic FFT effect model or explicit recorded logical facts. GPU completion,
real elapsed time, animation draw count and file-I/O callback arrival must not decide
simulation progress. A renderer consumes state; slower rendering changes pacing,
not the logical completion tick. Original scheduler timing still needs observation.

## Failure policy and resource bounds

Proposed first-proof limits: at most 64 live tasks, 256 total task starts/tombstones
per scene run, 64 updates per command, and one tick per AdvanceTick. These are
native proof budgets, not copies of PS1 table capacities. Stable string IDs use
the existing namespaced-ID rules with a declared 256-character cap. No arbitrary
callbacks, asset payloads, executable handlers or retry loops enter this protocol.

Validate envelope/shape, supported selectors and budget first. For a referenced
task, use this deterministic order: matching SceneRunId, existing sequence,
nonterminal phase, expected revision, then legal family/phase transition. A wrong
scene, unknown task, terminal duplicate, stale revision and illegal transition
therefore have distinct typed diagnostics. Repeated terminal acknowledgments
reject as duplicates even if they repeat the original revision; no silent retry
success. Repeated acknowledgments or reused expected revisions within a batch
reject the whole batch. Distinct legal phase changes with successive revisions
are allowed in supplied order, including after a start earlier in that batch.

All commands are transactional: any failure returns the exact input snapshot,
empty committed trace and zero accepted updates/ticks. A rejected batch restores
sequence allocation, revisions, task phases, counters, barrier state and logical
tick. A start followed by an invalid update cannot leak a new task ID. A live/start
budget failure cannot evict a worker or discard a tombstone. Live capacity must
hold after each candidate update: completing before starting can free capacity,
while starting first at capacity rejects. Identical Start commands intentionally
allocate distinct instances; they are not idempotent acknowledgment retries.
Indefinite pending
work never forces completion or event exit; each bounded tick remains explicit.

## Authored expectations and one implementation child

[task-barrier-contract-v1.json](../evidence/events/task-barrier-contract-v1.json)
records independent, specification-only cases for empty and precompleted barriers,
two matching owners, newly started matches, nonmatching work, dark-screen phases,
cancellation, duplicate/stale/foreign/unknown acknowledgments, illegal transitions,
atomic allocation rollback, update budgets and sequential empty barriers. They are
source-derived expectations plus native-policy examples, not executable oracle
results or a new accepted prepared-event format. A replay plan checks identical
state and ordered traces from the same explicit scene ID and command history.

**Selected child ZOD-073: implement the pure BATTLE scene task ledger and single
barrier proof.** Use authored task facts for the three families above, in Core,
with no camera/banner code, real worker starts, original export or event-interpreter
integration. Convert the specification cases into meaningful headless regressions;
add phase/resource boundaries and immutable/replay checks. Record the exact input
commands and before/after state/trace, not expectations generated by the reducer.

Only after that proof should a separate ticket integrate validated E5 into an
explicit new interpreter profile. The current conditional and timed profiles must
continue rejecting E5. Full original workers, concurrent interpreter consumers,
scene transition cleanup, native slot allocation, suspended threads, opcode 77
selection, EventEnd and original/emulator comparison remain outside ZOD-073.

## Verification and remaining evidence

Implementation follow-up: ZOD-073 now [executes this bounded contract](../behavior/scene-task-barriers.md),
including all 17 specification cases and additional phase/resource/failure checks.
The proposal and investigation results below remain historical. ZOD-074 now
[integrates E5 in a separate synthetic profile](../behavior/scene-event-barriers.md);
no original worker is implemented.

The source review cross-checked both E5 scans, record widths, start/exit/suspend/
resume behavior, lookup/allocation helpers, camera tags, REQUIRE parent lifetime
and the 54 -> 55 -> 54 hold signal. Pinned paths, metadata cases/IDs, document links,
coverage view and whitespace are checked without executing a proposed reducer.
No decomp, original data, runtime, tests or package graph is modified.

The last full application run remains ZOD-071's **1,105 passing tests** and
zero-warning Release build; no new suite run or original execution is claimed.
This closes the selected contract spike, not task/barrier execution or complete
scene compatibility.
