# ZOD-080 — Bounded DisplayConditions admission and update order

Date: 2026-10-24. Edition: `SCUS-94221`. Managed base:
`07431935a64890db4524ae0be223a9c688392d21`; the preceding event-identity work was
already uncommitted at entry and is preserved. Read-only reference HEAD, freshly
verified: `11464943bbdbd2a0af022d90cb7f10455cf911df`.

Select **one authored BATTLE condition-8 DisplayConditions instruction**, using
prepared artwork/resources ready before scene creation. Its logical worker is
the existing general-banner model. Admit it through a new explicit profile only
after proving an **event-before-worker update transaction**. Preserve the old
effect-first synthetic adapter and every existing profile/API unchanged.

This is a completed source/contract selection, not opcode-78 implementation.
[Source evidence](../evidence/events/banner-opcode-admission-v1.json) and
[28 authored expectations](../evidence/events/banner-opcode-admission-contract-v1.json)
are metadata/specification only. The expectations have not run against an
implementation. ZOD-081 implements the Core transaction; ZOD-082 then connects
strict authored preparation/loading. No original event or new runtime API is
enabled by this document.

## Source path, operands and asynchronous admission

**Implementation follow-up (2026-10-25):** [ZOD-081](../development/ordered-banner-transactions.md)
now executes the 27 Core expectations with a distinct typed ordered executor.
The original specification artifact remains unchanged; BOP-028 and authored
Content/host profile admission remain ZOD-082's separate gate. Statements above
describe this document's original preimplementation checkpoint.

Pinned source entry points:

- `src/battle/event/interpreter.c`: opcode `0x78` fetches two **unsigned bytes**,
  selects the first idle slot, starts `script_conditions_display` and writes
  condition/hold to its first two 32-bit parameters. It continues to the next
  instruction without waiting for the new worker. The slot-4 workaround is for
  condition 7 and is outside this selection.
- `src/lib/event/script/control.c::thread_slot_resolve`: slot 1 is already occupied
  by the event interpreter, so the selected child occupies a later slot, 2..15.
  Full native slot exhaustion exits its caller. The managed bounded profile
  requires available capacity and rejects a capacity failure atomically; it does
  not emulate slot exhaustion or reuse native slots as stable managed IDs.
- `src/lib/thread/scheduler.c::thread_start`: marks the selected slot running with
  task ID 0. The wrapper clears numeric-panel hiding and enters REQUIRE. REQUIRE's
  `entry_conditions.c` sets task 56, reads the two arguments and runs the general
  helper inline for this condition. There is one task instance, not a parent plus
  a new banner child.
- `src/event/require/conditions.c`: condition 8 selects image 0 and calls the
  already traced logical reveal/hold/hide/extra-yield path. Hold is 0..255; it is
  not a signed halfword or a packed `0x3c08` value.
- `src/lib/thread/scheduler.c::thread_current_exit`: clears running/task before
  yielding. General-banner exit does not return into another condition branch.

The actual original source command has already been identified in
[event 4](../development/original-banner-event-identity.md): opcode 78 at offset
804, condition 8 / hold 60, immediately followed by E5 task 56 at 807. Event 401's
static block contains its own condition-8 instruction, but its fallback entry
remains unobserved. Neither complete event fits the selected runtime subset.

## Why the existing adapter cannot simply admit the opcode

Native `thread_yield` scans increasing slot IDs. The interpreter in slot 1 resumes
and polls its E5 wait before the banner in a later slot advances or terminates.
Thus the task's terminal fact and the parent's observation of it occur in
different scheduler updates. The full handwritten BATTLE switch region
`0x8014ca80..0x8014cbb4` is 308 bytes, SHA-256
`2f09dae8797f8acbde873608b889474322bad8efbce867c640d5ef74cdb1289a`;
the pinned `target/battle.yaml` declares that exact region.

Three retained, hash-bound original states independently corroborate that order:

1. Source frame **9001**: event 4 waits at **807**, while slot 3 is active task 56,
   condition 8 / hold 60, at the skip exit yield.
