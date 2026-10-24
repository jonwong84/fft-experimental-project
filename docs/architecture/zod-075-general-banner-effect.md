# ZOD-075 — First deterministic scene effect: general condition banner

Date: 2026-10-23. Edition: North American PS1 `SCUS-94221`.
Read-only decomp HEAD verified during this investigation:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
Managed base: `cb5ce5c` (ZOD-074); working tree was clean at entry.

**Select the logical lifetime of DisplayConditions condition 8, after banner assets
are ready.** Model reveal/hold/hide/exit-yield progress and the original Circle
skip sample, generating completion for one scene-owned ConditionDisplay task (56).
Compose that state atomically with the existing event/ledger tick. Keep graphics,
asset loading and original opcode-78 admission outside the first implementation.

This is a source investigation and selected contract, not an implemented effect.
The [24 authored cases](../evidence/events/general-banner-effect-contract-v1.json)
are specification-only and unexecuted. No original timing or visual comparison is
available. The selected child is ZOD-076; no runtime, test or package change is
made by ZOD-075.

Follow-up discovery: [ZOD-077](zod-077-general-banner-assets.md) now resolves the
selected original image/palette/geometry and recommends ZOD-078's prepared preview.
The unknown-data statements below describe this ticket's historical evidence;
original startup latency, packet parity and visual equivalence remain unresolved.

## Why this candidate

- **Ordinary Camera:** asset-independent, but even linear mode has seven component
  targets, quantized starting positions, small-delta skips, hold sentinels, position
  lock, yaw-wrap state, multiply/divide and asymmetric rotation rounding. It also
  lands targets and clears the zoom flag before a final yield. See
  [camera_thread](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/scene_commands.c).
  [CameraFusion](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/camera_fusion.c)
  adds cumulative key tracks, relative operand rewriting, Q12 arithmetic and a
  tilt-track completion predicate. Neither is just a duration counter.
- **DarkScreen:** its logical completion can be separated from GPU submission, but
  it tracks delayed angle/radius changes for a 12x24 tile grid, distance/divisor
  arithmetic, two transition phases, an indefinite class-55 hold and an explicit
  release signal. Its extra finished frame and final yield also matter. See the
  [burst worker](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/entry_dark_screen.c).
  This is a larger first state machine, with sqrt/rotation behavior outside the
  simple horizontal variant.
- **General condition banner 8:** the selected general path runs inline in task
  56, has fixed loop counts around an unsigned-byte hold argument, and reads Circle
  at two precise locations. Geometry affects drawing but does not decide the loop
  termination. Its logical lifecycle can be checked without importing any banner
  tables or implementing a child worker. Other condition IDs, especially Ready,
  dispatch nested work and are excluded.

The verified [BattleStart block](zod-066-battle-start-event.md) contains condition 8
with hold 60 at source offset 164; its later condition-0 Ready flow at 192 is a
different path. The existing ignored 8,192-byte block was rehashed and matched
`a12ad52f48c2b6684993f196ead1721ecb8895cfd0f6690faed1a0d9ccca483f`
before checking that selection. Original bytes/inspection records stay under
`scratch/zod-066/`; the committed [source metadata](../evidence/events/battle-start-0401.json)
retains edition/image/archive/block provenance. Presence in this block is not an
observed event-entry state or proof of exact encounter timing.

## Entry, operands and task ownership

The pinned [BATTLE interpreter](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/event/interpreter.c)
fetches operand bytes through `u8* instruction` into signed-32 temporaries, then
opcode 78 starts `script_conditions_display` and passes condition and hold as
thread parameters 1/2. The selected encoded domain is condition 8 and hold 0..255;
it is not a signed-halfword F1 count. The special slot-4 workaround is only for
condition 7 and does not apply here.

The [bridge](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/scene/menu_overlays.c)
clears the hide-numeric-values flag and calls
[require_conditions_entrypoint](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/entry_conditions.c).
That function tags its own task 56, obtains signed-32 condition/argument values,
and calls `conditions_show_general(argument)` inline for conditions >=8. General
banner 8 selects image 0. This is the same task instance, not a new banner child.

[conditions_show_general](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/conditions.c)
ends its own thread on both natural and Circle paths. The
[scheduler exit](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/thread/scheduler.c)
clears running/task ID **before** its yield. Under the selected no-resume-after-exit
domain, the caller's later idle-slot lookup/condition dispatch is not additional
general-banner work. Do not append a second parent-completion wait. Native slot
exhaustion, arbitrary thread resume and other condition branches remain excluded.

## Exact post-readiness loop boundaries

Start the selected logical model immediately before the first reveal-loop yield,
with no completed reveal/hold/hide steps. Each explicit tick resumes one existing
yield; it is not a GPU draw or wall-clock frame. Source loop variables and hold
count are signed 32-bit; the pad mask is unsigned 32-bit. The selected loops are
bounded: reveal 20, hold H in 0..255, hide 10, then one exit-yield resumption, at
most 286 ticks. Proposed scene ticks retain checked signed-64 accounting.

1. **Revealing:** yield before each iteration, then read the event input. Circle
   jumps to an extra yield and exit, before rendering or incrementing that reveal
   iteration. Otherwise advance one of exactly 20 iterations (`frame < 0x14`).
2. **Holding:** only when H>0; yield, read Circle, and otherwise complete one hold
   iteration until exactly H. H=0 skips this phase without adding a yield.
3. **Hiding:** exactly 10 iterations (`frame < 0x0a`), each preceded by a yield.
   This loop does **not** read Circle. A press during hiding is ignored by this
   effect; do not persist it as a deferred skip.
4. **ExitYield:** after the final hide iteration, yield once more, then clear the
   task through `thread_current_exit`. The exit helper's own yield happens after
   running is cleared and adds no further active-task tick. Circle likewise has
   one extra active yield before termination and never enters remaining phases.

With no Circle, completion occurs on post-readiness tick **H+31**: H=0 ->31,
H=1 ->32, H=60 ->91, H=255 ->286. For H=60, reveal ends on tick 20, hold ends on
80, hide ends on 90, and task 56 remains running through tick 90. A Circle sample
on reveal tick 1 enters ExitYield immediately with reveal count zero and completes
on tick 2. A sample on the last reveal tick 20 leaves reveal count 19. A sample
on the last hold tick 80 leaves hold count 59 and completes on 81. Circle on hide
tick 81 or exit tick 91 does not change natural completion at 91.

All visual subphases remain class **56** while active. They are not the DarkScreen
54/55 phases and do not increment ledger revision on every animation step.
Completion changes the managed task from Running revision 0 to Completed revision
1. An E5-56 barrier can release on that completion tick after the generated fact
is applied; another live class-56 instance still blocks it. Task completion alone
does not execute D4/DB or close the scene.

### Input is an explicit logical sample

The banner calls
[script_get_controller_input_pointer(0)](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/menu/display.c),
which returns `g_script_event_input`, not the held-button word returned for 1.
Circle is bit 0x0020 in
[pad.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/psx/pad.h).
[script_event_input_update](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/scene/control.c)
derives presses/repeat from held/previous input; repeat selects directions/L1/R1,
not Circle. The
[BATTLE frame](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/event/frame.c)
updates this input and has additional tutorial routing/overrides.

The child consumes one explicit `CirclePressed` boolean representing the Circle
bit actually visible at that worker's logical resume. Record it on every tick,
including false. Do not synthesize it from an OS key held down, poll hardware in
Core, equate it with every raw held Circle frame, or reuse a stale sample. Input
mapping, edge detection, script masks/forced input and tutorial routing remain
outside this model. Initial support is non-tutorial speed one; changing host
render rate cannot insert or remove logical samples/ticks.

### Readiness and presentation exclusions