2. Source frame **9002**: the event still waits at **807**, while slot 3 is cleared
   at scheduler exit. The banner has finished; the earlier-slot interpreter has
   not yet observed that completion.
3. Source frame **9003**: the interpreter waits at **813**, and the reused slot is
   now condition **0** / hold 60, active task 56. The parent advanced on the next
   update, executed the next display command at 810, and reached its following wait.

These are the previously preserved Circle-hold continuations; no new emulator or
original-instruction run was needed. The evidence report checks resident code,
current/cache IDs, exact processed event-4 payload, parent offset and worker state.
It does not relax the earlier live-worker-only identity inspector to accept a
cleared worker. Source-frame counts are local observations, not universal frontend
frame or presentation timing conversions.

`BannerSceneExecutor.AdvanceTick` currently advances the effect, supplies its
terminal update, and then invokes the event/ledger tick. `SceneTaskLedger` applies
that batch before polling. That ordering is explicitly selected by ZOD-075/076's
synthetic proof, whose programs still cannot start a banner instruction. It
cannot establish the native parent/worker order needed for opcode admission.
Changing it globally would invalidate existing contracts and capture histories.

## Selected domain and resource boundary

The future profile name is proposed as
`sample:event-profile/non-tutorial-speed1-preloaded-banner-ordered-v1`.
It is authored synthetic input, BATTLE only, non-tutorial, speed one. It admits
the existing bounded conditional operations, F1, selected E5 classes and exactly
one syntactic DisplayConditions instruction with condition **8**, hold **0..255**.
Forward control flow may bypass that one instruction. A new scene may execute it
at most once, including across interpreter restart; terminal effects remain retained.

Old conditional, timed and task-barrier profiles keep rejecting opcode 78. WORLD,
Ready/condition 0, condition 7, other general images/conditions, EventEnd, retail
import and all other missing opcodes remain rejected, even in unreachable records.
The full original event 4 and event 401 must still fail preparation. Do not copy
their command slices and label them synthetic; regression scripts are authored
independently. D4 remains the existing synthetic interpreter exit, not retail
EventEnd behavior.

The host must validate/load prepared artwork and presentation resources before
creating the logical scene, following ZOD-077/078. Content owns hashes, decoded
indices/palette/pieces and prepared-file validation. The composition root binds
the loaded artwork to a stable Core resource/definition identity and an explicit
scene owner. Core sees immutable logical IDs/condition only, never file paths,
textures, callbacks, original formats or a renderer. That binding is a trusted
preloaded-domain assertion, not Core verification of disk/GPU readiness.

The opcode's hold byte creates the logical definition for this instance; artwork
is independent of hold duration. Missing/mismatched binding or readiness failure
cannot start a task, consume a tick, advance an instruction or consume an identity.
Retail task 56 exists during loading, whereas this profile begins at the first
post-readiness reveal yield. No fixed loader delay, asynchronous resource state or
zero-latency original claim is introduced. Device loss may pause/rebuild the view
without changing logical effect/VM/task history.

## Pure ordered transaction contract

External calls publish one complete immutable result or preserve the exact input
state and publish no trace. There must be no public half-tick, poll-now/apply-later
handle or arbitrary callback that can escape the transaction.

**RunUntilBlocked at scene tick T:** execute bounded instructions in source order.
On DisplayConditions, validate the ready binding, allocate one managed task/identity
and initialize the existing effect at its first reveal yield with zero progress.
Advance the instruction normally and continue until a frame/barrier suspension or
synthetic exit. This costs one instruction and one task Start, consumes no tick
or Circle sample, and does not turn asynchronous admission into an implicit wait.
An immediately following E5 enters Pending and still requires a later update even
if it sees no other tasks. Birth at T never performs the first reveal iteration.

**AdvanceTick from a blocked or exited interpreter:**

1. Validate binding, explicit Circle sample (including false), all typed updates,
   cancellation, budgets and checked tick increment into a private candidate.
2. Advance the clock to T+1. Resume the existing frame wait or poll the existing
   barrier against task facts published at the end of T. Post-VM worker updates
   from this call are not visible to this poll.