`conditions_load_banner_image` in
[screen_support.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/screen_support.c)
first yields/retries allocation, file request and load completion, uploads image/
palette, yields again and frees the buffer. Task 56 is already active during this
startup. **H+31 is not total opcode-78 duration.** The first proof assumes the
definition/assets are prepared before logical scene execution and attaches an
already ready banner. No I/O callback or fake fixed loading delay may stand in
for that unimplemented startup. A later opcode adapter needs an explicit policy
and original observations for loading/readiness timing.

Rendering also needs prepared quad ranges, six-piece metadata, heights, texture
pages and vertex colors. Their actual target-edition values and live original
presentation state were not established here. The source reveals lower pieces
before upper pieces (upper starts at zero-based iteration 9, the tenth reveal
update), increments wipe offsets by 8,
and clamps shared offsets in piece order. The initial buffer parity is uninitialized
in the decomp. Record that uncertainty; this logical model does not choose a
parity, calculate quads or silently fix source presentation quirks. These writes
do not control phase termination, so no quad payload is needed for this slice.

## Selected immutable Core contract

Proposed names describe behavior; implement the API in `Fft.Core.Events` without
new project/namespace extraction. Content may later validate a definition and
project inward. No prepared format/profile extension or opcode-78 admission is
part of the first child.

- **Definition:** stable namespaced ID, selected condition 8, hold count 0..255.
  Reveal/hide counts 20/10 are observed compatibility behavior, not generic
  engine defaults. Negative hold arguments can skip the source hold loop when
  called directly, but lie outside this byte-operand proof and must reject.
- **Effect snapshot:** immutable definition, exact scene/task identity and owner,
  start tick, last processed tick, Reveal/Hold/Hide completed counts, phase
  Revealing/Holding/Hiding/ExitYield/Completed/Cancelled, and exit cause
  Natural/CircleSkipped/OwnerAcknowledgedCancellation where applicable. Preserve
  counts across phase transitions; completed counts never exceed 20/H/10.
- **Task binding:** attach allocates one ConditionDisplay task at revision 0,
  immediately classifies it 56 and retains the identity. The effect driver owns
  phase/completion authority for that task. Active binding must agree with the
  scene, owner, family, Running phase and revision; terminal records must agree
  with their effect outcome. No external completion/phase/cancel fact may target
  this managed identity. Such a fact rejects the whole command.
- **Combined snapshot:** event/scene state plus at most one attached effect,
  including its terminal record until scene discard. Active effect LastProcessedTick
  equals the scene tick; terminal effect snapshots freeze at their terminal tick
  while the scene may continue. Existing ledger bounds/IDs/revisions/tombstones
  remain authoritative. Reject a second attachment, even after terminal, in this
  first proof; do not overwrite a live effect or reuse its identity.

Attach is allowed only at a Ready or Exited VM boundary with no attached effect;
it allocates the task and initializes Revealing counters at the current tick,
consuming no tick or VM instruction. A tick cannot also consume the first reveal
yield of an effect just attached at its boundary. RunUntilBlocked delegates VM
work without advancing the effect. StartNextEvent is allowed only after VM exit
and keeps the effect, ledger, scene tick and sequence counter, using a new explicit
consumer/program/variable selection. No mid-suspension replacement or automatic
cancellation is inferred from a consumer restart.

### Atomic tick composition and completion authority

For each accepted AdvanceTick at scene tick t:

1. Validate command envelope/budgets/state, explicit Circle sample and managed
   binding; compute a candidate effect update for t+1. A terminal effect emits
   no more task facts. Do not publish candidate state yet.
2. Preserve supplied unmanaged task-fact order, then append any driver-generated
   terminal fact. Reject any supplied fact addressing the managed identity, even
   if it repeats the driver's intended completion. All facts together must fit
   the existing per-call update budget (0..64).
3. Call the existing `SceneEventExecutor.AdvanceTick` with that entire candidate
   batch. It increments the shared scene tick once, applies facts, polls an
   existing E5 barrier, and resumes the continuation within instruction budget.
   All task/resource/revision checks stay in the ledger. Newly entered waits
   still need a later tick; Ready VMs reject AdvanceTick as before.
4. Publish effect, task ledger, tick and VM together only if every stage succeeds.
   Otherwise return the exact combined input, empty committed trace and zero
   accepted effects/facts/instructions/ticks. A failed completion tick must not
   consume Circle, finish the effect, release the barrier or allocate another ID.

The combined trace orders TickAdvanced, EffectAdvanced/terminal intent, supplied
unmanaged task changes, generated terminal change, BarrierPolled/Released and VM
resumption/instructions. It can wrap the existing immutable event facts and insert
the effect fact after TickAdvanced; do not change the old trace contracts. Start
and cancellation intent, before/after effect snapshots, logical input, exact task
IDs/revisions and command budgets must be recorded for replay. Pending ticks
without a terminal fact need no task-update budget; an acknowledgment tick needs
one slot, even if the VM itself executes zero instructions.

Circle skip is **original effect completion**, generating Completed with a
CircleSkipped diagnostic cause. It is not the native owner-cancellation command.
For that separate declared policy, an explicit per-tick cancellation command names
the managed task and expected revision. Validate scene/existence/binding, terminal
status and revision; the authoritative pure driver stops progress and emits
Cancelled with OwnerAcknowledgedCancellation on that tick, without an extra exit
yield. Valid cancellation takes precedence over that tick's Circle sample and
normal phase update. Late/duplicate cancellation rejects, including an original
revision repeated after terminal. This policy is not an assertion that retail
has a banner cancel opcode. A renderer is only a state consumer; GPU resource
cleanup remains separate and cannot acknowledge simulation completion.

## Independent expectations and implementation child

[general-banner-effect-contract-v1.json](../evidence/events/general-banner-effect-contract-v1.json)
contains 24 independently authored, unexecuted cases: four no-skip hold boundaries;
first/last reveal and hold skips; ignored hide/exit samples; multiple class-56
tasks; VM exit/restart; native cancellation and terminal duplicates; invalid
definitions/second attachment; managed-update authority; foreign/stale cancellation;
and atomic acknowledgment-budget, VM-budget and arithmetic failures. Range inputs
are compact explicit histories, not an implemented reducer or accepted runtime
schema. All expected values are post-readiness logical steps. Native ownership,
ordering and rejection cases are labeled separately from source control flow.

**Selected child ZOD-076:** implement this one pure logical banner driver and an
atomic adapter over the proved scene event/ledger, with headless regressions for
the 24 cases and immutable/replay/resource/clock boundaries. Use an authored typed
definition and explicit logical inputs, not original bytes, loading callbacks or
GPU completion. Keep old APIs/profiles and opcode-78 rejection unchanged. The
effect must keep progressing after VM exit and across an explicitly started next
event, and must not complete any other class-56 task. Graphics, other conditions,
original export, real startup/loading latency, EventEnd and original playback
remain separate children.

## Verification boundary

Implementation follow-up: ZOD-076 now [implements this logical driver and atomic
scene adapter](../behavior/general-banner-effect.md), with regressions for all 24
authored cases and additional resource/ownership/lifecycle boundaries. This note
and its specification artifact retain their original discovery classification;
startup/loading, graphics and opcode-78 admission remain separate.

This investigation rechecked source operands, dispatch/inline lifetime, phase
loops, input selection/staging, asset-readiness yields and task clearing; verified
the existing selected block hash; and checked expectation IDs/counts, pinned
source paths, links, generated coverage view and changed-file whitespace. It did
not execute the proposed effect model, C reference, emulator or original banner.
The last full application run remains ZOD-074's **1,230 passing tests** and clean
Release build; the suite was not rerun for this documentation-only milestone.