3. If allowed, pump the interpreter at T+1 with one cumulative instruction budget.
   It may admit the selected banner, continue, enter another wait or exit. A newly
   entered E5 cannot poll in this same call. New managed Start facts are recorded
   in this phase; later worker completions cannot free capacity retroactively.
4. Apply supplied unmanaged worker facts in their explicit order. Then advance
   the existing managed effect once and generate its terminal fact if it clears.
   An effect born at T+1 is initialized at its first yield and **does not advance
   again at its birth tick**. No callback or rendering work supplies completion.
5. Publish the candidate VM, effects, ledger, identities and ordered trace together.
   A pending barrier may coexist with a now-terminal task; it polls on the next
   update. Post-poll facts cannot retroactively release or re-block a barrier.

Unmanaged facts in this first proof are explicitly a post-VM input batch. They
are not a claim to implement arbitrary native slot ordering, worker interactions
or exact physical slot capacity. The managed completion follows the supplied
facts, as in the existing budget contract; do not reorder updates to manufacture
free capacity. This single event-before-worker relation is the selected original
ordering constraint.

All task starts/updates, including generated Start/Complete/Cancel, share the
existing update-budget envelope of 0..64. Instructions share the existing bounded
0..8192 envelope across all pumps within a call. Sequence/revision/tick arithmetic
is checked. Late arithmetic, capacity, binding or budget failures roll back every
candidate phase. An exited VM can still tick its retained effect. Starting a new
consumer preserves scene IDs, clock, live tasks and tombstones; it does not grant
permission to start a second banner.

Only the driver updates its managed task identity, including its terminal record.
An acknowledged owner cancellation is a separate native policy: it clears the
worker in the post-VM phase and becomes visible to a barrier on the next update.
It is not an original Circle skip. The pure GeneralBannerState transition rules
and accepted-snapshot presentation projection remain unchanged.

## Exact boundary examples

Let the authored program admit the banner and enter E5 at T=0. Its existing
post-readiness lifetime stays H+31 ticks. The waiting parent resumes at **H+32**:
H=0 clears at 31 / resumes 32; H=60 clears 91 / resumes 92; H=255 clears 286 /
resumes 287. Do not extend the effect's lifetime to hide this observation delay.

A first-reveal Circle sample at tick 1 enters ExitYield; tick 2 clears the task,
and tick 3 resumes the parent. The observed hold case corresponds to hold progress
1 before tick 22: skip yield 22, clear 23, parent resume 24. Circle during hide
does not change the natural lifetime. Two sequential empty barriers need separate
updates. A newly admitted effect after an F1 resumes remains at zero progress
through that birth update.

These are independently authored selected-domain expectations, not a replay of
event 4 or a measurement of asset-load/display latency. The fixture includes
rollback, other-owner class membership, post-poll updates, preloaded failure,
skipped admission, event exit/restart, repeat admission rejection, rendering
independence and full trace/replay expectations.

## Implementation split and review gates

**ZOD-081 — Prove ordered event/banner transactions:** add a distinct typed Core
program/adapter for this contract, reusing the conditional executor, banner reducer
and ledger validation. A narrow internal pump/ledger refactor is justified if
needed; every old public behavior and trace stays unchanged. Keep typed admission
requests private inside one atomic call. Convert the Core expectations into
meaningful regressions plus boundary/rollback cases; the profile/host expectation
belongs to ZOD-082. Raw bytecode, Content,
prepared schemas and original exports are outside this child.

**ZOD-082 — Prepare/execute the authored preloaded banner profile:** after ZOD-081,
add explicit AssetTool.Cli conversion and strict Content admission/projection for
the new synthetic profile. Decode the two byte operands separately, preserve old
JSON/profile behavior, reject unsupported provenance/records, validate readiness
and provide a prepared-files-only finite host check. Test the complete authored
78/E5 sequence through preparation, load, execution and rendering-independent
replay. No retail event import, original startup duration, Ready banner or complete
BattleStart is implied.

Existing 1,494 .NET / 163 Python passing-test counts remain historical; this
documentation/source study changes no managed or verifier implementation and
does not rerun those suites. Source/evidence hashes, case structure, local links
and coverage views are checked separately. No automatic commit.
